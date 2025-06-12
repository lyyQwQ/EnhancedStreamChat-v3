using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EnhancedStreamChat.Core.Models;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 负责将聊天消息渲染为可显示的UI元素
    /// </summary>
    public interface IMessageRenderer
    {
        /// <summary>
        /// 渲染一条聊天消息
        /// </summary>
        /// <param name="message">要渲染的聊天消息</param>
        /// <returns>渲染后的消息对象</returns>
        Task<RenderableMessage> RenderAsync(ChatMessage message);
        
        /// <summary>
        /// 批量渲染消息（性能优化）
        /// </summary>
        /// <param name="messages">要渲染的消息集合</param>
        /// <returns>渲染后的消息列表</returns>
        Task<IReadOnlyList<RenderableMessage>> RenderBatchAsync(IEnumerable<ChatMessage> messages);
        
        /// <summary>
        /// 更新已渲染消息（如动画表情）
        /// </summary>
        /// <param name="message">要更新的消息</param>
        /// <param name="deltaTime">时间间隔</param>
        void UpdateRenderedMessage(RenderableMessage message, float deltaTime);
        
        /// <summary>
        /// 释放渲染资源
        /// </summary>
        /// <param name="message">要释放的消息</param>
        void ReleaseMessage(RenderableMessage message);
    }
}