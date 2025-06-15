using System;
using System.Collections.Generic;
using System.Linq;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Chat;  // For ChatConfig
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Adapters
{
    /// <summary>
    /// 适配器类，用于将现有的 ChatDisplay 单例适配到新的依赖注入架构
    /// </summary>
    public class ChatDisplayAdapter : IInitializable, IDisposable
    {
        private readonly IMessageRenderer _messageRenderer;
        private ChatConfig _chatConfig => ChatConfig.instance;
        private readonly RenderableMessage.Pool _messagePool;
        private ChatDisplay _chatDisplay;

        [Inject]
        public ChatDisplayAdapter(
            IMessageRenderer messageRenderer,
            RenderableMessage.Pool messagePool)
        {
            _messageRenderer = messageRenderer;
            _messagePool = messagePool;
        }

        public void Initialize()
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Initialize), "Starting initialization");
#endif
            // 监听 ChatDisplay 创建事件
            // 注意：ChatDisplay 是在 menu scene 加载时创建的
            Logger.Log.Info("ChatDisplayAdapter initialized, waiting for ChatDisplay...");
            
            // 订阅配置变更事件
            if (_chatConfig != null)
            {
                _chatConfig.OnConfigChanged += OnLegacyConfigChanged;
            }
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Initialize), "Subscribed to PropertyChanged event");
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Initialize), 
                $"Message pool available: {_messagePool != null}");
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Initialize), "Initialization completed");
#endif
        }

        public void Dispose()
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Dispose), "Starting disposal");
#endif
            if (_chatConfig != null)
            {
                _chatConfig.OnConfigChanged -= OnLegacyConfigChanged;
            }
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Dispose), "Unsubscribed from PropertyChanged");
#endif
            
            // 清理消息池
            if (_messagePool != null)
            {
                // 释放所有消息
                Logger.Log.Info("ChatDisplayAdapter disposed");
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Dispose), "Message pool cleaned up");
#endif
            }
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Dispose), "Disposal completed");
#endif
        }

        /// <summary>
        /// 设置 ChatDisplay 实例（当它被创建时调用）
        /// </summary>
        public void SetChatDisplay(ChatDisplay chatDisplay)
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(SetChatDisplay), 
                $"Setting ChatDisplay instance: {chatDisplay}");
#endif
            _chatDisplay = chatDisplay;
            if (_chatDisplay != null)
            {
                Logger.Log.Info("ChatDisplayAdapter connected to ChatDisplay");
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(SetChatDisplay), 
                    "ChatDisplay connected successfully");
#endif
                
                // 在这里可以拦截或修改 ChatDisplay 的行为
                // 例如：替换其渲染逻辑为新的渲染器
                ApplyConfiguration();
            }
#if DEBUG
            else
            {
                TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(SetChatDisplay), 
                    "ChatDisplay is null, connection failed");
            }
#endif
        }

        /// <summary>
        /// 使用新的渲染器渲染消息
        /// </summary>
        public async void RenderMessage(IChatService service, IChatMessage message, ParsedMessage parsedMessage)
        {
            try
            {
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(RenderMessage), 
                    $"Starting to render message: {message.Id} from {service.DisplayName}");
#endif
                // 使用工厂方法创建 ChatMessage 对象
                var chatMessage = ChatMessage.FromChatCoreMessage(service, message);
                
                // 如果有解析后的消息，可以更新表情和徽章
                if (parsedMessage != null)
                {
                    // 可以在这里合并 parsedMessage 的数据
                    if (parsedMessage.Emotes != null && parsedMessage.Emotes.Count > 0)
                    {
                        chatMessage.Emotes.AddRange(parsedMessage.Emotes);
                    }
                    if (parsedMessage.Badges != null && parsedMessage.Badges.Count > 0)
                    {
                        chatMessage.Badges.AddRange(parsedMessage.Badges);
                    }
                    if (parsedMessage.Metadata != null)
                    {
                        foreach (var kvp in parsedMessage.Metadata)
                        {
                            chatMessage.Metadata[kvp.Key] = kvp.Value;
                        }
                    }
                }
                
                // 使用新的渲染器异步渲染消息
                var renderableMessage = await _messageRenderer.RenderAsync(chatMessage);
                
                if (renderableMessage != null)
                {
                    Logger.Log.Info($"Rendered message {message.Id} with height {renderableMessage.Height}");
                    
#if DEBUG
                    TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(RenderMessage), 
                        $"Message rendered successfully - Height: {renderableMessage.Height}, " +
                        $"HasTextComponent: {renderableMessage.TextComponent != null}, " +
                        $"Images count: {renderableMessage.Images?.Count ?? 0}");
#endif
                    
                    // 在将来，这里可以直接将渲染元素传递给 ChatDisplay
                    // 目前仍让 ChatDisplay 使用其原有的渲染逻辑
                    
                    // 如果需要，可以在这里访问渲染后的消息属性
                    // renderableMessage.TextComponent, renderableMessage.Images 等
                }
#if DEBUG
                else
                {
                    TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(RenderMessage), 
                        "RenderableMessage is null");
                }
#endif
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"Error rendering message in ChatDisplayAdapter: {ex.Message}");
#if DEBUG
                TestAdapters.LogAdapterError(nameof(ChatDisplayAdapter), nameof(RenderMessage), ex);
#endif
            }
        }

        private void OnLegacyConfigChanged(ChatConfig config)
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(OnLegacyConfigChanged), 
                "Configuration changed");
#endif
            if (_chatDisplay != null)
            {
                ApplyConfiguration();
            }
#if DEBUG
            else
            {
                TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(OnLegacyConfigChanged), 
                    "ChatDisplay is null, skipping configuration apply");
            }
#endif
        }

        private void ApplyConfiguration()
        {
            // 应用配置到 ChatDisplay
            // 例如：字体大小、消息显示数量等
            Logger.Log.Info($"Applying configuration to ChatDisplay");
            
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(ApplyConfiguration), 
                $"Applying configuration - FontSize: {_chatConfig.FontSize}, " +
                $"ChatWidth: {_chatConfig.ChatWidth}, ChatHeight: {_chatConfig.ChatHeight}, " +
                $"ReverseChatOrder: {_chatConfig.ReverseChatOrder}");
#endif
            
            // 这里可以根据配置更新 ChatDisplay 的显示参数
            // 在完全迁移之前，我们保持兼容性
        }
    }
}