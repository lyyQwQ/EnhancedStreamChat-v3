using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Logging;
using ChatCore.Services;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Utilities;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Util;
using BS_Utils.Utilities;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Core.Services
{
    /// <summary>
    /// ChatManager 的 Zenject 服务实现，采用委托模式避免双重 ChatCore 实例
    /// </summary>
    public class ChatManagerService : IChatManager, IInitializable, IDisposable, ITickable
    {
        // 事件
        public event Action<IChatService, IChatMessage> OnTextMessageReceived;
        public event Action<IChatService, IChatChannel> OnJoinChannel;
        public event Action<IChatService, string> OnMessageCleared;
        public event Action<IChatService, string> OnChatCleared;
        public event Action<IChatService, IChatChannel, Dictionary<string, IChatResourceData>> OnChannelResourceDataCached;
        
        // 使用委托模式 - 不创建新的 ChatCore 实例
        private ChatManager _legacyChatManager => ChatManager.instance;
        private bool _initialized = false;
        private bool _disposed = false;
        
        // 属性
        public bool IsChatDisplayReady => _legacyChatManager?._chatDisplay != null;
        
        [Inject]
        public ChatManagerService()
        {
        }
        
        public void Initialize()
        {
            Logger.Info("[ChatManagerService] Initializing in delegation mode...");
            
            if (_initialized)
            {
                Logger.Warn("[ChatManagerService] Already initialized, skipping");
                return;
            }
            
            // 等待 ChatManager 单例初始化
            if (_legacyChatManager == null)
            {
                Logger.Error("[ChatManagerService] ChatManager singleton not available, deferring initialization");
                // 可以考虑启动协程等待
                return;
            }
            
            // 订阅单例的 ChatCore 事件并转发
            if (_legacyChatManager._chatServiceMultiplexer != null)
            {
                Logger.Info("[ChatManagerService] Subscribing to legacy ChatManager events");
                SubscribeToChatCoreEvents();
                _initialized = true;
            }
            else
            {
                Logger.Warn("[ChatManagerService] ChatServiceMultiplexer not ready, will retry");
            }
            
            Logger.Info("[ChatManagerService] Delegation mode initialization completed");
        }
        
        public void Tick()
        {
            // 在委托模式下，消息队列由 ChatManager 处理
            // 但是我们可能需要检查初始化状态
            if (!_initialized && _legacyChatManager?._chatServiceMultiplexer != null)
            {
                Logger.Info("[ChatManagerService] Late initialization detected, subscribing to events");
                Initialize();
            }
        }
        
        public void Dispose()
        {
            Logger.Info("[ChatManagerService] Disposing...");
            
            if (_disposed)
            {
                return;
            }
            
            // 取消订阅事件
            UnsubscribeFromChatCoreEvents();
            
            _disposed = true;
            _initialized = false;
            
            // 注意：不停止 ChatCore 服务，因为它由 ChatManager 管理
            Logger.Info("[ChatManagerService] Disposal completed (delegation mode)");
        }
        
        public void QueueMessage(Action action)
        {
            // 委托给 ChatManager 的队列处理
            if (_legacyChatManager != null)
            {
                _legacyChatManager.QueueOrSendMessage(action);
            }
            else
            {
                Logger.Warn("[ChatManagerService] Cannot queue message, ChatManager not available");
            }
        }
        
        public ChatCoreInstance GetChatCoreInstance()
        {
            return _legacyChatManager?._chatCoreInstance;
        }
        
        public ChatServiceMultiplexer GetChatServiceMultiplexer()
        {
            return _legacyChatManager?._chatServiceMultiplexer;
        }
        
        private void SubscribeToChatCoreEvents()
        {
            var multiplexer = _legacyChatManager?._chatServiceMultiplexer;
            if (multiplexer != null)
            {
                multiplexer.OnJoinChannel += BridgeOnJoinChannel;
                multiplexer.OnTextMessageReceived += BridgeOnTextMessageReceived;
                multiplexer.OnChatCleared += BridgeOnChatCleared;
                multiplexer.OnMessageCleared += BridgeOnMessageCleared;
                multiplexer.OnChannelResourceDataCached += BridgeOnChannelResourceDataCached;
                Logger.Info("[ChatManagerService] Successfully subscribed to ChatCore events via delegation");
            }
            else
            {
                Logger.Error("[ChatManagerService] ChatServiceMultiplexer not available for event subscription");
            }
        }
        
        private void UnsubscribeFromChatCoreEvents()
        {
            var multiplexer = _legacyChatManager?._chatServiceMultiplexer;
            if (multiplexer != null)
            {
                try
                {
                    multiplexer.OnJoinChannel -= BridgeOnJoinChannel;
                    multiplexer.OnTextMessageReceived -= BridgeOnTextMessageReceived;
                    multiplexer.OnChatCleared -= BridgeOnChatCleared;
                    multiplexer.OnMessageCleared -= BridgeOnMessageCleared;
                    multiplexer.OnChannelResourceDataCached -= BridgeOnChannelResourceDataCached;
                    Logger.Info("[ChatManagerService] Successfully unsubscribed from ChatCore events");
                }
                catch (Exception e)
                {
                    Logger.Error($"[ChatManagerService] Error unsubscribing from events: {e}");
                }
            }
        }
        
        // 桥接方法 - 直接转发事件
        private void BridgeOnJoinChannel(IChatService svc, IChatChannel channel)
        {
            Logger.Debug($"[ChatManagerService] Bridging OnJoinChannel event from {svc.DisplayName}");
            OnJoinChannel?.Invoke(svc, channel);
        }
        
        private void BridgeOnTextMessageReceived(IChatService svc, IChatMessage msg)
        {
            // Logger.Debug($"[ChatManagerService] Bridging text message from {svc.DisplayName}: {msg.Sender.UserName}");
            OnTextMessageReceived?.Invoke(svc, msg);
        }
        
        private void BridgeOnMessageCleared(IChatService svc, string messageId)
        {
            // Logger.Debug($"[ChatManagerService] Bridging message cleared: {messageId}");
            OnMessageCleared?.Invoke(svc, messageId);
        }
        
        private void BridgeOnChatCleared(IChatService svc, string userId)
        {
            Logger.Debug($"[ChatManagerService] Bridging chat cleared for user: {userId}");
            OnChatCleared?.Invoke(svc, userId);
        }
        
        private void BridgeOnChannelResourceDataCached(IChatService svc, IChatChannel channel, Dictionary<string, IChatResourceData> resources)
        {
            Logger.Debug($"[ChatManagerService] Bridging channel resource data cached for {channel.Id}");
            OnChannelResourceDataCached?.Invoke(svc, channel, resources);
        }
        
    }
}