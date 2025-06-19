using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using ChatCore.Interfaces;
using ChatCore.Models.Bilibili;
using ChatCore.Utilities;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.HarmonyPatches;
using EnhancedStreamChat.Utilities;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStream_139.Interfaces;
using HMUI;
using IPA.Utilities;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SiraUtil.Zenject;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRUIControls;
using Zenject;
using Color = UnityEngine.Color;

namespace EnhancedStreamChat.Chat
{
    [HotReload]
    public partial class ChatDisplay : BSMLAutomaticViewController, IChatDisplay, IAsyncInitializable, IDisposable, ILatePreRenderRebuildReceiver
    {
        // 单例实例，用于向后兼容
        private static ChatDisplay _instance;
        public static ChatDisplay instance => _instance;

        private readonly ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground> _messages =
            new ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground>();

        private ChatConfig _chatConfig;
        private ESCFontManager _fontManager;
        private EnhancedTextMeshProUGUIWithBackground.Pool _textPool;
        private MemoryPoolContainer<EnhancedTextMeshProUGUIWithBackground> _textPoolContainer;

        private bool _isInGame;
        private bool _isInitialized = false;
        private bool _isUpdatingLayout = false;
        private bool _updateMessagePositions = false;
        
        // IChatDisplay 接口实现
        public bool IsReady => _isInitialized && _chatScreen != null;
        
        // 依赖注入构造方法
        [Inject]
        public void Construct(
            EnhancedTextMeshProUGUIWithBackground.Pool textPool,
            ESCFontManager fontManager)
        {
            _textPool = textPool;
            _textPoolContainer = new MemoryPoolContainer<EnhancedTextMeshProUGUIWithBackground>(textPool);
            _fontManager = fontManager;
            _chatConfig = ChatConfig.instance; // 使用单例，因为 ChatConfig 还未迁移
        }

        private void Awake()
        {
            _instance = this; // 设置单例实例
            this._waitForEndOfFrame = new WaitForEndOfFrame();
            DontDestroyOnLoad(this.gameObject);
            // Logger.Debug("ChatDisplay Awake");
            // VRPointerOnEnablePatch.OnEnabled += this.PointerOnEnabled;
        }
        
        // 实现 IAsyncInitializable 接口
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            Logger.Info("[ChatDisplay] Initializing via Zenject...");
            
            // 注册到 ChatManager（临时方案，直到 ChatManager 完全迁移到 Zenject）
            if (ChatManager.instance != null)
            {
                ChatManager.instance.SetChatDisplay(this);
                Logger.Info("[ChatDisplay] Registered to ChatManager");
            }
            else
            {
                Logger.Warn("[ChatDisplay] ChatManager instance not found during initialization");
            }
            
            // 执行异步初始化（参考 v3 的 InitializeAsync）
            await InitializeInternalAsync(cancellationToken);
        }
        
        private async Task InitializeInternalAsync(CancellationToken cancellationToken = default)
        {
            // 等待字体管理器初始化（v3: while (!this._fontManager.IsInitialized)）
            while (_fontManager != null && !_fontManager.IsInitialized && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            // 设置屏幕（v3: this.SetupScreens()）
            this.SetupScreens();
            
            // 刷新现有消息（v3: foreach (var msg in this._messages)）
            foreach (var msg in this._messages.ToArray())
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled)
                {
                    msg.SubText.SetAllDirty();
                }
            }
            
            // 设置 pivot（v3: (this.transform as RectTransform).pivot = new Vector2(0.5f, 0f)）
            (this.transform as RectTransform).pivot = new Vector2(0.5f, 0f);
            
            // 订阅配置变更事件
            _chatConfig.OnConfigChanged += this.Instance_OnConfigChanged;
            
            // 订阅场景变更事件  
            SceneManager.activeSceneChanged += this.SceneManager_activeSceneChanged;
            
            // 等待屏幕创建完成
            while (this._chatScreen == null && !cancellationToken.IsCancellationRequested)
            {
                await Task.Yield();
            }
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            // 处理备份消息队列（v3: while (s_backupMessageQueue.TryDequeue(out var msg))）
            while (_backupMessageQueue.TryDequeue(out var msg))
            {
                await this.OnTextMessageReceived(msg.Value, msg.Key);
            }
            
