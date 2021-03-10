using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using BS_Utils.Utilities;
using ChatCore.Interfaces;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using HMUI;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace EnhancedStreamChat.Chat
{
    [HotReload]
    public partial class ChatDisplay : BSMLAutomaticViewController
    {
        public ObjectPool<EnhancedTextMeshProUGUIWithBackground> TextPool { get; internal set; }
        private ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground> _messages = new ConcurrentQueue<EnhancedTextMeshProUGUIWithBackground>();
        private ChatConfig _chatConfig;
        
        private volatile bool _isInGame;

        private void Awake()
        {
            _waitForEndOfFrame = new WaitForEndOfFrame();
            HMMainThreadDispatcher.instance.Enqueue(this.Initialize());
        }

        // TODO: eventually figure out a way to make this more modular incase we want to create multiple instances of ChatDisplay
        private static ConcurrentQueue<KeyValuePair<DateTime, IChatMessage>> _backupMessageQueue = new ConcurrentQueue<KeyValuePair<DateTime, IChatMessage>>();
        protected override void OnDestroy()
        {
            base.OnDestroy();
            ChatConfig.instance.OnConfigChanged -= Instance_OnConfigChanged;
            BSEvents.menuSceneActive -= BSEvents_menuSceneActive;
            BSEvents.gameSceneActive -= BSEvents_gameSceneActive;
            StopAllCoroutines();
            while (_messages.TryDequeue(out var msg)) {
                msg.OnLatePreRenderRebuildComplete -= OnRenderRebuildComplete;
                if (msg.Text.ChatMessage != null) {
                    _backupMessageQueue.Enqueue(new KeyValuePair<DateTime, IChatMessage>(msg.ReceivedDate, msg.Text.ChatMessage));
                }
                if (msg.SubText.ChatMessage != null) {
                    _backupMessageQueue.Enqueue(new KeyValuePair<DateTime, IChatMessage>(msg.ReceivedDate, msg.SubText.ChatMessage));
                }
                Destroy(msg);
            }
            //_messages.Clear();
            Destroy(_rootGameObject);
            if (TextPool != null) {
                TextPool.Dispose();
                TextPool = null;
            }
            if (_chatScreen != null) {
                Destroy(_chatScreen);
                _chatScreen = null;
            }
            if (_chatMoverMaterial != null) {
                Destroy(_chatMoverMaterial);
                _chatMoverMaterial = null;
            }
        }

        private void Update()
        {
            if (!_updateMessagePositions) {
                return;
            }
            HMMainThreadDispatcher.instance.Enqueue(this.UpdateMessagePositions());
            _updateMessagePositions = false;
        }

        private volatile bool _applicationQuitting = false;
        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private FloatingScreen _chatScreen;
        private GameObject _chatContainer;
        private GameObject _rootGameObject;
        private Material _chatMoverMaterial;
        private ImageView _bg;

        IEnumerator Initialize()
        {
            DontDestroyOnLoad(gameObject);
            _chatConfig = ChatConfig.instance;
            yield return new WaitWhile(() => !ESCFontManager.instance.IsInitialized);
            SetupScreens();
            foreach (var msg in _messages.ToArray()) {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled) {
                    msg.SubText.SetAllDirty();
                }
            }
            (transform as RectTransform).pivot = new Vector2(0.5f, 0f);
            TextPool = new ObjectPool<EnhancedTextMeshProUGUIWithBackground>(64,
                constructor: () =>
                {
                    var go = new GameObject();
                    DontDestroyOnLoad(go);
                    var msg = go.AddComponent<EnhancedTextMeshProUGUIWithBackground>();
                    msg.gameObject.SetActive(true);
                    msg.Text.enableWordWrapping = true;
                    msg.Text.autoSizeTextContainer = false;
                    msg.SubText.enableWordWrapping = true;
                    msg.SubText.autoSizeTextContainer = false;
                    (msg.transform as RectTransform).pivot = new Vector2(0.5f, 0);
                    msg.transform.SetParent(_chatContainer.transform, false);
                    HMMainThreadDispatcher.instance.Enqueue(UpdateMessage(msg));
                    return msg;
                },
                onFree: (msg) =>
                {
                    try {
                        msg.OnLatePreRenderRebuildComplete -= OnRenderRebuildComplete;
                        msg.HighlightEnabled = false;
                        msg.AccentEnabled = false;
                        msg.SubTextEnabled = false;
                        msg.Text.text = "";
                        msg.Text.ChatMessage = null;
                        msg.SubText.text = "";
                        msg.SubText.ChatMessage = null;
                        msg.Text.ClearImages();
                        msg.SubText.ClearImages();
                        msg.transform.localPosition = Vector3.zero;
                    }
                    catch (Exception ex) {
                        Logger.log.Error($"An exception occurred while trying to free CustomText object. {ex.ToString()}");
                    }
                }
            );
            ChatConfig.instance.OnConfigChanged += Instance_OnConfigChanged;
            BSEvents.menuSceneActive += BSEvents_menuSceneActive;
            BSEvents.gameSceneActive += BSEvents_gameSceneActive;

            yield return new WaitWhile(() => _chatScreen == null);

            while (_backupMessageQueue.TryDequeue(out var msg)) {
                OnTextMessageReceived(msg.Value, msg.Key);
            }
    }

        private void SetupScreens()
        {
            if (_chatScreen == null) {
                var screenSize = new Vector2(ChatWidth, ChatHeight);
                _chatScreen = FloatingScreen.CreateFloatingScreen(screenSize, true, ChatPosition, Quaternion.identity, 0f, true);
                var rectMask2D = _chatScreen.GetComponent<RectMask2D>();
                if (rectMask2D) {
                    Destroy(rectMask2D);
                }

                _chatContainer = new GameObject("chatContainer");
                _chatContainer.transform.SetParent(_chatScreen.transform, false);
                _chatContainer.AddComponent<RectMask2D>().rectTransform.sizeDelta = screenSize;

                var canvas = _chatScreen.GetComponent<Canvas>();
                canvas.sortingOrder = 3;

                _chatScreen.SetRootViewController(this, AnimationType.None);
                _rootGameObject = new GameObject();
                DontDestroyOnLoad(_rootGameObject);

                _chatMoverMaterial = Instantiate(BeatSaberUtils.UINoGlowMaterial);
                _chatMoverMaterial.color = Color.clear;

                var renderer = _chatScreen.handle.gameObject.GetComponent<Renderer>();
                renderer.material = _chatMoverMaterial;
                renderer.material.mainTexture = _chatMoverMaterial.mainTexture;

                _chatScreen.transform.SetParent(_rootGameObject.transform);
                _chatScreen.ScreenRotation = Quaternion.Euler(ChatRotation);

                _bg = _chatScreen.GetComponentInChildren<ImageView>();
                _bg.material.mainTexture = BeatSaberUtils.UINoGlowMaterial.mainTexture;
                _bg.color = BackgroundColor;

                AddToVRPointer();
                UpdateChatUI();
            }
        }

        private void Instance_OnConfigChanged(ChatConfig obj)
        {
            UpdateChatUI();
        }

        private void floatingScreen_OnRelease(Vector3 pos, Quaternion rot)
        {
            if (_isInGame) {
                _chatConfig.Song_ChatPosition = pos;
                _chatConfig.Song_ChatRotation = rot.eulerAngles;
            }
            else {
                _chatConfig.Menu_ChatPosition = pos;
                _chatConfig.Menu_ChatRotation = rot.eulerAngles;
            }
            _chatConfig.Save();
        }

        private void BSEvents_gameSceneActive()
        {
            _isInGame = true;
            AddToVRPointer();
            UpdateChatUI();
        }

        private void BSEvents_menuSceneActive()
        {
            _isInGame = false;
            AddToVRPointer();
            UpdateChatUI();
        }

        private void AddToVRPointer()
        {
            if (_chatScreen.screenMover) {
                _chatScreen.screenMover.OnRelease -= floatingScreen_OnRelease;
                _chatScreen.screenMover.OnRelease += floatingScreen_OnRelease;
                _chatScreen.screenMover.transform.SetAsFirstSibling();
            }
        }

        private volatile bool _updateMessagePositions = false;
        private WaitForEndOfFrame _waitForEndOfFrame;


        private IEnumerator UpdateMessagePositions()
        {
            yield return _waitForEndOfFrame;
            // TODO: Remove later on
            //float msgPos =  (ReverseChatOrder ?  ChatHeight : 0);
            float? msgPos = ChatHeight / (ReverseChatOrder ? 2f : -2f);
            foreach (var chatMsg in _messages.OrderBy(x => x.ReceivedDate).ToArray().Reverse()) {
                if (chatMsg == null) {
                    continue;
                }
                var msgHeight = (chatMsg.transform as RectTransform)?.sizeDelta.y;
                if (ReverseChatOrder) {
                    msgPos -= msgHeight;
                }
                chatMsg.transform.localPosition = new Vector3(0, msgPos ?? 0);
                if (!ReverseChatOrder) {
                    msgPos += msgHeight;
                }
                yield return null;
            }
        }

        private void OnRenderRebuildComplete()
        {
            _updateMessagePositions = true;
        }

        public void AddMessage(EnhancedTextMeshProUGUIWithBackground newMsg)
        {
            newMsg.OnLatePreRenderRebuildComplete -= OnRenderRebuildComplete;
            newMsg.OnLatePreRenderRebuildComplete += OnRenderRebuildComplete;
            HMMainThreadDispatcher.instance.Enqueue(UpdateMessage(newMsg));
            _messages.Enqueue(newMsg);
            HMMainThreadDispatcher.instance.Enqueue(ClearOldMessages());
        }

        private void UpdateChatUI()
        {
            ChatWidth = _chatConfig.ChatWidth;
            ChatHeight = _chatConfig.ChatHeight;
            FontSize = _chatConfig.FontSize;
            AccentColor = _chatConfig.AccentColor;
            HighlightColor = _chatConfig.HighlightColor;
            BackgroundColor = _chatConfig.BackgroundColor;
            PingColor = _chatConfig.PingColor;
            TextColor = _chatConfig.TextColor;
            ReverseChatOrder = _chatConfig.ReverseChatOrder;
            if (_isInGame) {
                ChatPosition = _chatConfig.Song_ChatPosition;
                ChatRotation = _chatConfig.Song_ChatRotation;
            }
            else {
                ChatPosition = _chatConfig.Menu_ChatPosition;
                ChatRotation = _chatConfig.Menu_ChatRotation;
            }
            var chatContainerTransform = _chatContainer.GetComponent<RectMask2D>().rectTransform!;
            chatContainerTransform.sizeDelta = new Vector2(ChatWidth, ChatHeight);

            _chatScreen.handle.transform.localScale = new Vector3(ChatWidth, ChatHeight * 0.9f, 0.01f);
            _chatScreen.handle.transform.localPosition = Vector3.zero;
            _chatScreen.handle.transform.localRotation = Quaternion.identity;

            AllowMovement = _chatConfig.AllowMovement;
            UpdateMessages();
        }

        private void UpdateMessages()
        {
            foreach (var msg in _messages.ToArray()) {
                HMMainThreadDispatcher.instance.Enqueue(UpdateMessage(msg, true));
            }
            _updateMessagePositions = true;
        }

        private IEnumerator UpdateMessage(EnhancedTextMeshProUGUIWithBackground msg, bool setAllDirty = false)
        {
            yield return _waitForEndOfFrame;
            (msg.transform as RectTransform).sizeDelta = new Vector2(ChatWidth, (msg.transform as RectTransform).sizeDelta.y);
            msg.Text.font = ESCFontManager.instance.MainFont;
            msg.Text.font.fallbackFontAssetTable = ESCFontManager.instance.FallBackFonts;
            msg.Text.overflowMode = TextOverflowModes.Overflow;
            msg.Text.alignment = TextAlignmentOptions.BottomLeft;
            msg.Text.color = TextColor;
            msg.Text.fontSize = FontSize;
            msg.Text.lineSpacing = 1.5f;

            msg.SubText.font = ESCFontManager.instance.MainFont;
            msg.SubText.font.fallbackFontAssetTable = ESCFontManager.instance.FallBackFonts;
            msg.SubText.overflowMode = TextOverflowModes.Overflow;
            msg.SubText.alignment = TextAlignmentOptions.BottomLeft;
            msg.SubText.color = TextColor;
            msg.SubText.fontSize = FontSize;
            msg.SubText.lineSpacing = 1.5f;

            if (msg.Text.ChatMessage != null) {
                msg.HighlightColor = msg.Text.ChatMessage.IsPing ? PingColor : HighlightColor;
                msg.AccentColor = AccentColor;
                msg.HighlightEnabled = msg.Text.ChatMessage.IsHighlighted || msg.Text.ChatMessage.IsPing;
                msg.AccentEnabled = !msg.Text.ChatMessage.IsPing && (msg.HighlightEnabled || msg.SubText.ChatMessage != null);
            }
            yield return _waitForEndOfFrame;
            if (setAllDirty) {
                msg.Text.SetAllDirty();
                if (msg.SubTextEnabled) {
                    msg.SubText.SetAllDirty();
                }
            }
            yield return null;
        }
        private IEnumerator ClearOldMessages()
        {
            while (_messages.TryPeek(out var msg) && ReverseChatOrder ? msg.transform.localPosition.y < 0 - (msg.transform as RectTransform).sizeDelta.y : msg.transform.localPosition.y >= ChatConfig.instance.ChatHeight) {
                if (_messages.TryDequeue(out msg)) {
                    TextPool.Free(msg);
                    yield return null;
                }
            }
        }

        private string BuildClearedMessage(EnhancedTextMeshProUGUI msg)
        {
            StringBuilder sb = new StringBuilder($"<color={msg.ChatMessage.Sender.Color}>{msg.ChatMessage.Sender.DisplayName}</color>");
            var badgeEndIndex = msg.text.IndexOf("<color=");
            if (badgeEndIndex != -1) {
                sb.Insert(0, msg.text.Substring(0, badgeEndIndex));
            }
            sb.Append(": <color=#bbbbbbbb><message deleted></color>");
            return sb.ToString();
        }

        private void ClearMessage(EnhancedTextMeshProUGUIWithBackground msg)
        {
            // Only clear non-system messages
            if (!msg.Text.ChatMessage.IsSystemMessage) {
                msg.Text.text = BuildClearedMessage(msg.Text);
                msg.SubTextEnabled = false;
            }
            if (msg.SubText.ChatMessage != null && !msg.SubText.ChatMessage.IsSystemMessage) {
                msg.SubText.text = BuildClearedMessage(msg.SubText);
            }
        }

        public void OnMessageCleared(string messageId)
        {
            if (messageId != null) {
                MainThreadInvoker.Invoke(() =>
                {
                    foreach (var msg in _messages.ToArray()) {
                        if (msg.Text.ChatMessage == null) {
                            continue;
                        }
                        if (msg.Text.ChatMessage.Id == messageId) {
                            ClearMessage(msg);
                        }
                    }
                });
            }
        }

        public void OnChatCleared(string userId)
        {
            MainThreadInvoker.Invoke(() =>
            {
                foreach (var msg in _messages.ToArray()) {
                    if (msg.Text.ChatMessage == null) {
                        continue;
                    }
                    if (userId == null || msg.Text.ChatMessage.Sender.Id == userId) {
                        ClearMessage(msg);
                    }
                }
            });
        }

        public void OnJoinChannel(IChatService svc, IChatChannel channel)
        {
            MainThreadInvoker.Invoke(() =>
            {
                var newMsg = TextPool.Alloc();
                newMsg.Text.text = $"<color=#bbbbbbbb>[{svc.DisplayName}] Success joining {channel.Id}</color>";
                newMsg.HighlightEnabled = true;
                newMsg.HighlightColor = Color.gray.ColorWithAlpha(0.05f);
                AddMessage(newMsg);
            });
        }

        public void OnChannelResourceDataCached(IChatChannel channel, Dictionary<string, IChatResourceData> resources)
        {
            MainThreadInvoker.Invoke(() =>
            {
                int count = 0;
                if (_chatConfig.PreCacheAnimatedEmotes) {
                    foreach (var emote in resources) {
                        if (emote.Value.IsAnimated) {
                            HMMainThreadDispatcher.instance.Enqueue(ChatImageProvider.instance.PrecacheAnimatedImage(emote.Value.Uri, emote.Key, 110));
                            count++;
                        }
                    }
                    Logger.log.Info($"Pre-cached {count} animated emotes.");
                }
                else {
                    Logger.log.Warn("Pre-caching of animated emotes disabled by the user. If you're experiencing lag, re-enable emote precaching.");
                }
            });
        }

        EnhancedTextMeshProUGUIWithBackground _lastMessage;
        public void OnTextMessageReceived(IChatMessage msg)
        {
            this.OnTextMessageReceived(msg, DateTime.Now);
        }
        public async void OnTextMessageReceived(IChatMessage msg, DateTime dateTime)
        {
            string parsedMessage = await ChatMessageBuilder.BuildMessage(msg, ESCFontManager.instance.FontInfo, _isInGame);
            this.CreateMessage(msg, dateTime, parsedMessage);
        }

        private void CreateMessage(IChatMessage msg, DateTime date, string parsedMessage)
        {
            if (_lastMessage != null && !msg.IsSystemMessage && _lastMessage.Text.ChatMessage.Id == msg.Id) {
                // If the last message received had the same id and isn't a system message, then this was a sub-message of the original and may need to be highlighted along with the original message
                _lastMessage.SubText.text = parsedMessage;
                _lastMessage.SubText.ChatMessage = msg;
                _lastMessage.SubTextEnabled = true;
                HMMainThreadDispatcher.instance.Enqueue(UpdateMessage(_lastMessage));
            }
            else {
                var newMsg = TextPool.Alloc();
                newMsg.gameObject.SetActive(true);
                newMsg.Text.ChatMessage = msg;
                newMsg.Text.text = parsedMessage;
                newMsg.ReceivedDate = date;
                AddMessage(newMsg);
                _lastMessage = newMsg;
            }
        }
    }
}