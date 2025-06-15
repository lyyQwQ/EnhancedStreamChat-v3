using System;
using System.Collections.Generic;
using ChatCore.Interfaces;

namespace EnhancedStreamChat.Core.Models
{
    /// <summary>
    /// 聊天消息数据模型
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// 消息唯一标识
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// 消息内容
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// 发送者信息
        /// </summary>
        public IChatUser Sender { get; set; }
        
        /// <summary>
        /// 消息时间戳
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// 消息来源服务（Twitch、Bilibili等）
        /// </summary>
        public IChatService Service { get; set; }
        
        /// <summary>
        /// 是否为系统消息
        /// </summary>
        public bool IsSystemMessage { get; set; }
        
        /// <summary>
        /// 是否为动作消息（/me）
        /// </summary>
        public bool IsActionMessage { get; set; }
        
        /// <summary>
        /// 是否被高亮
        /// </summary>
        public bool IsHighlighted { get; set; }
        
        /// <summary>
        /// 是否提及当前用户
        /// </summary>
        public bool IsMentioned { get; set; }
        
        /// <summary>
        /// 消息中的表情列表
        /// </summary>
        public List<ChatEmote> Emotes { get; set; } = new List<ChatEmote>();
        
        /// <summary>
        /// 用户徽章列表
        /// </summary>
        public List<ChatBadge> Badges { get; set; } = new List<ChatBadge>();
        
        /// <summary>
        /// 额外的元数据
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// 从ChatCore消息创建
        /// </summary>
        public static ChatMessage FromChatCoreMessage(IChatService service, IChatMessage coreMessage)
        {
            var message = new ChatMessage
            {
                Id = coreMessage.Id,
                Message = coreMessage.Message,
                Sender = coreMessage.Sender,
                Timestamp = DateTime.UtcNow,
                Service = service,
                IsSystemMessage = coreMessage.IsSystemMessage,
                IsActionMessage = coreMessage.IsActionMessage,
                IsHighlighted = coreMessage.IsHighlighted,
                IsMentioned = false // 需要根据实际逻辑判断
            };
            
            // 转换表情
            if (coreMessage.Emotes != null)
            {
                foreach (var emote in coreMessage.Emotes)
                {
                    var chatEmote = new ChatEmote
                    {
                        Id = emote.Id,
                        Name = emote.Name,
                        ImageUrl = emote.Uri,
                        IsAnimated = emote.IsAnimated,
                        StartIndex = emote.StartIndex,
                        EndIndex = emote.EndIndex
                    };
                    
                    // 推断表情类型
                    if (emote.IsAnimated || 
                        (!string.IsNullOrEmpty(emote.Uri) && 
                         (emote.Uri.ToLower().Contains(".gif") || 
                          emote.Uri.ToLower().Contains("animated") ||
                          emote.Uri.ToLower().Contains("_frame"))))
                    {
                        chatEmote.EmoteType = ChatEmoteType.SpriteSheet;
                    }
                    else
                    {
                        chatEmote.EmoteType = ChatEmoteType.SingleImage;
                    }
                    
                    message.Emotes.Add(chatEmote);
                }
            }
            
            // 转换徽章
            if (coreMessage.Sender?.Badges != null)
            {
                foreach (var badge in coreMessage.Sender.Badges)
                {
                    message.Badges.Add(new ChatBadge
                    {
                        Id = badge.Id,
                        Name = badge.Name,
                        ImageUrl = badge.Uri
                    });
                }
            }
            
            return message;
        }
    }
}