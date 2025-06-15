using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Bilibili;
using ChatCore.Utilities;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using UnityEngine;

namespace EnhancedStreamChat.Core.Services
{
    /// <summary>
    /// 消息解析服务实现，将 IChatMessage 转换为结构化的 ParsedMessage
    /// </summary>
    public class MessageParser : IMessageParser
    {
        private readonly List<ICustomParser> _customParsers = new List<ICustomParser>();
        private readonly Regex _urlRegex = new Regex(@"https?://(?:www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b(?:[-a-zA-Z0-9()@:%_\+.~#?&\/=]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly Regex _mentionRegex = new Regex(@"@([a-zA-Z0-9_]+)", RegexOptions.Compiled);
        
        public MessageParser()
        {
            // 注册默认的解析器
            RegisterDefaultParsers();
        }
        
        /// <summary>
        /// 解析聊天消息（从 ChatMessage 对象）
        /// </summary>
        public ParsedMessage Parse(ChatMessage message)
        {
            if (message == null || string.IsNullOrEmpty(message.Message))
            {
                return new ParsedMessage();
            }
            
            var parsed = new ParsedMessage();
            
            // 解析徽章
            if (message.Badges != null)
            {
                foreach (var badge in message.Badges)
                {
                    parsed.Badges.Add(badge); // 直接使用已有的 ChatBadge 对象
                }
            }
            
            // 解析表情
            if (message.Emotes != null)
            {
                var sortedEmotes = message.Emotes.OrderBy(e => e.StartIndex).ToList();
                
                foreach (var emote in sortedEmotes)
                {
                    // 设置表情类型（如果尚未设置）
                    if (emote.EmoteType == ChatEmoteType.SingleImage && emote.IsAnimated)
                    {
                        emote.EmoteType = InferEmoteTypeFromChatEmote(emote);
                    }
                    parsed.Emotes.Add(emote);
                }
            }
            
            // 解析消息片段
            parsed.Segments = ParseSegments(message.Message, parsed.Emotes);
            
            // 添加元数据
            if (message.IsSystemMessage)
            {
                parsed.Metadata["IsSystemMessage"] = true;
            }
            if (message.IsActionMessage)
            {
                parsed.Metadata["IsActionMessage"] = true;
            }
            if (message.IsMentioned)
            {
                parsed.Metadata["IsMentioned"] = true;
            }
            if (message.IsHighlighted)
            {
                parsed.Metadata["IsHighlighted"] = true;
            }
            
            // 保存原始消息引用
            parsed.Metadata["OriginalMessage"] = message;
            parsed.Metadata["UserId"] = message.Sender?.Id ?? "system";
            parsed.Metadata["UserName"] = message.Sender?.DisplayName ?? "System";
            
            return parsed;
        }
        
        /// <summary>
        /// 解析聊天消息（实现接口要求）
        /// </summary>
        public ParsedMessage Parse(string rawMessage, IChatUser user)
        {
            // 创建一个临时的 ChatMessage 对象
            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Message = rawMessage,
                Sender = user,
                Timestamp = DateTime.UtcNow,
                Service = null, // 从上下文获取
                IsSystemMessage = false,
                IsActionMessage = false,
                IsHighlighted = false,
                IsMentioned = false
            };
            
            // 尝试从用户的徽章创建 ChatBadge 列表
            if (user?.Badges != null)
            {
                foreach (var badge in user.Badges)
                {
                    message.Badges.Add(new ChatBadge
                    {
                        Id = badge.Id,
                        Name = badge.Name,
                        ImageUrl = badge.Uri
                    });
                }
            }
            
            return Parse(message);
        }
        
