using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Chat;  // For ChatConfig
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace EnhancedStreamChat.Core.Services
{
    /// <summary>
    /// 消息渲染服务实现
    /// </summary>
    public class MessageRenderer : IMessageRenderer
    {
        private readonly IMessageParser _messageParser;
        private readonly IImageProvider _imageProvider;
        private readonly IFontProvider _fontProvider;
        private readonly ChatConfig _chatConfig = ChatConfig.instance;
        private readonly Chat.Adapters.Rendering.ChatMessageBuilderAdapter _messageBuilderAdapter;
        private readonly RenderableMessage.Pool _messagePool;
        
        public MessageRenderer(
            IMessageParser messageParser,
            IImageProvider imageProvider,
            IFontProvider fontProvider,
            Chat.Adapters.Rendering.ChatMessageBuilderAdapter messageBuilderAdapter,
            RenderableMessage.Pool messagePool)
        {
            _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
            _imageProvider = imageProvider ?? throw new ArgumentNullException(nameof(imageProvider));
            _fontProvider = fontProvider ?? throw new ArgumentNullException(nameof(fontProvider));
            _messageBuilderAdapter = messageBuilderAdapter ?? throw new ArgumentNullException(nameof(messageBuilderAdapter));
            _messagePool = messagePool ?? throw new ArgumentNullException(nameof(messagePool));
        }
        
        /// <summary>
        /// 异步渲染单条消息
        /// </summary>
        public async Task<RenderableMessage> RenderAsync(ChatMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            // 从池中获取可渲染消息对象
            var renderableMessage = _messagePool.Spawn();
            renderableMessage.Id = message.Id;
            renderableMessage.SourceMessage = message;
            renderableMessage.CreatedAt = DateTime.UtcNow;
            
            try
            {
                // 解析消息
                var parsedMessage = _messageParser.Parse(message.Message, message.Sender);
                
                // 构建富文本
                var richText = await BuildRichTextAsync(parsedMessage, message);
                
                // 设置文本
                if (renderableMessage.TextComponent != null)
                {
                    renderableMessage.TextComponent.text = richText;
                    renderableMessage.TextComponent.fontSize = _chatConfig.FontSize;
                    renderableMessage.TextComponent.color = _chatConfig.TextColor;
                    
                    // 如果是系统消息，使用不同的颜色
                    if (message.IsSystemMessage)
                    {
                        renderableMessage.TextComponent.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    }
                    
                    // 更新高度
                    renderableMessage.UpdateHeight();
                }
                
                // 检查是否包含动画元素
                renderableMessage.IsAnimated = parsedMessage.Emotes.Any(e => e.IsAnimated);
                
                return renderableMessage;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error rendering message: {ex}");
                // 发生错误时回收对象
                _messagePool.Despawn(renderableMessage);
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
        /// 更新已渲染消息（处理动画等）
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
                    // 消息完全淡出，可以回收
                    ReleaseMessage(message);
                }
            }
            
            // 更新动画表情
            if (message.IsAnimated)
            {
                foreach (var image in message.Images)
                {
                    if (image?.animStateUpdater != null && image.animStateUpdater.enabled)
                    {
                        // AnimationStateUpdater 会自动处理动画更新
                    }
                }
            }
        }
        
        /// <summary>
        /// 释放消息资源
        /// </summary>
        public void ReleaseMessage(RenderableMessage message)
        {
            if (message == null) return;
            
            // 回收到对象池
            _messagePool.Despawn(message);
        }
        
        /// <summary>
        /// 构建富文本字符串
        /// </summary>
        private async Task<string> BuildRichTextAsync(ParsedMessage parsedMessage, ChatMessage message)
        {
            // 使用 ChatMessageBuilderAdapter 来构建消息
            var fontProvider = _fontProvider as FontProvider;
            if (fontProvider == null)
            {
                Logger.Error("FontProvider is not the expected type");
                return message.Message;
            }
            
            var enhancedFont = fontProvider.GetFontInfo();
            if (enhancedFont == null)
            {
                Logger.Error("EnhancedFontInfo is null, cannot build rich text");
                return message.Message;
            }
            
            return await _messageBuilderAdapter.BuildMessageAsync(message, enhancedFont);
        }
        
        /// <summary>
        /// 获取或加载图片字符
        /// </summary>
        private async Task<int?> GetOrLoadImageCharacterAsync(string imageId, string imageUrl, bool isAnimated)
        {
            // 获取 EnhancedFontInfo
            var fontProvider = _fontProvider as FontProvider;
            if (fontProvider == null) return null;
            
            var enhancedFont = fontProvider.GetFontInfo();
            if (enhancedFont == null) return null;
            
            // 检查是否已经注册
            if (enhancedFont.TryGetCharacter(imageId, out var existingChar))
            {
                return (int)existingChar;
            }
            
            // 使用 ChatImageProviderAdapter 加载图片
            var imageProviderAdapter = _imageProvider as Chat.Adapters.Rendering.ChatImageProviderAdapter;
            if (imageProviderAdapter == null)
            {
                Logger.Error("ImageProvider is not ChatImageProviderAdapter");
                return null;
            }
            
            // 加载图片信息
            var imageInfo = await imageProviderAdapter.GetImageInfoAsync(imageId, imageUrl, isAnimated, 110);
            if (imageInfo == null)
            {
                Logger.Error($"Failed to load image info for {imageId}");
                return null;
            }
            
            // 注册到字体
            if (enhancedFont.TryRegisterImageInfo(imageInfo, out var character))
            {
                return (int)character;
            }
            
            Logger.Error($"Failed to register image {imageId} to font");
            return null;
        }
        
    }
}