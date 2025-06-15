using System;
using System.Threading.Tasks;
using ChatCore.Interfaces;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 聊天显示接口，定义聊天UI的核心功能
    /// </summary>
    public interface IChatDisplay
    {
        /// <summary>
        /// 创建并显示聊天消息
        /// </summary>
        /// <param name="message">聊天消息</param>
        void CreateMessage(IChatMessage message);
        
        /// <summary>
        /// 创建并显示聊天消息（异步版本）
        /// </summary>
        /// <param name="message">聊天消息</param>
        Task CreateMessageAsync(IChatMessage message);
        
        /// <summary>
        /// 清除指定ID的消息
        /// </summary>
        /// <param name="messageId">消息ID</param>
        void ClearMessage(string messageId);
        
        /// <summary>
        /// 清除指定用户的所有消息
        /// </summary>
        /// <param name="userId">用户ID</param>
        void ClearUserMessages(string userId);
        
        /// <summary>
        /// ChatDisplay 是否已准备就绪
        /// </summary>
        bool IsReady { get; }
        
        /// <summary>
        /// 更新浮动屏幕位置（菜单/游戏场景切换时）
        /// </summary>
        void UpdateFloatingScreenPosition();
    }
}