        /// <summary>
        /// 注册自定义解析器
        /// </summary>
        public void RegisterCustomParser(ICustomParser parser)
        {
            if (parser == null) throw new ArgumentNullException(nameof(parser));
            
            _customParsers.Add(parser);
            _customParsers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        
        /// <summary>
        /// 解析消息片段
        /// </summary>
        private List<MessageSegment> ParseSegments(string text, List<ChatEmote> emotes)
        {
            var segments = new List<MessageSegment>();
            var emoteMap = new Dictionary<int, ChatEmote>();
            
            // 构建表情位置映射
            foreach (var emote in emotes)
            {
                for (int i = emote.StartIndex; i <= emote.EndIndex; i++)
                {
                    emoteMap[i] = emote;
                }
            }
            
            int currentIndex = 0;
            while (currentIndex < text.Length)
            {
                // 检查是否是表情
                if (emoteMap.TryGetValue(currentIndex, out var emote))
                {
                    segments.Add(new EmoteSegment
                    {
                        EmoteId = emote.Id,
                        EmoteName = emote.Name,
                        EmoteUrl = emote.Uri,
                        IsAnimated = emote.IsAnimated
                    });
                    
                    // 跳过整个表情
                    currentIndex = emote.EndIndex + 1;
                    continue;
                }
                
                // 尝试自定义解析器
                bool parsed = false;
                foreach (var parser in _customParsers)
                {
                    if (parser.TryParse(text, currentIndex, out var segment, out var consumed))
                    {
                        segments.Add(segment);
                        currentIndex += consumed;
                        parsed = true;
                        break;
                    }
                }
                
                if (parsed) continue;
                
                // 检查 URL
                var urlMatch = _urlRegex.Match(text, currentIndex);
                if (urlMatch.Success && urlMatch.Index == currentIndex)
                {
                    segments.Add(new LinkSegment
                    {
                        Text = urlMatch.Value,
                        Url = urlMatch.Value
                    });
                    currentIndex += urlMatch.Length;
                    continue;
                }
                
                // 检查提及
                var mentionMatch = _mentionRegex.Match(text, currentIndex);
                if (mentionMatch.Success && mentionMatch.Index == currentIndex)
                {
                    segments.Add(new MentionSegment
                    {
                        Text = mentionMatch.Value,
                        Username = mentionMatch.Groups[1].Value
                    });
                    currentIndex += mentionMatch.Length;
                    continue;
                }
                
                // 收集普通文本
                int textStart = currentIndex;
                while (currentIndex < text.Length)
                {
                    // 检查是否遇到了特殊内容
                    if (emoteMap.ContainsKey(currentIndex))
                        break;
                    
                    if (_urlRegex.IsMatch(text, currentIndex))
                        break;
                    
                    if (_mentionRegex.IsMatch(text, currentIndex))
                        break;
                    
                    bool customParserMatch = false;
                    foreach (var parser in _customParsers)
                    {
                        if (parser.TryParse(text, currentIndex, out _, out _))
                        {
                            customParserMatch = true;
                            break;
                        }
                    }
                    if (customParserMatch) break;
                    
                    currentIndex++;
                }
                
                if (currentIndex > textStart)
                {
                    segments.Add(new TextSegment
                    {
                        Text = text.Substring(textStart, currentIndex - textStart)
                    });
                }
            }
            
            return segments;
        }
        
        /// <summary>
        /// 注册默认解析器
        /// </summary>
        private void RegisterDefaultParsers()
        {
            // 可以在这里注册 Emoji 解析器等
            RegisterCustomParser(new EmojiParser());
        }
        
        /// <summary>
        /// 推断表情类型（从 IChatEmote）
        /// </summary>
        private Core.Models.ChatEmoteType InferEmoteType(IChatEmote emote)
        {
            if (emote == null)
                return Core.Models.ChatEmoteType.SingleImage;
            
            // 基于 URI 模式推断
            if (!string.IsNullOrEmpty(emote.Uri))
            {
                var uri = emote.Uri.ToLower();
                
                // 检查是否包含帧数信息或动画相关的标识
                if (uri.Contains("_frame") || 
                    uri.Contains("animated") || 
                    uri.Contains("sprite") ||
                    uri.Contains(".gif"))
                {
                    return Core.Models.ChatEmoteType.SpriteSheet;
                }
                
                // 检查特定平台的动画表情模式
                // Twitch 动画表情通常以 .gif 结尾
                // BTTV/FFZ/7TV 动画表情可能包含特定标识
                if (emote.IsAnimated)
                {
                    return Core.Models.ChatEmoteType.SpriteSheet;
                }
            }
            
            // 默认为单张图片
            return Core.Models.ChatEmoteType.SingleImage;
        }
        
        /// <summary>
        /// 推断表情类型（从 ChatEmote）
        /// </summary>
        private Core.Models.ChatEmoteType InferEmoteTypeFromChatEmote(ChatEmote emote)
        {
            if (emote == null)
                return Core.Models.ChatEmoteType.SingleImage;
            
            // 基于 URI/ImageUrl 模式推断
            var url = emote.ImageUrl ?? emote.Uri;
            if (!string.IsNullOrEmpty(url))
            {
                var urlLower = url.ToLower();
                
                // 检查是否包含帧数信息或动画相关的标识
                if (urlLower.Contains("_frame") || 
                    urlLower.Contains("animated") || 
                    urlLower.Contains("sprite") ||
                    urlLower.Contains(".gif"))
                {
                    return Core.Models.ChatEmoteType.SpriteSheet;
                }
                
                // 如果标记为动画，则为精灵表
                if (emote.IsAnimated)
                {
                    return Core.Models.ChatEmoteType.SpriteSheet;
                }
            }
            
            // 默认为单张图片
            return Core.Models.ChatEmoteType.SingleImage;
        }
        
        /// <summary>
        /// 创建模拟消息（用于测试或特殊情况）
        /// </summary>
        private IChatMessage CreateMockMessage(string text, IChatUser user)
        {
            // 这里返回一个基本的消息实现
            return new MockChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Message = text,
                Sender = user,
                Emotes = new IChatEmote[0],
                Badges = new IChatBadge[0]
            };
        }
        
