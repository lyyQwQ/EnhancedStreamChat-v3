using BeatSaberMarkupLanguage;
using BS_Utils.Utilities;
using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Logging;
using ChatCore.Services;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ChatCore.Config;
using BeatSaberMarkupLanguage;
using ChatCore.Config;
using BeatSaberMarkupLanguage;
using ChatCore.Config;
using BeatSaberMarkupLanguage;
using ChatCore.Config;
    public class ChatManager : PersistentSingleton<ChatManager>
        private void Awake() => DontDestroyOnLoad(this.gameObject);

    {
        void Awake()
        {
            this._sc = ChatCoreInstance.Create();
        }
            this._svcs = this._sc.RunAllServices();
            this._svcs.OnJoinChannel += this.QueueOrSendOnJoinChannel;
            this._svcs.OnTextMessageReceived += this.QueueOrSendOnTextMessageReceived;
            this._svcs.OnChatCleared += this.QueueOrSendOnClearChat;
            this._svcs.OnMessageCleared += this.QueueOrSendOnClearMessage;
            this._svcs.OnChannelResourceDataCached += this.QueueOrSendOnChannelResourceDataCached;
            _svcs = _sc.RunAllServices();
            _svcs.OnJoinChannel += QueueOrSendOnJoinChannel;
            _svcs.OnTextMessageReceived += QueueOrSendOnTextMessageReceived;
            Task.Run(this.HandleOverflowMessageQueue);
            BSEvents.lateMenuSceneLoadedFresh += this.BSEvents_menuSceneLoadedFresh;
            Task.Run(HandleOverflowMessageQueue);
            BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
            Task.Run(HandleOverflowMessageQueue);
            BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
            Task.Run(HandleOverflowMessageQueue);
            BSEvents.menuSceneLoadedFresh += BSEvents_menuSceneLoadedFresh;
        }

        private void _sc_OnLogReceived(CustomLogLevel level, string category, string log)
        {
            var newLevel = level switch
            {
                CustomLogLevel.Critical => IPA.Logging.Logger.Level.Critical,
                CustomLogLevel.Debug => IPA.Logging.Logger.Level.Debug,
                CustomLogLevel.Error => IPA.Logging.Logger.Level.Error,
                CustomLogLevel.Information => IPA.Logging.Logger.Level.Info,
                CustomLogLevel.Trace => IPA.Logging.Logger.Level.Trace,
                CustomLogLevel.Warning => IPA.Logging.Logger.Level.Warning,
                _ => IPA.Logging.Logger.Level.None
            if (this._svcs != null) {
                this._svcs.OnJoinChannel -= this.QueueOrSendOnJoinChannel;
                this._svcs.OnTextMessageReceived -= this.QueueOrSendOnTextMessageReceived;
                this._svcs.OnChatCleared -= this.QueueOrSendOnClearChat;
                this._svcs.OnMessageCleared -= this.QueueOrSendOnClearMessage;
                this._svcs.OnChannelResourceDataCached -= this.QueueOrSendOnChannelResourceDataCached;
            if (this._sc != null) {
                BSEvents.menuSceneLoadedFresh -= BSEvents_menuSceneLoadedFresh;
                this._sc.StopAllServices();
                BSEvents.menuSceneLoadedFresh -= BSEvents_menuSceneLoadedFresh;
            if (this._chatDisplay != null) {
                Destroy(this._chatDisplay.gameObject);
                this._chatDisplay = null;
                _sc.StopAllServices();
                //_sc.OnLogReceived -= _sc_OnLogReceived;
            if(_chatDisplay != null)
            {
                Destroy(_chatDisplay.gameObject);
                _chatDisplay = null;
                Destroy(this._chatDisplay.gameObject);
        private ChatDisplay _chatDisplay;
            if (this._chatDisplay != null) {
                DestroyImmediate(this._chatDisplay.gameObject);
                this._chatDisplay = null;
        private void BSEvents_menuSceneLoadedFresh()
        ChatDisplay _chatDisplay;
            if (_chatDisplay != null)
            {
            this._chatDisplay = BeatSaberUI.CreateViewController<ChatDisplay>();
            this._chatDisplay.gameObject.SetActive(true);
        private readonly ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
        private readonly SemaphoreSlim _msgLock = new SemaphoreSlim(1, 1);
                this._chatDisplay = null;
            _chatDisplay = BeatSaberUI.CreateViewController<ChatDisplay>();
            while (!_applicationIsQuitting) {
                if (this._chatDisplay == null) {
        private readonly ConcurrentQueue<Action> _actionQueue = new ConcurrentQueue<Action>();
                    await this._msgLock.WaitAsync();
                    while (this._chatDisplay == null) {
                if (_chatDisplay == null)
                {
            while (!_applicationIsQuitting) {
                else {
                    {
                    while (this._actionQueue.IsEmpty) {
                        await Task.Delay(1000);
                else
                {
                }
                    await this._msgLock.WaitAsync();
                    {
                var i = 0;
                var start = DateTime.UtcNow;
                var stopwatch = Stopwatch.StartNew();
                        await Task.Delay(1000);
                while (this._actionQueue.TryDequeue(out var action)) {
                int i = 0;
                DateTime start = DateTime.UtcNow;
                Stopwatch stopwatch = Stopwatch.StartNew();
                var start = DateTime.UtcNow;
                Logger.log.Warn($"{i} overflowed actions were executed in {stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond}ms.");
                {
                this._msgLock.Release();
                while (this._actionQueue.TryDequeue(out var action)) {
                    action.Invoke();
                    i++;
                Logger.log.Warn($"{i} overflowed actions were executed in {stopwatch.ElapsedTicks/TimeSpan.TicksPerMillisecond}ms.");
                stopwatch.Stop();
            if (this._chatDisplay == null || !this._msgLock.Wait(50)) {
                this._actionQueue.Enqueue(() => action.Invoke(svc, a));
            }
            else {
            if (_chatDisplay == null || !_msgLock.Wait(50))
                this._msgLock.Release();
                _actionQueue.Enqueue(() => action.Invoke(svc, a));
            if (this._chatDisplay == null || !this._msgLock.Wait(50)) {
            else
            {
            if (this._chatDisplay == null || !this._msgLock.Wait(50)) {
                this._actionQueue.Enqueue(() => action.Invoke(svc, a, b));
                this._msgLock.Release();
            else {
            if (_chatDisplay == null || !_msgLock.Wait(50))
                this._msgLock.Release();
                _actionQueue.Enqueue(() => action.Invoke(svc, a, b));
            if (this._chatDisplay == null || !this._msgLock.Wait(50)) {
            else
        private void QueueOrSendOnChannelResourceDataCached(IChatService svc, IChatChannel channel, Dictionary<string, IChatResourceData> resources) => this.QueueOrSendMessage(svc, channel, resources, this.OnChannelResourceDataCached);
        private void OnChannelResourceDataCached(IChatService svc, IChatChannel channel, Dictionary<string, IChatResourceData> resources) => this._chatDisplay.OnChannelResourceDataCached(channel, resources);
            }
        private void QueueOrSendOnTextMessageReceived(IChatService svc, IChatMessage msg) => this.QueueOrSendMessage(svc, msg, this.OnTextMesssageReceived);
        private void OnTextMesssageReceived(IChatService svc, IChatMessage msg) => this._chatDisplay.OnTextMessageReceived(msg);
        private void QueueOrSendOnChannelResourceDataCached(IChatService svc, IChatChannel channel, Dictionary<string, IChatResourceData> resources) => this.QueueOrSendMessage(svc, channel, resources, this.OnChannelResourceDataCached);
        private void QueueOrSendOnJoinChannel(IChatService svc, IChatChannel channel) => this.QueueOrSendMessage(svc, channel, this.OnJoinChannel);
        private void OnJoinChannel(IChatService svc, IChatChannel channel) => this._chatDisplay.OnJoinChannel(svc, channel);
        private void QueueOrSendOnTextMessageReceived(IChatService svc, IChatMessage msg) => this.QueueOrSendMessage(svc, msg, this.OnTextMesssageReceived);
        private void QueueOrSendOnClearMessage(IChatService svc, string messageId) => this.QueueOrSendMessage(svc, messageId, this.OnClearMessage);
        private void OnClearMessage(IChatService svc, string messageId) => this._chatDisplay.OnMessageCleared(messageId);
        private void QueueOrSendOnJoinChannel(IChatService svc, IChatChannel channel) => this.QueueOrSendMessage(svc, channel, this.OnJoinChannel);
        private void QueueOrSendOnClearChat(IChatService svc, string userId) => this.QueueOrSendMessage(svc, userId, this.OnClearChat);
        private void OnClearChat(IChatService svc, string userId) => this._chatDisplay.OnChatCleared(userId);
        private void QueueOrSendOnClearMessage(IChatService svc, string messageId) => this.QueueOrSendMessage(svc, messageId, this.OnClearMessage);
        private void QueueOrSendOnClearChat(IChatService svc, string userId) => QueueOrSendMessage(svc, userId, OnClearChat);
        private void OnClearChat(IChatService svc, string userId)
        {
            _chatDisplay.OnChatCleared(userId);
        }
        private void QueueOrSendOnClearChat(IChatService svc, string userId) => this.QueueOrSendMessage(svc, userId, this.OnClearChat);
        private void OnClearChat(IChatService svc, string userId) => this._chatDisplay.OnChatCleared(userId);
    }
}
