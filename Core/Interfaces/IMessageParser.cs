using System.Collections.Generic;
using ChatCore.Interfaces;
using EnhancedStreamChat.Core.Models;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 负责解析聊天消息，提取表情、徽章等元素
    /// </summary>
    public interface IMessageParser
    {
        /// <summary>
        /// 解析聊天消息，提取表情、徽章等元素
        /// </summary>
        /// <param name="rawMessage">原始消息文本</param>
        /// <param name="user">发送消息的用户</param>
        /// <returns>解析后的消息数据</returns>
        ParsedMessage Parse(string rawMessage, IChatUser user);
        
        /// <summary>
        /// 注册自定义解析器（如 BTTV、FFZ、7TV）
        /// </summary>
        /// <param name="parser">自定义解析器</param>
        void RegisterCustomParser(ICustomParser parser);
    }
    
    /// <summary>
    /// 自定义解析器接口
    /// </summary>
    public interface ICustomParser
    {
        /// <summary>
        /// 解析器名称
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// 解析优先级（数字越大越先执行）
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 尝试解析消息片段
        /// </summary>
        bool TryParse(string text, int startIndex, out MessageSegment segment, out int consumedLength);
    }
    
    /// <summary>
    /// 解析后的消息数据
    /// </summary>
    public class ParsedMessage
    {
        public List<MessageSegment> Segments { get; set; } = new List<MessageSegment>();
        public List<ChatEmote> Emotes { get; set; } = new List<ChatEmote>();
        public List<ChatBadge> Badges { get; set; } = new List<ChatBadge>();
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// 消息片段基类
    /// </summary>
    public abstract class MessageSegment
    {
        public abstract MessageSegmentType Type { get; }
    }
    
    /// <summary>
    /// 消息片段类型
    /// </summary>
    public enum MessageSegmentType
    {
        Text,
        Emote,
        Emoji,
        Mention,
        Link
    }
    
    /// <summary>
    /// 文本片段
    /// </summary>
    public class TextSegment : MessageSegment
    {
        public override MessageSegmentType Type => MessageSegmentType.Text;
        public string Text { get; set; }
        public UnityEngine.Color Color { get; set; } = UnityEngine.Color.white;
    }
    
    /// <summary>
    /// 表情片段
    /// </summary>
    public class EmoteSegment : MessageSegment
    {
        public override MessageSegmentType Type => MessageSegmentType.Emote;
        public string EmoteId { get; set; }
        public string EmoteName { get; set; }
        public string EmoteUrl { get; set; }
        public bool IsAnimated { get; set; }
    }
    
    /// <summary>
    /// Emoji片段
    /// </summary>
    public class EmojiSegment : MessageSegment
    {
        public override MessageSegmentType Type => MessageSegmentType.Emoji;
        public string Unicode { get; set; }
        public string ShortCode { get; set; }
    }
}