            _isInitialized = true;
            Logger.Info("[ChatDisplay] Initialization completed");
        }
        
        #region IChatDisplay 接口实现
        
        // 创建并显示聊天消息
        public void CreateMessage(IChatMessage message)
        {
            OnTextMessageReceived(message);
        }
        
        // 创建并显示聊天消息（异步版本）
        public async Task CreateMessageAsync(IChatMessage message)
        {
            await OnTextMessageReceived(message, DateTime.Now);
        }
        
        // 清除指定ID的消息
        public void ClearMessage(string messageId)
        {
            OnMessageCleared(messageId);
        }
        
        // 清除指定用户的所有消息
        public void ClearUserMessages(string userId)
        {
            OnChatCleared(userId);
        }
        
        // 更新浮动屏幕位置（菜单/游戏场景切换时）
        public void UpdateFloatingScreenPosition()
        {
            if (_chatScreen != null)
            {
                _chatScreen.ScreenPosition = _isInGame ? _chatConfig.Song_ChatPosition : _chatConfig.Menu_ChatPosition;
                _chatScreen.ScreenRotation = Quaternion.Euler(_isInGame ? _chatConfig.Song_ChatRotation : _chatConfig.Menu_ChatRotation);
            }
        }
        
        #endregion

        // private void PointerOnEnabled(VRPointer obj)
        // {
        //     try {
        //         var mover = this._chatScreen.gameObject.GetComponent<FloatingScreenMoverPointer>();
        //         if (!mover) {
        //             mover = this._chatScreen.gameObject.AddComponent<FloatingScreenMoverPointer>();
        //             Destroy(this._chatScreen.screenMover);
        //         }
        //         this._chatScreen.screenMover = mover;
        //         this._chatScreen.screenMover.Init(this._chatScreen, obj);
        //     }
        //     catch (Exception e) {
        //         Logger.Error(e);
        //     }
        // }

        // TODO: eventually figure out a way to make this more modular incase we want to create multiple instances of ChatDisplay
        private static readonly ConcurrentQueue<KeyValuePair<DateTime, IChatMessage>> _backupMessageQueue =
            new ConcurrentQueue<KeyValuePair<DateTime, IChatMessage>>();

        protected override void OnDestroy()
        {
            Dispose();
            base.OnDestroy();
        }
        
        // IDisposable 实现（参考 v3）
        private bool _disposedValue = false;
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        // 从 ChatManager 注销
                        if (ChatManager.instance != null && ChatManager.instance._chatDisplay == this)
                        {
                            ChatManager.instance.SetChatDisplay(null);
                            Logger.Info("[ChatDisplay] Unregistered from ChatManager");
                        }
                        
                        // 取消事件订阅
                        if (_chatConfig != null)
                        {
                            _chatConfig.OnConfigChanged -= this.Instance_OnConfigChanged;
                        }
                        SceneManager.activeSceneChanged -= this.SceneManager_activeSceneChanged;
                        
                        // 停止所有协程
                        this.StopAllCoroutines();
                        
                        // 备份消息并清理
                        while (this._messages.TryDequeue(out var msg))
                        {
                            msg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
                            // 移除 receiver 注册
                            msg.Text?.RemoveReceiver(this);
                            msg.SubText?.RemoveReceiver(this);
                            if (msg.Text.ChatMessage != null)
                            {
                                _backupMessageQueue.Enqueue(
                                    new KeyValuePair<DateTime, IChatMessage>(msg.ReceivedDate, msg.Text.ChatMessage));
                            }
                            if (msg.SubText.ChatMessage != null)
                            {
                                _backupMessageQueue.Enqueue(
                                    new KeyValuePair<DateTime, IChatMessage>(msg.ReceivedDate, msg.SubText.ChatMessage));
                            }
                            
                            // 使用池回收而不是销毁
                            if (_textPoolContainer != null)
                            {
                                _textPoolContainer.Despawn(msg);
                            }
                            else
                            {
                                Destroy(msg);
                            }
                        }
                        
                        // 清理资源
                        if (this._rootGameObject != null)
                        {
                            Destroy(this._rootGameObject);
                        }
                        
                        if (this._chatScreen != null)
                        {
                            this._chatScreen.HandleReleased -= OnHandleReleased;
                            Destroy(this._chatScreen);
                            this._chatScreen = null;
                        }
                        
                        if (this._bg != null && this._bg.material != null)
                        {
                            Destroy(this._bg.material);
                        }
                        
                        if (this._chatMoverMaterial != null)
                        {
                            Destroy(this._chatMoverMaterial);
                            this._chatMoverMaterial = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error during ChatDisplay disposal: {ex}");
                    }
                }
                
                _disposedValue = true;
                _instance = null; // 清除单例引用
            }
        }
        
        public void LatePreRenderRebuildHandler(object sender, EventArgs e)
        {
            _updateMessagePositions = true;
        }
        
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void Update()
        {
            try
            {
                // 检查是否需要更新消息位置
                if (!this._updateMessagePositions)
                {
                    return;
                }
                
                // 检查是否已初始化
                if (!_isInitialized || _chatScreen == null || _chatContainer == null)
                {
                    Logger.Debug("[Update] Chat display not fully initialized, skipping position update");
                    return;
                }
                
                // 避免在销毁过程中更新
                if (_disposedValue)
                {
                    Logger.Debug("[Update] Chat display is disposed, skipping position update");
                    return;
                }

                // 使用协程延迟到帧末尾，确保所有布局计算完成
                StartCoroutine(UpdateMessagePositionsCoroutine());
                this._updateMessagePositions = false;
            }
            catch (Exception ex)
            {
                Logger.Error($"[Update] Error in update loop: {ex}");
                this._updateMessagePositions = false;
            }
        }
        
        private IEnumerator UpdateMessagePositionsCoroutine()
        {
            // 等待帧结束，确保所有布局更新完成
            yield return _waitForEndOfFrame;
            
            // 再次检查状态
            if (!_disposedValue && _isInitialized)
            {
                UpdateMessagePositions();
            }
        }

        private FloatingScreen _chatScreen;
        private GameObject _chatContainer;
        private GameObject _rootGameObject;
        private Material _chatMoverMaterial;
        private ImageView _bg;
        private static readonly string s_menu = "MainMenu";
        private static readonly string s_game = "GameCore";

        // Start 方法保留为空，遵循 v3 设计（所有初始化在 Initialize 中）
        private void Start()
        {
        }

        private void SetupScreens()
        {
            // Logger.Debug($"SetupScreens, _chatScreen: {this._chatScreen}");
            if (this._chatScreen == null)
            {
                var screenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                // Logger.Debug($"Creating FloatingScreen, screenSize: {screenSize}");
                this._chatScreen = FloatingScreen.CreateFloatingScreen(screenSize, true, this.ChatPosition,
                    Quaternion.identity, 0f, true);
                // Logger.Debug($"FloatingScreen created, _chatScreen: {this._chatScreen}");
                this._chatScreen.gameObject.layer = 5;
                // Logger.Debug($"Setting up FloatingScreen");
                this._chatScreen.HandleReleased += OnHandleReleased;
                // Logger.Debug($"HandleReleased event added");
                var rectMask2D = this._chatScreen.GetComponent<RectMask2D>();
                if (rectMask2D)
                {
                    Destroy(rectMask2D);
                }

                // Logger.Debug($"Creating chatContainer");
                this._chatContainer = new GameObject("chatContainer");
                this._chatContainer.transform.SetParent(this._chatScreen.transform, false);
                var rectMask = this._chatContainer.AddComponent<RectMask2D>();
                rectMask.rectTransform.sizeDelta = screenSize;
                // RectMask2D 本身不会阻挡射线检测
                // Logger.Debug($"chatContainer created");

                var canvas = this._chatScreen.GetComponent<Canvas>();
                canvas.worldCamera = Camera.main;
                canvas.sortingOrder = 3;
                // Logger.Debug($"Setting up chatScreen");

                this._chatScreen.SetRootViewController(this, AnimationType.None);
                this._rootGameObject = new GameObject();
                DontDestroyOnLoad(this._rootGameObject);
                // Logger.Debug($"Creating chatMoverMaterial");

                this._chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlowMaterial);
                this._chatMoverMaterial.color = Color.clear;
                // Logger.Debug($"chatMoverMaterial created");

                var handleField = typeof(FloatingScreen).GetField("handle", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var handle = handleField.GetValue(this._chatScreen) as GameObject;
                var renderer = handle.GetComponent<Renderer>();
                renderer.material = this._chatMoverMaterial;
                renderer.material.mainTexture = this._chatMoverMaterial.mainTexture;
                // Logger.Debug($"Setting up chatScreen handle");

                this._chatScreen.transform.SetParent(this._rootGameObject.transform);
                this._chatScreen.ScreenRotation = Quaternion.Euler(this.ChatRotation);
                // Logger.Debug($"Setting up chatScreen rotation");

                // this._bg = this._chatScreen.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "bg");
                // this._bg = this._chatScreen.GetComponentsInChildren<ImageView>()
                //     .FirstOrDefault(x => x.name == "Background");
                this._bg = this._chatScreen.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Background");
                this._bg.raycastTarget = false;
                this._bg.material = Instantiate(this._bg.material);
                this._bg.SetField("_gradient", false);
                this._bg.material.color = Color.white.ColorWithAlpha(1);
                this._bg.color = this.BackgroundColor;
                this._bg.SetAllDirty();

                // this.AddToVRPointer();
                this.UpdateChatUI();
            }
        }

        private void Instance_OnConfigChanged(ChatConfig obj) => this.UpdateChatUI();

        private void OnHandleReleased(object sender, FloatingScreenHandleEventArgs e) =>
            this.FloatingScreenOnRelease(e.Position, e.Rotation);

        private void FloatingScreenOnRelease(in Vector3 pos, in Quaternion rot)
        {
            if (this._isInGame)
            {
                this._chatConfig.Song_ChatPosition = pos;
                this._chatConfig.Song_ChatRotation = rot.eulerAngles;
            }
            else
            {
                this._chatConfig.Menu_ChatPosition = pos;
                this._chatConfig.Menu_ChatRotation = rot.eulerAngles;
            }

            this._chatConfig.Save();
        }

        private void BSEvents_gameSceneActive()
        {
            this._isInGame = true;
            foreach (var canvas in this._chatScreen.GetComponentsInChildren<Canvas>(true))
            {
                canvas.sortingOrder = 0;
            }

            // this.AddToVRPointer();
            this.UpdateChatUI();
        }

        private void BSEvents_menuSceneActive()
        {
            this._isInGame = false;
            foreach (var canvas in this._chatScreen.GetComponentsInChildren<Canvas>(true))
            {
                canvas.sortingOrder = 3;
            }

            // this.AddToVRPointer();
            this.UpdateChatUI();
        }

        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            if (arg1.name != s_game && arg1.name != s_menu)
            {
                this._isInGame = false;
                this._rootGameObject.SetActive(false);
                return;
            }

            this._rootGameObject.SetActive(true);
            if (arg1.name == s_game)
            {
                this._isInGame = true;
                foreach (var canvas in this._chatScreen.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.sortingOrder = 0;
                }
            }
            else if (arg1.name == s_menu)
            {
                this._isInGame = false;
                foreach (var canvas in this._chatScreen.GetComponentsInChildren<Canvas>(true))
                {
                    canvas.sortingOrder = 3;
                }
            }

            this.UpdateChatUI();
        }

        // private void AddToVRPointer()
        // {
        //     if (this._chatScreen.screenMover) {
        //         this._chatScreen.HandleReleased -= this.OnHandleReleased;
        //         this._chatScreen.HandleReleased += this.OnHandleReleased;
        //         this._chatScreen.screenMover.transform.SetAsFirstSibling();
        //     }
        // }

        private WaitForEndOfFrame _waitForEndOfFrame;


        private void UpdateMessagePositions()
        {
            try
            {
                // 确保在主线程执行
                if (System.Threading.Thread.CurrentThread.ManagedThreadId != 1)
                {
                    Logger.Warn("[UpdateMessagePositions] Called from non-main thread, rescheduling");
                    MainThreadInvoker.Invoke(() => UpdateMessagePositions());
                    return;
                }
                
                // 检查是否正在更新布局
                if (_isUpdatingLayout)
                {
                    Logger.Debug("[UpdateMessagePositions] Layout is already updating, skipping");
                    return;
                }
                
                // 检查关键组件
                if (_messages == null || _chatContainer == null)
                {
                    Logger.Error("[UpdateMessagePositions] Messages queue or chat container is null");
                    return;
                }
                
                Logger.Debug($"[UpdateMessagePositions] Processing {this._messages.Count} messages");
                
                float? msgPos = this.ChatHeight / (this.ReverseChatOrder ? 2f : -2f);
                var messagesArray = this._messages.OrderBy(x => x.ReceivedDate).Reverse().ToArray();
                
                foreach (var chatMsg in messagesArray)
                {
                    try
                    {
                        // 增强的空值检查
                        if (chatMsg == null)
                        {
                            Logger.Debug("[UpdateMessagePositions] Skipping null message");
                            continue;
                        }
                        
                        if (chatMsg.gameObject == null || chatMsg.transform == null)
                        {
                            Logger.Debug("[UpdateMessagePositions] Skipping message with invalid GameObject or Transform");
                            continue;
                        }
                        
                        // 检查消息是否仍在容器中
                        if (chatMsg.transform.parent == null || chatMsg.transform.parent != _chatContainer.transform)
                        {
                            Logger.Debug("[UpdateMessagePositions] Skipping message not in chat container");
                            continue;
                        }
                        
                        var rectTransform = chatMsg.transform as RectTransform;
                        if (rectTransform == null)
                        {
                            Logger.Warn("[UpdateMessagePositions] Failed to get RectTransform for message");
                            continue;
                        }
                        
                        var msgHeight = rectTransform.sizeDelta.y;
                        
                        if (this.ReverseChatOrder)
                        {
                            msgPos -= msgHeight;
                        }

                        chatMsg.transform.localPosition = new Vector3(0, msgPos ?? 0);
                        
                        if (!this.ReverseChatOrder)
                        {
                            msgPos += msgHeight;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[UpdateMessagePositions] Error positioning individual message: {ex.Message}");
                    }
                }
                
                Logger.Debug("[UpdateMessagePositions] Complete");
            }
            catch (Exception ex)
            {
                Logger.Error($"[UpdateMessagePositions] Unexpected error: {ex}");
            }
        }

        private void OnRenderRebuildComplete()
        {
            // 避免在销毁或未初始化状态下触发更新
            if (!_disposedValue && _isInitialized && !_isUpdatingLayout)
            {
                this._updateMessagePositions = true;
            }
        }

        public void AddMessage(EnhancedTextMeshProUGUIWithBackground newMsg)
        {
            try
            {
                // 验证输入
                if (newMsg == null)
                {
                    Logger.Error("[AddMessage] Attempted to add null message");
                    return;
                }
                
                if (newMsg.gameObject == null)
                {
                    Logger.Error("[AddMessage] Message has null gameObject");
                    return;
                }
                
                Logger.Debug($"[AddMessage] Adding new message to display, current count: {_messages.Count}");
                
                // 安全地移除和添加事件监听器
                newMsg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
                newMsg.OnLatePreRenderRebuildComplete += this.OnRenderRebuildComplete;
                
                // 注册为 receiver 以接收 rebuild 事件
                if (newMsg is ILatePreRenderRebuildReceiver receiver)
                {
                    newMsg.Text?.AddReceiver(this);
                    newMsg.SubText?.AddReceiver(this);
                }
                
                // 更新消息样式
                this.UpdateMessage(newMsg, true);
                
                // 添加到队列
                this._messages.Enqueue(newMsg);
                Logger.Debug($"[AddMessage] Message added, new count: {_messages.Count}");
                
                // 清理旧消息
                this.ClearOldMessages();
            }
            catch (Exception ex)
            {
                Logger.Error($"[AddMessage] Unexpected error: {ex}");
            }
        }

        private void UpdateChatUI()
        {
            // Logger.Debug("UpdateChatUI");
            this.ChatWidth = this._chatConfig.ChatWidth;
            this.ChatHeight = this._chatConfig.ChatHeight;
            this.FontSize = this._chatConfig.FontSize;
            // Logger.Debug($"ChatWidth: {this.ChatWidth}, ChatHeight: {this.ChatHeight}, FontSize: {this.FontSize}");
            this.AccentColor = this._chatConfig.AccentColor;
            this.HighlightColor = this._chatConfig.HighlightColor;
            this.BackgroundColor = this._chatConfig.BackgroundColor;
            // Logger.Debug(
            //     $"AccentColor: {this.AccentColor}, HighlightColor: {this.HighlightColor}, BackgroundColor: {this.BackgroundColor}");
            this.PingColor = this._chatConfig.PingColor;
            this.TextColor = this._chatConfig.TextColor;
            this.ReverseChatOrder = this._chatConfig.ReverseChatOrder;
            // Logger.Debug(
            //     $"PingColor: {this.PingColor}, TextColor: {this.TextColor}, ReverseChatOrder: {this.ReverseChatOrder}");
            if (this._isInGame)
            {
                this.ChatPosition = this._chatConfig.Song_ChatPosition;
                this.ChatRotation = this._chatConfig.Song_ChatRotation;
                this.gameObject.layer = this._chatConfig.Song_ChatLayer;
            }
            else
            {
                this.ChatPosition = this._chatConfig.Menu_ChatPosition;
                this.ChatRotation = this._chatConfig.Menu_ChatRotation;
                this.gameObject.layer = this._chatConfig.Menu_ChatLayer;
            }

            // Logger.Debug(
            //     $"ChatPosition: {this.ChatPosition}, ChatRotation: {this.ChatRotation}, gameObject.layer: {this.gameObject.layer}");
            var chatContainerTransform = this._chatContainer.GetComponent<RectMask2D>().rectTransform!;
            // Logger.Debug($"chatContainerTransform: {chatContainerTransform}");
            chatContainerTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
            // Logger.Debug($"chatContainerTransform.sizeDelta: {chatContainerTransform.sizeDelta}");

            var handleField = typeof(FloatingScreen).GetField("handle", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var handle = handleField.GetValue(this._chatScreen) as GameObject;
            handle.transform.localScale = new Vector3(this.ChatWidth, this.ChatHeight * 0.9f, 0.01f);
            handle.transform.localPosition = Vector3.zero;
            handle.transform.localRotation = Quaternion.identity;
            // Logger.Debug(
            //     $"handle.transform.localScale: {handle.transform.localScale}, handle.transform.localPosition: {handle.transform.localPosition}");

            this.AllowMovement = this._chatConfig.AllowMovement;
            // Logger.Debug($"AllowMovement: {this.AllowMovement}");
            
            // 确保拖动手柄在最上层
            if (this._chatScreen != null)
            {
                var handleFieldInfo = typeof(FloatingScreen).GetField("handle", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var handleObject = handleFieldInfo?.GetValue(this._chatScreen) as GameObject;
                if (handleObject != null)
                {
                    handleObject.transform.SetAsLastSibling();
                }
            }
            
            this.UpdateMessages();
            // Logger.Debug("UpdateChatUI complete");
        }

        private void UpdateMessages()
        {
            // 防止在布局更新期间修改消息
            if (_isUpdatingLayout)
            {
                Logger.Debug("[UpdateMessages] Already updating layout, skipping");
                return;
            }
            
            // 确保在主线程执行
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != 1)
            {
                Logger.Warn("[UpdateMessages] Called from non-main thread, scheduling on main thread");
                MainThreadInvoker.Invoke(() => UpdateMessages());
                return;
            }
            
            _isUpdatingLayout = true;
            try
            {
                // 检查关键组件是否存在
                if (_chatContainer == null || _chatContainer.gameObject == null)
                {
                    Logger.Error("[UpdateMessages] Chat container is null or destroyed");
                    return;
                }
                
                // 创建消息数组副本，避免在迭代时集合被修改
                var messages = this._messages.ToArray();
                Logger.Debug($"[UpdateMessages] Processing {messages.Length} messages");
                
                foreach (var msg in messages)
                {
                    try
                    {
                        // 增强的空值和有效性检查
                        if (msg == null)
                        {
                            Logger.Debug("[UpdateMessages] Skipping null message");
                            continue;
                        }
                        
                        if (msg.gameObject == null)
                        {
                            Logger.Debug("[UpdateMessages] Skipping message with null gameObject");
                            continue;
                        }
                        
                        // 检查组件是否被销毁
                        if (msg.transform == null || msg.transform.parent == null)
                        {
                            Logger.Debug("[UpdateMessages] Skipping message with invalid transform");
                            continue;
                        }
                        
                        if (!msg.gameObject.activeInHierarchy)
                        {
                            Logger.Debug("[UpdateMessages] Skipping inactive message");
                            continue;
                        }
                        
                        this.UpdateMessage(msg, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"[UpdateMessages] Error updating individual message: {ex.Message}");
                    }
                }
                this._updateMessagePositions = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[UpdateMessages] Unexpected error: {ex}");
            }
            finally
            {
                _isUpdatingLayout = false;
            }
        }

        private void UpdateMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            // Logger.Debug("UpdateMessage");
            try
            {
                // 验证消息对象
                if (msg == null || msg.gameObject == null || msg.transform == null)
                {
                    Logger.Warn("[UpdateMessage] Message object is invalid");
                    return;
                }
                
                // 验证文本组件
                if (msg.Text == null || msg.SubText == null)
                {
                    Logger.Warn("[UpdateMessage] Text components are null");
                    return;
                }
                
                // 安全地获取 RectTransform
                var rectTransform = msg.transform as RectTransform;
                if (rectTransform == null)
                {
                    Logger.Error("[UpdateMessage] Failed to get RectTransform");
                    return;
                }
                
                rectTransform.sizeDelta = new Vector2(this.ChatWidth, rectTransform.sizeDelta.y);
                
                // 验证字体管理器
                if (ESCFontManager.instance == null || ESCFontManager.instance.MainFont == null)
                {
                    Logger.Error("[UpdateMessage] Font manager or main font is null");
                    return;
                }
                
                msg.Text.font = ESCFontManager.instance.MainFont;
                msg.Text.font.fallbackFontAssetTable = ESCFontManager.instance.FallBackFonts;
                msg.Text.overflowMode = TextOverflowModes.Overflow;
                msg.Text.alignment = TextAlignmentOptions.BottomLeft;
                msg.Text.color = this.TextColor;
                msg.Text.fontSize = this.FontSize;
                msg.Text.lineSpacing = 1.5f;
            // Logger.Debug("UpdateMessage: Text complete");
            // Logger.Debug(
            //     $"UpdateMessage: Text: {msg.Text.text}, ChatMessage: {msg.Text.ChatMessage}, font: {msg.Text.font}, color: {msg.Text.color}, fontSize: {msg.Text.fontSize}, lineSpacing: {msg.Text.lineSpacing}");
            // if (msg.Text.ChatMessage != null)
            // {
            //     Logger.Debug($"UpdateMessage: Text.ChatMessage: {msg.Text.ChatMessage.Message}");
            // }

                // Logger.Debug("UpdateMessage: SubText");
                msg.SubText.font = ESCFontManager.instance.MainFont;
                msg.SubText.font.fallbackFontAssetTable = ESCFontManager.instance.FallBackFonts;
                msg.SubText.overflowMode = TextOverflowModes.Overflow;
                msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
                msg.SubText.color = this.TextColor;
                msg.SubText.fontSize = this.FontSize;
                msg.SubText.lineSpacing = 1.5f;
                // Logger.Debug("UpdateMessage: SubText complete");

                if (msg.Text.ChatMessage != null)
                {
                    msg.HighlightColor = msg.Text.ChatMessage.IsPing ? this.PingColor : this.HighlightColor;
                    msg.AccentColor = this.AccentColor;
                    msg.HighlightEnabled = msg.Text.ChatMessage.IsHighlighted || msg.Text.ChatMessage.IsPing;
                    msg.AccentEnabled = !msg.Text.ChatMessage.IsPing &&
                                        (msg.HighlightEnabled || msg.SubText.ChatMessage != null);
                }

                // Logger.Debug("UpdateMessage: SetAllDirty");
                if (setAllDirty)
                {
                    // 在调用 SetAllDirty 前检查组件状态
                    if (msg.Text != null && msg.Text.gameObject != null && msg.Text.enabled)
                    {
                        msg.Text.SetAllDirty();
                    }
                    
                    if (msg.SubTextEnabled && msg.SubText != null && msg.SubText.gameObject != null && msg.SubText.enabled)
                    {
                        msg.SubText.SetAllDirty();
                    }
                }

                if (msg.Text.ChatMessage is BilibiliChatMessage)
                {
                    // Logger.Debug($"[UpdateMessage] is BilibiliChatMessage");
                }
                
                // 强制刷新文本信息 - 添加安全检查
                if (setAllDirty)
                {
                    try
                    {
                        if (msg.Text != null && msg.Text.gameObject != null && msg.Text.enabled)
                        {
                            msg.Text.ForceMeshUpdate();
                        }
                        
                        if (msg.SubTextEnabled && msg.SubText != null && msg.SubText.gameObject != null && msg.SubText.enabled)
                        {
                            msg.SubText.ForceMeshUpdate();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[UpdateMessage] Failed to force mesh update: {ex.Message}");
                    }
                }
                
                if (msg.Text != null && msg.Text.textInfo != null)
                {
                    // Logger.Debug($"[UpdateMessage] After update - characterCount: {msg.Text.textInfo.characterCount}, text: {msg.Text.text}");
                }

                // Logger.Debug("UpdateMessage complete");
            }
            catch (Exception ex)
            {
                Logger.Error($"[UpdateMessage] Unexpected error: {ex}");
            }
        }

        private bool UpdateMessageContent(string id, string content)
        {
            var flag = false;
            foreach (var msg in this._messages.ToArray())
            {
                if (msg.Text.ChatMessage is BilibiliChatMessage && msg.Text.ChatMessage.Id == id)
                {
                    // Console.WriteLine("1: Find Msg id: " + id + "Content: " + msg.Text.ChatMessage.Message + " --> " + content);
                    ((BilibiliChatMessage)msg.Text.ChatMessage).UpdateContent(content);
                    this.UpdateMessage(msg, true);
                    flag = true;
                    break;
                }
            }

            if (flag)
            {
                Console.WriteLine("Update Message");
                this._updateMessagePositions = true;
            }

            return flag;
        }

        private bool UpdateMessageContent2(string id, string content)
        {
            var flag = false;
            foreach (var msg in this._messages.ToArray())
            {
                if (msg.Text.ChatMessage is BilibiliChatMessage && msg.Text.ChatMessage.Id == id)
                {
                    // Console.WriteLine("2: Find Msg id: " + id + "Content: " + msg.Text.ChatMessage.Message + " --> " + content);
                    ((BilibiliChatMessage)msg.Text.ChatMessage).UpdateContent(content);
                    msg.SubText.text = content;
                    msg.SubText.ChatMessage = msg.Text.ChatMessage;
                    msg.SubTextEnabled = true;
                    this.UpdateMessage(msg, true);
                    flag = true;
                    break;
                }
            }

            return flag;
        }

        private void ClearOldMessages()
        {
            // 延迟清理以避免在布局更新期间删除对象
            MainThreadInvoker.Invoke(() =>
            {
                try
                {
                    // 检查是否正在更新布局
                    if (_isUpdatingLayout)
                    {
                        Logger.Debug("[ClearOldMessages] Layout is updating, postponing cleanup");
                        return;
                    }
                    
                    // 检查关键组件
                    if (_messages == null || _textPoolContainer == null)
                    {
                        Logger.Error("[ClearOldMessages] Messages queue or pool container is null");
                        return;
                    }
                    
                    if (ChatConfig.instance == null)
                    {
                        Logger.Error("[ClearOldMessages] ChatConfig instance is null");
                        return;
                    }
                    
                    var cleanedCount = 0;
                    var maxCleanupPerFrame = 5; // 限制每帧清理的消息数量，避免性能问题
                    
                    while (cleanedCount < maxCleanupPerFrame && this._messages.TryPeek(out var msg))
                    {
                        try
                        {
                            // 增强的空值检查
                            if (msg == null)
                            {
                                Logger.Debug("[ClearOldMessages] Null message found, removing from queue");
                                this._messages.TryDequeue(out _);
                                continue;
                            }
                            
                            if (msg.gameObject == null)
                            {
                                Logger.Debug("[ClearOldMessages] Message with null gameObject found, removing from queue");
                                this._messages.TryDequeue(out _);
                                continue;
                            }
                            
                            if (msg.transform == null)
                            {
                                Logger.Debug("[ClearOldMessages] Message with null transform found, removing from queue");
                                this._messages.TryDequeue(out _);
                                continue;
                            }
                            
                            var rectTransform = msg.transform as RectTransform;
                            if (rectTransform == null)
                            {
                                Logger.Warn("[ClearOldMessages] Failed to get RectTransform");
                                this._messages.TryDequeue(out _);
                                continue;
                            }
                            
                            // 检查消息是否超出可见范围
                            var shouldRemove = this.ReverseChatOrder
                                ? msg.transform.localPosition.y < 0 - rectTransform.sizeDelta.y
                                : msg.transform.localPosition.y >= ChatConfig.instance.ChatHeight;
                            
                            if (shouldRemove)
                            {
                                if (this._messages.TryDequeue(out msg))
                                {
                                    // 再次验证对象有效性
                                    if (msg != null && msg.gameObject != null)
                                    {
                                        Logger.Debug($"[ClearOldMessages] Despawning message at position {msg.transform.localPosition.y}");
                                        _textPoolContainer.Despawn(msg);
                                        cleanedCount++;
                                    }
                                }
                            }
                            else
                            {
                                // 消息仍在可见范围内，停止清理
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[ClearOldMessages] Error processing individual message: {ex.Message}");
                            // 尝试移除有问题的消息
                            this._messages.TryDequeue(out _);
                        }
                    }
                    
                    if (cleanedCount > 0)
                    {
                        Logger.Debug($"[ClearOldMessages] Cleaned {cleanedCount} messages");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ClearOldMessages] Unexpected error: {ex}");
                }
            });
        }

        private string BuildClearedMessage(EnhancedTextMeshProUGUI msg)
        {
            var nameColorCode = msg.ChatMessage.Sender.Color;
            if (ColorUtility.TryParseHtmlString(msg.ChatMessage.Sender.Color.Substring(0, 7), out var nameColor))
            {
                Color.RGBToHSV(nameColor, out var h, out var s, out var v);
                if (v < 0.85f)
                {
                    v = 0.85f;
                    nameColor = Color.HSVToRGB(h, s, v);
                }

                nameColorCode = ColorUtility.ToHtmlStringRGB(nameColor);
                nameColorCode = nameColorCode.Insert(0, "#");
            }

            var sb = new StringBuilder($"<color={nameColorCode}>{msg.ChatMessage.Sender.DisplayName}</color>");
            var badgeEndIndex = msg.text.IndexOf("<color=");
            if (badgeEndIndex != -1)
            {
                sb.Insert(0, msg.text.Substring(0, badgeEndIndex));
            }

            sb.Append(": <color=#bbbbbbbb><message deleted></color>");
            return sb.ToString();
        }

        private void ClearMessage(EnhancedTextMeshProUGUIWithBackground msg)
        {
            // Only clear non-system messages
            if (!msg.Text.ChatMessage.IsSystemMessage)
            {
                msg.Text.text = this.BuildClearedMessage(msg.Text);
                msg.SubTextEnabled = false;
            }

            if (msg.SubText.ChatMessage != null && !msg.SubText.ChatMessage.IsSystemMessage)
            {
                msg.SubText.text = this.BuildClearedMessage(msg.SubText);
            }
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null)
            {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach (var msg in this._messages.ToArray())
                    {
                        if (msg.Text.ChatMessage == null)
                        {
                            continue;
                        }

                        if (msg.Text.ChatMessage.Id == messageId)
                        {
                            this.ClearMessage(msg);
                        }
                    }
                });
            }
        }

        public void OnChatCleared(string userId) => MainThreadInvoker.Invoke(() =>
        {
            foreach (var msg in this._messages.ToArray())
            {
                if (msg.Text.ChatMessage == null)
                {
                    continue;
                }

                if (userId == null || msg.Text.ChatMessage.Sender.Id == userId)
                {
                    this.ClearMessage(msg);
                }
            }
        });

        public void OnJoinChannel(IChatService svc, IChatChannel channel) => MainThreadInvoker.Invoke(() =>
        {
            var newMsg = _textPoolContainer.Spawn();
            newMsg.Text.text = $"<color=#bbbbbbbb>[{svc.DisplayName}] Success joining {channel.Id}</color>";
            newMsg.HighlightEnabled = true;
            newMsg.HighlightColor = Color.gray.ColorWithAlpha(0.05f);
            this.AddMessage(newMsg);
        });

        public void OnChannelResourceDataCached(IChatChannel channel,
            Dictionary<string, IChatResourceData> resources) => MainThreadInvoker.Invoke(() =>
        {
            var count = 0;
            if (this._chatConfig.PreCacheAnimatedEmotes)
            {
                foreach (var emote in resources)
                {
                    if (emote.Value.IsAnimated)
                    {
                        // 没有HMMainThreadDispatcher了
                        // HMMainThreadDispatcher.instance.Enqueue(ChatImageProvider.instance.PrecacheAnimatedImage(emote.Value.Uri, emote.Key, 110));
                        this.StartCoroutine(
                            ChatImageProvider.instance.PrecacheAnimatedImage(emote.Value.Uri, emote.Key, 110));
                        count++;
                    }
                }

                Logger.Debug($"Pre-cached {count} animated emotes.");
            }
            else
            {
                Logger.Warn(
                    "Pre-caching of animated emotes disabled by the user. If you're experiencing lag, re-enable emote precaching.");
            }
        });

        // TODO: 移除 _lastMessage - 新的消息处理逻辑不再需要跟踪最后一条消息
        // 保留此字段仅为了向后兼容的 CreateMessage 方法
        private EnhancedTextMeshProUGUIWithBackground _lastMessage;

        // public void OnTextMessageReceived(IChatMessage msg) => _ = this.OnTextMessageReceived(msg, DateTime.Now);
        public void OnTextMessageReceived(IChatMessage msg)
        {
            Logger.Info($"Received message: {msg.Message}");
            _ = this.OnTextMessageReceived(msg, DateTime.Now);
            // Logger.Debug($"OnTextMessageReceived: {msg.Message}");
        }

        public async Task OnTextMessageReceived(IChatMessage msg, DateTime dateTime)
        {
            // Logger.Debug(
            //     $"Received message: msg.Id: {msg.Id}, msg.IsSystemMessage: {msg.IsSystemMessage}, msg.IsActionMessage: {msg.IsActionMessage}, msg.IsHighlighted: {msg.IsHighlighted}, msg.IsPing: {msg.IsPing}, msg.Message: {msg.Message}, msg.Sender: {msg.Sender}, msg.Channel: {msg.Channel}, msg.Emotes: {msg.Emotes}, msg.Metadata: {msg.Metadata}");
            
            // 在构建消息之前先准备图片资源（包括表情和徽章）
            if (!ChatMessageBuilder.PrepareImages(msg, ESCFontManager.instance.FontInfo))
            {
                Logger.Warn($"Failed to prepare some/all images for msg \"{msg.Message}\"!");
            }
            
            // 分别构建主消息和子消息（参考v3的实现）
            var mainMessage = await ChatMessageBuilder.BuildMessage(msg, ESCFontManager.instance.FontInfo, BuildMessageTarget.Main);
            var subMessage = await ChatMessageBuilder.BuildMessage(msg, ESCFontManager.instance.FontInfo, BuildMessageTarget.Sub);
            
            // Logger.Debug($"Build message end - main: {mainMessage}, sub: {subMessage}");
            if (_textPoolContainer == null)
            {
                Logger.Warn("_textPoolContainer is null, waiting for it to be initialized.");
            }

            while (_textPoolContainer == null)
            {
                await Task.Delay(100);
            }

            // Logger.Debug($"Create message coroutine: main={mainMessage}, sub={subMessage}");
            // 在主线程上创建消息，使用 await 确保消息按顺序创建
            await MainThreadInvoker.InvokeAsync(() => this.CreateMessage(msg, dateTime, mainMessage, subMessage));
            // Logger.Debug($"Create message coroutine end");
        }


        /// <summary>
        /// 创建消息（支持主消息和子消息）
        /// </summary>
        private void CreateMessage(IChatMessage msg, DateTime date, string mainMessage, string subMessage)
        {
            try
            {
                Logger.Debug($"[CreateMessage] Start - Main: {mainMessage?.Length ?? 0} chars, Sub: {subMessage?.Length ?? 0} chars, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                
                // 验证关键组件
                if (_textPoolContainer == null)
                {
                    Logger.Error("[CreateMessage] Text pool container is null");
                    return;
                }
                
                if (_chatContainer == null || _chatContainer.transform == null)
                {
                    Logger.Error("[CreateMessage] Chat container is null or invalid");
                    return;
                }
                
                var newMsg = _textPoolContainer.Spawn();
                if (newMsg == null)
                {
                    Logger.Error("[CreateMessage] Failed to spawn message from pool");
                    return;
                }
                
                newMsg.transform.SetParent(this._chatContainer.transform, false);
                newMsg.gameObject.SetActive(true);
                
                // 验证字体管理器
                if (ESCFontManager.instance == null || ESCFontManager.instance.MainFont == null)
                {
                    Logger.Error("[CreateMessage] Font manager or main font is null");
                    return;
                }
                
                Logger.Debug($"[CreateMessage] Setting font - Main font: {ESCFontManager.instance.MainFont?.name}");
                newMsg.Text.font = ESCFontManager.instance.MainFont;
                newMsg.Text.ChatMessage = msg;
                newMsg.Text.text = mainMessage;
                
                // 直接设置子消息，不依赖 _lastMessage
                if (!string.IsNullOrEmpty(subMessage))
                {
                    newMsg.SubText.text = subMessage;
                    newMsg.SubText.ChatMessage = msg;
                    newMsg.SubTextEnabled = true;
                }
                else
                {
                    newMsg.SubTextEnabled = false;
                }
                
                newMsg.ReceivedDate = date;
                
                // 输出textinfo信息以调试
                Logger.Debug($"[CreateMessage] Before AddMessage - TextInfo characterCount: {newMsg.Text.textInfo?.characterCount ?? -1}");
                Logger.Debug($"[CreateMessage] Text content length: {newMsg.Text.text?.Length ?? 0}");
                
                // 添加消息到显示列表
                this.AddMessage(newMsg);
                
                // 再次检查characterCount
                Logger.Debug($"[CreateMessage] After AddMessage - TextInfo characterCount: {newMsg.Text.textInfo?.characterCount ?? -1}");
                
                Logger.Debug($"[CreateMessage] Message creation completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"[CreateMessage] Unexpected error: {ex}");
            }
        }
        
        /// <summary>
        /// 创建消息（向后兼容的方法）
        /// </summary>
        private void CreateMessage(IChatMessage msg, DateTime date, string parsedMessage)
        {
            Logger.Debug($"[CreateMessage] Start - Message: {parsedMessage}, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            
            if (this._lastMessage != null && !msg.IsSystemMessage && this._lastMessage.Text.ChatMessage.Id == msg.Id)
            {
                // If the last message received had the same id and isn't a system message, then this was a sub-message of the original and may need to be highlighted along with the original message
                this._lastMessage.SubText.text = parsedMessage;
                this._lastMessage.SubText.ChatMessage = msg;
                this._lastMessage.SubTextEnabled = true;
                Logger.Debug($"[CreateMessage] Sub message added: {parsedMessage}");
                this.UpdateMessage(this._lastMessage, true);
            }
            else
            {
                Logger.Debug($"[CreateMessage] Creating new message");
                var newMsg = _textPoolContainer.Spawn();
                newMsg.transform.SetParent(this._chatContainer.transform, false);
                newMsg.gameObject.SetActive(true);
                
                // Logger.Debug($"[CreateMessage] Setting font - Main font: {ESCFontManager.instance.MainFont?.name}");
                newMsg.Text.font = ESCFontManager.instance.MainFont;
                newMsg.Text.ChatMessage = msg;
                newMsg.Text.text = parsedMessage;
                // newMsg.Text.SetText(parsedMessage);
                // try
                // {
                //     newMsg.Text.SetText(parsedMessage);
                //     try
                //     {
                //         var fieldInfo = typeof(TextMeshProUGUI).GetField("m_TextProcessingArray",
                //             BindingFlags.NonPublic | BindingFlags.Instance);
                //         var mTextProcessingArray = fieldInfo.GetValue(newMsg.Text);
                //         Logger.Debug($"m_TextProcessingArray is null: {mTextProcessingArray == null}");
                //         if (mTextProcessingArray != null)
                //         {
                //             Logger.Debug($"m_TextProcessingArray length: {((Array)mTextProcessingArray).Length}");
                //             for (var i = 0; i < ((Array)mTextProcessingArray).Length; i++)
                //             {
                //                 Logger.Debug($"m_TextProcessingArray[{i}]: {((Array)mTextProcessingArray).GetValue(i):X}");
                //                 // Logger.Debug($"m_TextProcessingArray[{i}]: unicode = {((Array)mTextProcessingArray).GetValue(i).unicode:X}, stringIndex = {mTextProcessingArray[i].stringIndex}");
                //             }
                //         }
                //     }
                //     catch (Exception ex)
                //     {
                //         Logger.Error($"Exception while trying to force mesh update. {ex.StackTrace}");
                //     }
                //
                // } catch (Exception e)
                // {
                //     Logger.Error($"Error setting text: {e}");
                // }

                // try
                // {
                //     // 用反射获取m_fontAsset是否为空
                //     var fieldInfo = typeof(TextMeshProUGUI).GetField("m_fontAsset",
                //         BindingFlags.NonPublic | BindingFlags.Instance);
                //     var mFontAsset = fieldInfo.GetValue(newMsg.Text);
                //     Logger.Debug($"m_fontAsset is null: {mFontAsset == null}");
                //     if (mFontAsset != null)
                //     {
                //         Logger.Debug($"m_fontAsset name: {mFontAsset.GetType().Name}");
                //     }
                //     _ = newMsg.Text.GetTextInfo(parsedMessage);
                // }
                // catch (NullReferenceException ex)
                // {
                //     Logger.Error($"Error message: {ex.Message}");
                //     Logger.Error($"NullReferenceException in SetArraySizes: {ex.StackTrace}");
                //     if (ex.InnerException != null)
                //     {
                //         Logger.Error($"Inner exception: {ex.InnerException.Message}");
                //         Logger.Error($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                //     }
                //     Logger.Error($"Exception source: {ex.Source}");
                //     Logger.Error($"Exception target site: {ex.TargetSite}");
                //
                // }
                // catch (Exception e)
                // {
                //     Logger.Error($"Error getting text info: {e.StackTrace}");
                // }

                newMsg.ReceivedDate = date;
                
                // 输出textinfo信息以调试
                Logger.Debug($"[CreateMessage] Before AddMessage - TextInfo characterCount: {newMsg.Text.textInfo?.characterCount ?? -1}");
                Logger.Debug($"[CreateMessage] Text content: {newMsg.Text.text}");
                
                // 添加消息到显示列表
                this.AddMessage(newMsg);
                
                // 再次检查characterCount
                Logger.Debug($"[CreateMessage] After AddMessage - TextInfo characterCount: {newMsg.Text.textInfo?.characterCount ?? -1}");
                
                this._lastMessage = newMsg;
                Logger.Debug($"[CreateMessage] Message creation completed");
            }

            // Logger.Debug($"Update message positions: {parsedMessage}");
            this._updateMessagePositions = true;
            // Logger.Debug($"Update message positions end: {parsedMessage}");
        }
    }
}