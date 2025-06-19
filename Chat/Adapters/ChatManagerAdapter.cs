using System;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Models;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using EnhancedStreamChat.Adapters;
using EnhancedStream_139.Core.Services;
using Zenject;

namespace EnhancedStreamChat.Chat.Adapters
{
    /// <summary>
    /// Adapter that bridges the legacy ChatManager with the new architecture
    /// </summary>
    public class ChatManagerAdapter : IInitializable, IDisposable
    {
        private ChatConfig _chatConfig => ChatConfig.instance;
        private readonly IMessageParser _messageParser;
        private readonly ChatDisplayAdapter _chatDisplayAdapter;
        private readonly MessageRenderQueue _messageRenderQueue;
        
        // Legacy ChatManager instance
        private ChatManager _legacyChatManager;
        
        [Inject]
        public ChatManagerAdapter(
            IMessageParser messageParser,
            ChatDisplayAdapter chatDisplayAdapter,
            MessageRenderQueue messageRenderQueue)
        {
            _messageParser = messageParser;
            _chatDisplayAdapter = chatDisplayAdapter;
            _messageRenderQueue = messageRenderQueue;
        }
        
        public void Initialize()
        {
            Logger.Info("Initializing ChatManagerAdapter");
            
            // 获取现有的 ChatManager 单例
            _legacyChatManager = ChatManager.instance;
            
            if (_legacyChatManager == null)
            {
                Logger.Error("ChatManager instance is null!");
                return;
            }
            
            // 订阅 ChatCore 事件并转换为新架构的消息格式
            var chatCore = _legacyChatManager._chatServiceMultiplexer;
            if (chatCore != null)
            {
                chatCore.OnTextMessageReceived += OnChatCoreMessageReceived;
                Logger.Info("Successfully subscribed to ChatCore events");
            }
            else
            {
                Logger.Warn("ChatCore is not available yet");
            }
        }
        
        public void Dispose()
        {
            if (_legacyChatManager?._chatServiceMultiplexer != null)
            {
                _legacyChatManager._chatServiceMultiplexer.OnTextMessageReceived -= OnChatCoreMessageReceived;
            }
        }
        
        private async void OnChatCoreMessageReceived(IChatService service, IChatMessage message)
        {
            try
            {
                // 使用 ChatMessage 的静态方法转换
                var chatMessage = ChatMessage.FromChatCoreMessage(service, message);
                
                // 使用新的消息解析器（同步方法）
                var parsedMessage = _messageParser.Parse(chatMessage.Message, chatMessage.Sender);
                
                // 使用消息渲染队列确保在主线程安全渲染
                if (_messageRenderQueue != null && _chatDisplayAdapter != null)
                {
                    _messageRenderQueue.EnqueueMessage(new RenderRequest
                    {
                        Service = service,
                        Message = message,
                        ParsedMessage = parsedMessage,
                        SuccessCallback = async (renderableMessage) =>
                        {
                            // 渲染成功后的处理
                            Logger.Debug($"Message {message.Id} rendered successfully");
                        },
                        ErrorCallback = (ex) =>
                        {
                            Logger.Error($"Failed to render message {message.Id}: {ex}");
                        }
                    });
                    
                    Logger.Debug($"Message {message.Id} enqueued for rendering");
                }
                else
                {
                    Logger.Warn("MessageRenderQueue or ChatDisplayAdapter is null, cannot render message");
                }
                
                // 注意：ChatDisplay 仍通过 ChatManager 的事件接收消息
                // 这个适配器是新架构的一部分，用于测试和逐步迁移
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing chat message: {ex}");
            }
        }
        
        // 转换方法已经在 ChatMessage.FromChatCoreMessage 中实现
    }
}