using System;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Models;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using EnhancedStreamChat.Adapters;
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
        
        // Legacy ChatManager instance
        private ChatManager _legacyChatManager;
        
        [Inject]
        public ChatManagerAdapter(
            IMessageParser messageParser,
            ChatDisplayAdapter chatDisplayAdapter)
        {
            _messageParser = messageParser;
            _chatDisplayAdapter = chatDisplayAdapter;
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
                
                // 注意：PreRenderMessage 方法仅供测试使用，可能会导致图形设备错误
                // 实际的消息渲染仍由 ChatDisplay 通过传统路径处理
                // 暂时注释掉以避免崩溃
                /*
                if (_chatDisplayAdapter != null)
                {
                    await _chatDisplayAdapter.PreRenderMessage(service, message, parsedMessage);
                    Logger.Debug($"Message {message.Id} passed to ChatDisplayAdapter for rendering");
                }
                else
                {
                    Logger.Warn("ChatDisplayAdapter is null, cannot render message");
                }
                */
                
                // TODO: 当 ChatDisplay 完全迁移到 Zenject 后，实现正确的消息传递机制
                // 目前 ChatDisplay 仍通过 ChatManager 的事件接收消息
            }
            catch (Exception ex)
            {
                Logger.Error($"Error processing chat message: {ex}");
            }
        }
        
        // 转换方法已经在 ChatMessage.FromChatCoreMessage 中实现
    }
}