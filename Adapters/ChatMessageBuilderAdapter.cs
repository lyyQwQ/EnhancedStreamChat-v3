using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Models;
using ImageRect = ChatCore.Models.ImageRect;
using ChatCore.Utilities;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using UnityEngine;

namespace EnhancedStreamChat.Adapters
{
    /// <summary>
    /// 适配器：将旧的 ChatMessageBuilder 功能适配到新的 IMessageRenderer 接口
    /// </summary>
    public class ChatMessageBuilderAdapter : IMessageRenderer
    {
        private readonly IMessageParser _messageParser;
        private readonly IImageProvider _imageProvider;
        private readonly IFontProvider _fontProvider;
        private readonly ChatImageProvider _legacyImageProvider;
        
        public ChatMessageBuilderAdapter(
            IMessageParser messageParser,
            IImageProvider imageProvider,
            IFontProvider fontProvider)
        {
            _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
            _imageProvider = imageProvider ?? throw new ArgumentNullException(nameof(imageProvider));
            _fontProvider = fontProvider ?? throw new ArgumentNullException(nameof(fontProvider));
            
            // 获取旧的图片提供者实例
            _legacyImageProvider = ChatImageProvider.instance;
        }
        
        /// <summary>
        /// 异步渲染单条消息
        /// </summary>
        public async Task<RenderableMessage> RenderAsync(ChatMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            try
            {
                // 获取 EnhancedFontInfo
                var tmpFont = ESCFontManager.instance?.MainFont;
                if (tmpFont == null)
                {
                    throw new InvalidOperationException("ESCFontManager.MainFont is not available");
                }
                var font = new EnhancedFontInfo(tmpFont);
                
                // 转换 ChatMessage 到 IChatMessage
                var chatCoreMessage = ConvertToChatCoreMessage(message);
                
                // 准备图片（始终准备，PreCacheAnimatedEmotes 只影响预缓存）
                ChatMessageBuilder.PrepareImages(chatCoreMessage, font);
                
                // 构建消息文本
                var richText = await ChatMessageBuilder.BuildMessage(chatCoreMessage, font);
                
                // 创建 RenderableMessage
                var renderableMessage = new RenderableMessage
                {
                    Id = message.Id,
                    SourceMessage = message,
                    CreatedAt = DateTime.UtcNow
                };
                
                // 创建 GameObject 和组件
                var go = new GameObject($"ChatMessage_{message.Id}");
                renderableMessage.GameObject = go;
                
                // 添加 RectTransform
                var rectTransform = go.AddComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(ChatConfig.instance.ChatWidth, 0);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                
                // 添加文本组件
                var textComponent = go.AddComponent<EnhancedTextMeshProUGUI>();
                textComponent.text = richText;
                textComponent.fontSize = ChatConfig.instance.FontSize;
                textComponent.color = message.IsSystemMessage 
                    ? new Color(0.7f, 0.7f, 0.7f, 1f) 
                    : ChatConfig.instance.TextColor;
                textComponent.font = tmpFont;
                textComponent.enableWordWrapping = true;
                textComponent.richText = true;
                
                renderableMessage.TextComponent = textComponent;
                
                // 检查是否包含动画元素
                renderableMessage.IsAnimated = message.Emotes?.Any(e => e.IsAnimated) ?? false;
                
                // 更新高度
                renderableMessage.UpdateHeight();
                
                return renderableMessage;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in ChatMessageBuilderAdapter.RenderAsync: {ex}");
                throw;
            }
        }
        
        /// <summary>
        /// 批量渲染消息
        /// </summary>
        public async Task<IReadOnlyList<RenderableMessage>> RenderBatchAsync(IEnumerable<ChatMessage> messages)
        {
            var tasks = messages.Select(RenderAsync).ToList();
            var results = await Task.WhenAll(tasks);
            return results;
        }
        
        /// <summary>
        /// 更新已渲染消息
        /// </summary>
        public void UpdateRenderedMessage(RenderableMessage message, float deltaTime)
        {
            if (message == null) return;
            
            // 更新淡出动画
            if (message.IsFadingOut)
            {
                bool fullyFaded = message.UpdateFade(deltaTime);
                if (fullyFaded)
                {
                    ReleaseMessage(message);
                }
            }
            
            // 动画更新由 AnimationStateUpdater 自动处理
        }
        
        /// <summary>
        /// 释放消息资源
        /// </summary>
        public void ReleaseMessage(RenderableMessage message)
        {
            if (message == null) return;
            
            // 清理资源
            message.OnDespawn();
            
            // 销毁 GameObject
            if (message.GameObject != null)
            {
                UnityEngine.Object.Destroy(message.GameObject);
            }
        }
        
        /// <summary>
        /// 转换 ChatMessage 到 IChatMessage
        /// </summary>
        private IChatMessage ConvertToChatCoreMessage(ChatMessage message)
        {
            return new ChatCoreMessageWrapper(message);
        }
        
        /// <summary>
        /// ChatMessage 到 IChatMessage 的包装器
        /// </summary>
        private class ChatCoreMessageWrapper : IChatMessage
        {
            private readonly ChatMessage _message;
            
            public ChatCoreMessageWrapper(ChatMessage message)
            {
                _message = message;
            }
            
            public string Id => _message.Id;
            public bool IsSystemMessage => _message.IsSystemMessage;
            public bool IsActionMessage => _message.IsActionMessage;
            public bool IsHighlighted => _message.IsHighlighted;
            public bool IsPing => _message.IsMentioned;
            public string Message => _message.Message;
            public IChatUser Sender => _message.Sender;
            public IChatChannel Channel => null; // ChatMessage 没有 Channel 属性
            
            public IChatEmote[] Emotes => _message.Emotes?.Select(e => new ChatCoreEmoteWrapper(e)).ToArray() ?? new IChatEmote[0];
            public IChatBadge[] Badges => _message.Sender?.Badges?.ToArray() ?? new IChatBadge[0];
            
            public JSONObject JSONObject => null;
            public string Type => "Enhanced";
            public DateTime Timestamp => _message.Timestamp;
            public System.Collections.ObjectModel.ReadOnlyDictionary<string, string> Metadata => 
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new System.Collections.Generic.Dictionary<string, string>());
            
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
        
        /// <summary>
        /// ChatEmote 到 IChatEmote 的包装器
        /// </summary>
        private class ChatCoreEmoteWrapper : IChatEmote
        {
            private readonly ChatEmote _emote;
            
            public ChatCoreEmoteWrapper(ChatEmote emote)
            {
                _emote = emote;
            }
            
            public string Id => _emote.Id;
            public string Name => _emote.Name;
            public string Uri => _emote.Uri;
            public int StartIndex => _emote.StartIndex;
            public int EndIndex => _emote.EndIndex;
            public int Bits => 0; // ChatEmote 没有 Bits 属性
            public string Color => "#FFFFFF"; // 默认颜色
            public bool IsAnimated => _emote.IsAnimated;
            public EmoteType Type => EmoteType.SingleImage; // 简化处理
            public ChatCore.Models.ImageRect UVs => ChatCore.Models.ImageRect.None;
            
            public JSONObject ToJson()
            {
                var json = new JSONObject();
                json["id"] = Id;
                json["name"] = Name;
                json["uri"] = Uri;
                json["isAnimated"] = IsAnimated;
                return json;
            }
        }
    }
}