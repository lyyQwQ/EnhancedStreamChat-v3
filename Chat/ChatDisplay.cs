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
using Color = UnityEngine.Color;

namespace EnhancedStreamChat.Chat
{
    [HotReload]
    public partial class ChatDisplay : BSMLAutomaticViewController
    {
        public ObjectMemoryComponentPool<EnhancedTextMeshProUGUIWithBackground> TextPool { get; internal set; }

        private readonly ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground> _messages =
            new ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground>();

        private ChatConfig _chatConfig;

        private bool _isInGame;

        private void Awake()
        {
            this._waitForEndOfFrame = new WaitForEndOfFrame();
            DontDestroyOnLoad(this.gameObject);
            Logger.Info("ChatDisplay Awake");
            // StartCoroutine(Start());
            // VRPointerOnEnablePatch.OnEnabled += this.PointerOnEnabled;
        }

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
            ChatConfig.instance.OnConfigChanged -= this.Instance_OnConfigChanged;
            BSEvents.menuSceneActive -= this.BSEvents_menuSceneActive;
            BSEvents.gameSceneActive -= this.BSEvents_gameSceneActive;
            // VRPointerOnEnablePatch.OnEnabled -= this.PointerOnEnabled;
            this.StopAllCoroutines();
            while (this._messages.TryDequeue(out var msg))
            {
                msg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
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

                Destroy(msg);
            }

            //_messages.Clear();
            Destroy(this._rootGameObject);
            if (this.TextPool != null)
            {
                this.TextPool.Dispose();
                this.TextPool = null;
            }

            if (this._chatScreen != null)
            {
                Destroy(this._chatScreen);
                this._chatScreen = null;
            }

            if (this._chatMoverMaterial != null)
            {
                Destroy(this._chatMoverMaterial);
                this._chatMoverMaterial = null;
            }

            base.OnDestroy();
        }

        private void Update()
        {
            if (!this._updateMessagePositions)
            {
                return;
            }

            // HMMainThreadDispatcher.instance.Enqueue(this.UpdateMessagePositions());
            // this.StartCoroutine(this.UpdateMessagePositions());
            _ = this.UpdateMessagePositions();
            Logger.Info("UpdateMessagePositions called");
            this._updateMessagePositions = false;
        }

        private FloatingScreen _chatScreen;
        private GameObject _chatContainer;
        private GameObject _rootGameObject;
        private Material _chatMoverMaterial;
        private ImageView _bg;
        private static readonly string s_menu = "MainMenu";
        private static readonly string s_game = "GameCore";

