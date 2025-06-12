using EnhancedStreamChat.Core.Models;

namespace EnhancedStreamChat.Core.Events
{
    /// <summary>
    /// 聊天消息接收事件
    /// </summary>
    public class MessageReceivedEvent
    {
        /// <summary>
        /// 接收到的聊天消息
        /// </summary>
        public ChatMessage Message { get; }
        
        /// <summary>
        /// 是否为历史消息（重连时接收的）
        /// </summary>
        public bool IsHistorical { get; }
        
        /// <summary>
        /// 创建消息接收事件
        /// </summary>
        /// <param name="message">聊天消息</param>
        /// <param name="isHistorical">是否为历史消息</param>
        public MessageReceivedEvent(ChatMessage message, bool isHistorical = false)
        {
            Message = message;
            IsHistorical = isHistorical;
        }
    }
}