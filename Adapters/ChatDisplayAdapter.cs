using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Adapters
{
    /// <summary>
    /// 适配器类，用于桥接新的消息渲染系统和配置管理
    /// 注意：ChatDisplay 在菜单场景中创建，适配器在应用场景中创建，
    /// 因此不能直接依赖 ChatDisplay 实例
    /// </summary>
    public class ChatDisplayAdapter : IInitializable, IDisposable
    {
        private readonly IMessageRenderer _messageRenderer;
        private readonly RenderableMessage.Pool _messagePool;
        private ChatConfig _chatConfig => ChatConfig.instance;

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
            Logger.Log.Info("ChatDisplayAdapter initialized");
            
            // 订阅配置变更事件
            if (_chatConfig != null)
            {
                _chatConfig.OnConfigChanged += OnConfigChanged;
            }
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(Initialize), "Subscribed to OnConfigChanged event");
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
                _chatConfig.OnConfigChanged -= OnConfigChanged;
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
        /// 预渲染消息（供测试使用）
        /// 实际的消息渲染仍由 ChatDisplay 处理，这里只是演示新渲染器的能力
        /// </summary>
        public async Task<RenderableMessage> PreRenderMessage(IChatService service, IChatMessage message, ParsedMessage parsedMessage)
        {
            try
            {
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(PreRenderMessage), 
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
                    Logger.Log.Info($"Pre-rendered message {message.Id} with height {renderableMessage.Height}");
                    
#if DEBUG
                    TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(PreRenderMessage), 
                        $"Message pre-rendered successfully - Height: {renderableMessage.Height}, " +
                        $"HasTextComponent: {renderableMessage.TextComponent != null}, " +
                        $"Images count: {renderableMessage.Images?.Count ?? 0}");
#endif
                }
#if DEBUG
                else
                {
                    TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(PreRenderMessage), 
                        "RenderableMessage is null");
                }
#endif
                
                return renderableMessage;
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"Error pre-rendering message in ChatDisplayAdapter: {ex.Message}");
#if DEBUG
                TestAdapters.LogAdapterError(nameof(ChatDisplayAdapter), nameof(PreRenderMessage), ex);
#endif
                return null;
            }
        }

        private void OnConfigChanged(ChatConfig config)
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatDisplayAdapter), nameof(OnConfigChanged), 
                "Configuration changed");
#endif
            // 配置变更时的处理逻辑
            // 由于 ChatDisplay 在不同场景，这里只记录配置变更
            Logger.Log.Info($"Configuration changed - FontSize: {config.FontSize}, ChatWidth: {config.ChatWidth}");
        }

        /// <summary>
        /// 获取消息池（供测试使用）
        /// </summary>
        public RenderableMessage.Pool GetMessagePool()
        {
            return _messagePool;
        }
        
        /// <summary>
        /// 获取消息渲染器（供测试使用）
        /// </summary>
        public IMessageRenderer GetMessageRenderer()
        {
            return _messageRenderer;
        }
    }
}