        private IEnumerator Start()
        {
            Logger.Info("ChatDisplay Start");
            DontDestroyOnLoad(this.gameObject);
            this._chatConfig = ChatConfig.instance;
            Logger.Info(
                $"Waiting for ESCFontManager to be initialized, ESCFontManager.instance.IsInitialized: {ESCFontManager.instance.IsInitialized}");
            yield return new WaitWhile(() => !ESCFontManager.instance.IsInitialized);
            Logger.Info(
                $"ESCFontManager initialized, ESCFontManager.instance.IsInitialized: {ESCFontManager.instance.IsInitialized}");
            this.SetupScreens();
            Logger.Info("SetupScreens complete");
            foreach (var msg in this._messages.ToArray())
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled)
                {
                    msg.SubText.SetAllDirty();
                }
            }

            (this.transform as RectTransform).pivot = new Vector2(0.5f, 0f);
            Logger.Info("Create TextPool");
            this.TextPool = new ObjectMemoryComponentPool<EnhancedTextMeshProUGUIWithBackground>(64,
                constructor: () =>
                {
                    Logger.Info("TextPool constructor");
                    var go = new GameObject(nameof(EnhancedTextMeshProUGUIWithBackground),
                        typeof(EnhancedTextMeshProUGUIWithBackground));
                    DontDestroyOnLoad(go);
                    Logger.Info("TextPool constructor");
                    go.SetActive(false);
                    Logger.Info("TextPool constructor");
                    Logger.Info("TextPool constructor");
                    var msg = go.GetComponent<EnhancedTextMeshProUGUIWithBackground>();
                    msg.Text.enableWordWrapping = true;
                    msg.Text.autoSizeTextContainer = false;
                    msg.SubText.enableWordWrapping = true;
                    msg.SubText.autoSizeTextContainer = false;
                    (msg.transform as RectTransform).pivot = new Vector2(0.5f, 0);
                    Logger.Info("TextPool constructor");
                    msg.transform.SetParent(this._chatContainer.transform, false);
                    Logger.Info("TextPool constructor complete");
                    this.UpdateMessage(msg);
                    Logger.Info("TextPool constructor complete 2");
                    return msg;
                },
                onFree: (msg) =>
                {
                    try
                    {
                        msg.gameObject.SetActive(false);
                        (msg.transform as RectTransform).localPosition = Vector3.zero;
                        msg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
                        msg.HighlightEnabled = false;
                        msg.AccentEnabled = false;
                        msg.SubTextEnabled = false;
                        msg.Text.text = "";
                        msg.Text.ChatMessage = null;
                        msg.SubText.text = "";
                        msg.SubText.ChatMessage = null;
                        msg.Text.ClearImages();
                        msg.SubText.ClearImages();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"An exception occurred while trying to free CustomText object. {ex.ToString()}");
                    }
                }
            );
            Logger.Info("TextPool created");
            ChatConfig.instance.OnConfigChanged += this.Instance_OnConfigChanged;
            BSEvents.menuSceneActive += this.BSEvents_menuSceneActive;
            BSEvents.gameSceneActive += this.BSEvents_gameSceneActive;
            SceneManager.activeSceneChanged += (a, b) => this.SetupScreens();

            yield return new WaitWhile(() => this._chatScreen == null);
            while (_backupMessageQueue.TryDequeue(out var msg))
            {
                var task = this.OnTextMessageReceived(msg.Value, msg.Key).GetAwaiter();
                yield return new WaitWhile(() => !task.IsCompleted);
            }
        }

        private void SetupScreens()
        {
            Logger.Info($"SetupScreens, _chatScreen: {this._chatScreen}");
            if (this._chatScreen == null)
            {
                var screenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                Logger.Info($"Creating FloatingScreen, screenSize: {screenSize}");
                this._chatScreen = FloatingScreen.CreateFloatingScreen(screenSize, true, this.ChatPosition,
                    Quaternion.identity, 0f, true);
                Logger.Info($"FloatingScreen created, _chatScreen: {this._chatScreen}");
                this._chatScreen.gameObject.layer = 5;
                Logger.Info($"Setting up FloatingScreen");
                this._chatScreen.HandleReleased += OnHandleReleased;
                Logger.Info($"HandleReleased event added");
                var rectMask2D = this._chatScreen.GetComponent<RectMask2D>();
                if (rectMask2D)
                {
                    Destroy(rectMask2D);
                }

                Logger.Info($"Creating chatContainer");
                this._chatContainer = new GameObject("chatContainer");
                this._chatContainer.transform.SetParent(this._chatScreen.transform, false);
                this._chatContainer.AddComponent<RectMask2D>().rectTransform.sizeDelta = screenSize;
                Logger.Info($"chatContainer created");

                var canvas = this._chatScreen.GetComponent<Canvas>();
                canvas.worldCamera = Camera.main;
                canvas.sortingOrder = 3;
                Logger.Info($"Setting up chatScreen");

                this._chatScreen.SetRootViewController(this, AnimationType.None);
                this._rootGameObject = new GameObject();
                DontDestroyOnLoad(this._rootGameObject);
                Logger.Info($"Creating chatMoverMaterial");

                this._chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlowMaterial);
                this._chatMoverMaterial.color = Color.clear;
                Logger.Info($"chatMoverMaterial created");

                var renderer = this._chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = this._chatMoverMaterial;
                renderer.material.mainTexture = this._chatMoverMaterial.mainTexture;
                Logger.Info($"Setting up chatScreen handle");

                this._chatScreen.transform.SetParent(this._rootGameObject.transform);
                this._chatScreen.ScreenRotation = Quaternion.Euler(this.ChatRotation);
                Logger.Info($"Setting up chatScreen rotation");

                // this._bg = this._chatScreen.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "bg");
                this._bg = this._chatScreen.GetComponentsInChildren<ImageView>()
                    .FirstOrDefault(x => x.name == "Background");
                this._bg.raycastTarget = false;
                this._bg.material = Instantiate(this._bg.material);
                this._bg.SetField("_gradient", false);
                this._bg.material.color = Color.white.ColorWithAlpha(1);
                this._bg.color = this.BackgroundColor;
                this._bg.SetAllDirty();
                Logger.Info($"ChatScreen setup complete");

                // this.AddToVRPointer();
                this.UpdateChatUI();
                Logger.Info($"UpdateChatUI complete");
            }

            Logger.Info($"SetupScreens complete, _chatScreen: {this._chatScreen}");
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

        private bool _updateMessagePositions = false;
        private WaitForEndOfFrame _waitForEndOfFrame;


        private IEnumerator UpdateMessagePositions()
        {
            Logger.Info($"UpdateMessagePositions, _messages.Count: {this._messages.Count}");
            yield return this._waitForEndOfFrame;
            // TODO: Remove later on
            //float msgPos =  (ReverseChatOrder ?  ChatHeight : 0);
            Logger.Info($"UpdateMessagePositions, ReverseChatOrder: {this.ReverseChatOrder}");
            float? msgPos = this.ChatHeight / (this.ReverseChatOrder ? 2f : -2f);
            Logger.Info($"UpdateMessagePositions, msgPos: {msgPos}");
            foreach (var chatMsg in this._messages.OrderBy(x => x.ReceivedDate).Reverse())
            {
                if (chatMsg == null)
                {
                    continue;
                }

                Logger.Info($"UpdateMessagePositions, chatMsg: {chatMsg}");
                var msgHeight = (chatMsg.transform as RectTransform)?.sizeDelta.y;
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

            Logger.Info("UpdateMessagePositions complete");
        }

        private void OnRenderRebuildComplete() => this._updateMessagePositions = true;

        public void AddMessage(EnhancedTextMeshProUGUIWithBackground newMsg)
        {
            newMsg.OnLatePreRenderRebuildComplete -= this.OnRenderRebuildComplete;
            newMsg.OnLatePreRenderRebuildComplete += this.OnRenderRebuildComplete;
            this.UpdateMessage(newMsg, true);
            this._messages.Enqueue(newMsg);
            this.ClearOldMessages();
        }

        private void UpdateChatUI()
        {
            Logger.Info("UpdateChatUI");
            this.ChatWidth = this._chatConfig.ChatWidth;
            this.ChatHeight = this._chatConfig.ChatHeight;
            this.FontSize = this._chatConfig.FontSize;
            Logger.Info($"ChatWidth: {this.ChatWidth}, ChatHeight: {this.ChatHeight}, FontSize: {this.FontSize}");
            this.AccentColor = this._chatConfig.AccentColor;
            this.HighlightColor = this._chatConfig.HighlightColor;
            this.BackgroundColor = this._chatConfig.BackgroundColor;
            Logger.Info(
                $"AccentColor: {this.AccentColor}, HighlightColor: {this.HighlightColor}, BackgroundColor: {this.BackgroundColor}");
            this.PingColor = this._chatConfig.PingColor;
            this.TextColor = this._chatConfig.TextColor;
            this.ReverseChatOrder = this._chatConfig.ReverseChatOrder;
            Logger.Info(
                $"PingColor: {this.PingColor}, TextColor: {this.TextColor}, ReverseChatOrder: {this.ReverseChatOrder}");
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

            Logger.Info(
                $"ChatPosition: {this.ChatPosition}, ChatRotation: {this.ChatRotation}, gameObject.layer: {this.gameObject.layer}");
            var chatContainerTransform = this._chatContainer.GetComponent<RectMask2D>().rectTransform!;
            Logger.Info($"chatContainerTransform: {chatContainerTransform}");
            chatContainerTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
            Logger.Info($"chatContainerTransform.sizeDelta: {chatContainerTransform.sizeDelta}");

            this._chatScreen.handle.transform.localScale = new Vector3(this.ChatWidth, this.ChatHeight * 0.9f, 0.01f);
            this._chatScreen.handle.transform.localPosition = Vector3.zero;
            this._chatScreen.handle.transform.localRotation = Quaternion.identity;
            Logger.Info(
                $"_chatScreen.handle.transform.localScale: {this._chatScreen.handle.transform.localScale}, _chatScreen.handle.transform.localPosition: {this._chatScreen.handle.transform.localPosition}");

            this.AllowMovement = this._chatConfig.AllowMovement;
            Logger.Info($"AllowMovement: {this.AllowMovement}");
            this.UpdateMessages();
            Logger.Info("UpdateChatUI complete");
        }

        private void UpdateMessages()
        {
            foreach (var msg in this._messages.ToArray())
            {
                this.UpdateMessage(msg, true);
            }

            this._updateMessagePositions = true;
        }

        private void UpdateMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            Logger.Info("UpdateMessage");
            (msg.transform as RectTransform).sizeDelta =
                new Vector2(this.ChatWidth, (msg.transform as RectTransform).sizeDelta.y);
            msg.Text.font = ESCFontManager.instance.MainFont;
            msg.Text.font.fallbackFontAssetTable = ESCFontManager.instance.FallBackFonts;
            msg.Text.overflowMode = TextOverflowModes.Overflow;
            msg.Text.alignment = TextAlignmentOptions.BottomLeft;
            msg.Text.color = this.TextColor;
            msg.Text.fontSize = this.FontSize;
            msg.Text.lineSpacing = 1.5f;
            Logger.Info("UpdateMessage: Text complete");
            Logger.Info(
                $"UpdateMessage: Text: {msg.Text.text}, ChatMessage: {msg.Text.ChatMessage}, font: {msg.Text.font}, color: {msg.Text.color}, fontSize: {msg.Text.fontSize}, lineSpacing: {msg.Text.lineSpacing}");
            if (msg.Text.ChatMessage != null)
            {
                Logger.Info($"UpdateMessage: Text.ChatMessage: {msg.Text.ChatMessage.Message}");
            }

            Logger.Info("UpdateMessage: SubText");
            msg.SubText.font = ESCFontManager.instance.MainFont;
            msg.SubText.font.fallbackFontAssetTable = ESCFontManager.instance.FallBackFonts;
            msg.SubText.overflowMode = TextOverflowModes.Overflow;
            msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
            msg.SubText.color = this.TextColor;
            msg.SubText.fontSize = this.FontSize;
            msg.SubText.lineSpacing = 1.5f;
            Logger.Info("UpdateMessage: SubText complete");

            if (msg.Text.ChatMessage != null)
            {
                msg.HighlightColor = msg.Text.ChatMessage.IsPing ? this.PingColor : this.HighlightColor;
                msg.AccentColor = this.AccentColor;
                msg.HighlightEnabled = msg.Text.ChatMessage.IsHighlighted || msg.Text.ChatMessage.IsPing;
                msg.AccentEnabled = !msg.Text.ChatMessage.IsPing &&
                                    (msg.HighlightEnabled || msg.SubText.ChatMessage != null);
            }

            Logger.Info("UpdateMessage: SetAllDirty");
            if (setAllDirty)
            {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled)
                {
                    msg.SubText.SetAllDirty();
                }
            }

            if (msg.Text.ChatMessage is BilibiliChatMessage)
            {
                Logger.Info($"is BilibiliChatMessage");
            }
            if (msg.Text != null && msg.Text.textInfo != null)
            {
                Logger.Info($"text characterCount: {msg.Text.textInfo.characterCount}");
            }

            Logger.Info("UpdateMessage complete");
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
            while (this._messages.TryPeek(out var msg) && this.ReverseChatOrder
                       ? msg.transform.localPosition.y < 0 - (msg.transform as RectTransform).sizeDelta.y
                       : msg.transform.localPosition.y >= ChatConfig.instance.ChatHeight)
            {
                if (this._messages.TryDequeue(out msg))
                {
                    this.TextPool.Free(msg);
                }
            }
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
            var newMsg = this.TextPool.Alloc();
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

                Logger.Info($"Pre-cached {count} animated emotes.");
            }
            else
            {
                Logger.Warn(
                    "Pre-caching of animated emotes disabled by the user. If you're experiencing lag, re-enable emote precaching.");
            }
        });

        private EnhancedTextMeshProUGUIWithBackground _lastMessage;

        // public void OnTextMessageReceived(IChatMessage msg) => _ = this.OnTextMessageReceived(msg, DateTime.Now);
        public void OnTextMessageReceived(IChatMessage msg)
        {
            Logger.Info($"Received message: {msg.Message}");
            _ = this.OnTextMessageReceived(msg, DateTime.Now);
            Logger.Info($"OnTextMessageReceived: {msg.Message}");
        }

        public async Task OnTextMessageReceived(IChatMessage msg, DateTime dateTime)
        {
            Logger.Info(
                $"Received message: msg.Id: {msg.Id}, msg.IsSystemMessage: {msg.IsSystemMessage}, msg.IsActionMessage: {msg.IsActionMessage}, msg.IsHighlighted: {msg.IsHighlighted}, msg.IsPing: {msg.IsPing}, msg.Message: {msg.Message}, msg.Sender: {msg.Sender}, msg.Channel: {msg.Channel}, msg.Emotes: {msg.Emotes}, msg.Metadata: {msg.Metadata}");
            var parsedMessage = await ChatMessageBuilder.BuildMessage(msg, ESCFontManager.instance.FontInfo);
            Logger.Info($"Build message end: {parsedMessage}");
            if (this.TextPool == null)
            {
                Logger.Warn("TextPool is null, waiting for it to be initialized.");
            }

            while (this.TextPool == null)
            {
                await Task.Delay(100);
            }

            Logger.Info($"Create message coroutine: {parsedMessage}");
            // 没有HMMainThreadDispatcher了
            // HMMainThreadDispatcher.instance.Enqueue(() => this.CreateMessage(msg, dateTime, parsedMessage));
            this.StartCoroutine(CreateMessageCoroutine(msg, dateTime, parsedMessage));
            Logger.Info($"Create message coroutine end: {parsedMessage}");
        }

        private IEnumerator CreateMessageCoroutine(IChatMessage msg, DateTime dateTime, string parsedMessage)
        {
            Logger.Info($"Into Create message coroutine: {parsedMessage}");
            this.CreateMessage(msg, dateTime, parsedMessage);
            Logger.Info($"Create message coroutine end 2: {parsedMessage}");
            yield return null; // 使用yield return null 表示等待一帧
        }

        private void CreateMessage(IChatMessage msg, DateTime date, string parsedMessage)
        {
            Logger.Info($"Create message: {parsedMessage}");
            if (this._lastMessage != null && !msg.IsSystemMessage && this._lastMessage.Text.ChatMessage.Id == msg.Id)
            {
                // If the last message received had the same id and isn't a system message, then this was a sub-message of the original and may need to be highlighted along with the original message
                this._lastMessage.SubText.text = parsedMessage;
                this._lastMessage.SubText.ChatMessage = msg;
                this._lastMessage.SubTextEnabled = true;
                Logger.Info($"Sub message: {parsedMessage}");
                this.UpdateMessage(this._lastMessage, true);
                Logger.Info($"Update message: {parsedMessage}");
            }
            else
            {
                // parsedMessage = "abcdddac <color=#FFFFFFFF><b>颤抖的方便面说</b></color>: 1";
                Logger.Info($"New message: {parsedMessage}");
                var newMsg = this.TextPool.Alloc();
                newMsg.gameObject.SetActive(true);
                Logger.Info($"New message font: {newMsg.Text.font}, name: {newMsg.Text.font.name}");
                newMsg.Text.font = ESCFontManager.instance.MainFont;
                newMsg.Text.ChatMessage = msg;
                // newMsg.Text.text = parsedMessage;
                // newMsg.Text.SetText(parsedMessage);
                try
                {
                    newMsg.Text.SetText(parsedMessage);
                    try
                    {
                        var fieldInfo = typeof(TextMeshProUGUI).GetField("m_TextProcessingArray",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        var mTextProcessingArray = fieldInfo.GetValue(newMsg.Text);
                        Logger.Info($"m_TextProcessingArray is null: {mTextProcessingArray == null}");
                        if (mTextProcessingArray != null)
                        {
                            Logger.Info($"m_TextProcessingArray length: {((Array)mTextProcessingArray).Length}");
                            for (var i = 0; i < ((Array)mTextProcessingArray).Length; i++)
                            {
                                Logger.Info($"m_TextProcessingArray[{i}]: {((Array)mTextProcessingArray).GetValue(i):X}");
                                // Logger.Info($"m_TextProcessingArray[{i}]: unicode = {((Array)mTextProcessingArray).GetValue(i).unicode:X}, stringIndex = {mTextProcessingArray[i].stringIndex}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception while trying to force mesh update. {ex.StackTrace}");
                    }

                } catch (Exception e)
                {
                    Logger.Error($"Error setting text: {e}");
                }

                try
                {
                    // 用反射获取m_fontAsset是否为空
                    var fieldInfo = typeof(TextMeshProUGUI).GetField("m_fontAsset",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var mFontAsset = fieldInfo.GetValue(newMsg.Text);
                    Logger.Info($"m_fontAsset is null: {mFontAsset == null}");
                    if (mFontAsset != null)
                    {
                        Logger.Info($"m_fontAsset name: {mFontAsset.GetType().Name}");
                    }
                    _ = newMsg.Text.GetTextInfo(parsedMessage);
                }
                catch (NullReferenceException ex)
                {
                    Logger.Error($"Error message: {ex.Message}");
                    Logger.Error($"NullReferenceException in SetArraySizes: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Logger.Error($"Inner exception: {ex.InnerException.Message}");
                        Logger.Error($"Inner exception stack trace: {ex.InnerException.StackTrace}");
                    }
                    Logger.Error($"Exception source: {ex.Source}");
                    Logger.Error($"Exception target site: {ex.TargetSite}");

                }
                catch (Exception e)
                {
                    Logger.Error($"Error getting text info: {e.StackTrace}");
                }

                newMsg.ReceivedDate = date;
                // 输出一下textinfo，尤其是characterCount等信息
                Logger.Info($"TextInfo: {newMsg.Text.textInfo}");
                Logger.Info($"TextInfo textComponent: {newMsg.Text.textInfo.textComponent.text}");
                Logger.Info($"TextInfo characterCount: {newMsg.Text.textInfo.characterCount}");
                Logger.Info($"TextInfo spriteCount: {newMsg.Text.textInfo.spriteCount}");
                Logger.Info($"TextInfo spaceCount: {newMsg.Text.textInfo.spaceCount}");
                Logger.Info($"TextInfo wordCount: {newMsg.Text.textInfo.wordCount}");
                Logger.Info($"TextInfo linkCount: {newMsg.Text.textInfo.linkCount}");
                Logger.Info($"TextInfo lineCount: {newMsg.Text.textInfo.lineCount}");
                Logger.Info($"TextInfo pageCount: {newMsg.Text.textInfo.pageCount}");
                Logger.Info($"TextInfo materialCount: {newMsg.Text.textInfo.materialCount}");

                /*if (msg is BilibiliChatMessage) {
                   var message = msg.AsBilibiliMessage();
                   if (message.MessageType == "pk_pre") {
                       Task.Run(() => {
                           int tic = message.extra["timer"] - 1;
                           while (tic > 0) {
                               Thread.Sleep(1000);
                               if (UpdateMessageContent(msg.Id, "【大乱斗】距离与" + message.extra["uname"] + "的PK还有" + tic-- + "秒")) break;
                           }
                       });
                   } else if (message.MessageType == "pk_start") {
                       Task.Run(() => {
                           int tic = message.extra["timer"] - 1;
                           while (tic > 0)
                           {
                               Thread.Sleep(1000);
                               if (UpdateMessageContent(msg.Id, "【大乱斗】距离与 " + message.extra["uname"] + " 的PK还有" + tic-- + "秒")) break;
                           }
                       });
                   }
               }*/
                Logger.Info($"New message: {newMsg.Text.text}");
                this.AddMessage(newMsg);
                Logger.Info($"Add message: {newMsg.Text.text}");
                this._lastMessage = newMsg;
                Logger.Info($"Last message: {newMsg.Text.text}");
            }

            Logger.Info($"Update message positions: {parsedMessage}");
            this._updateMessagePositions = true;
            Logger.Info($"Update message positions end: {parsedMessage}");
        }
    }
}