        /// <summary>
        /// 模拟聊天消息实现
        /// </summary>
        private class MockChatMessage : IChatMessage
        {
            private readonly Dictionary<string, string> _metadataDict = new Dictionary<string, string>();
            
            public string Id { get; set; }
            public bool IsSystemMessage => false;
            public bool IsActionMessage => false;
            public bool IsHighlighted => false;
            public bool IsPing => false;
            public string Message { get; set; }
            public IChatUser Sender { get; set; }
            public IChatChannel Channel { get; set; }
            public IChatEmote[] Emotes { get; set; }
            public IChatBadge[] Badges { get; set; }
            public JSONObject JSONObject => null;
            public string Type => "Mock";
            public DateTime Timestamp { get; set; } = DateTime.Now;
            public ReadOnlyDictionary<string, string> Metadata => new ReadOnlyDictionary<string, string>(_metadataDict);
            
            public JSONObject ToJson()
            {
                var json = new JSONObject();
                json["id"] = Id;
                json["message"] = Message;
                json["type"] = Type;
                json["timestamp"] = Timestamp.ToString("o");
                return json;
            }
        }
    }
    
    /// <summary>
    /// 链接片段
    /// </summary>
    public class LinkSegment : MessageSegment
    {
        public override MessageSegmentType Type => MessageSegmentType.Link;
        public string Text { get; set; }
        public string Url { get; set; }
    }
    
    /// <summary>
    /// 提及片段
    /// </summary>
    public class MentionSegment : MessageSegment
    {
        public override MessageSegmentType Type => MessageSegmentType.Mention;
        public string Text { get; set; }
        public string Username { get; set; }
    }
    
    /// <summary>
    /// Emoji 解析器
    /// </summary>
    public class EmojiParser : ICustomParser
    {
        public string Name => "Emoji";
        public int Priority => 10;
        
        // 简化的 emoji 正则表达式
        // 修复 Unicode 范围顺序问题，将代理对范围分开处理
        private readonly Regex _emojiRegex = new Regex(@"[\u263a-\u263b\u2600-\u27ff]|[\ud83c][\udc00-\udfff]|[\ud83d][\udc00-\udeff]", RegexOptions.Compiled);
        
        public bool TryParse(string text, int startIndex, out MessageSegment segment, out int consumedLength)
        {
            segment = null;
            consumedLength = 0;
            
            if (startIndex >= text.Length)
                return false;
            
            var match = _emojiRegex.Match(text, startIndex);
            if (match.Success && match.Index == startIndex)
            {
                segment = new EmojiSegment
                {
                    Unicode = match.Value,
                    ShortCode = $":emoji_{match.Value.GetHashCode()}:"
                };
                consumedLength = match.Length;
                return true;
            }
            
            return false;
        }
    }
}