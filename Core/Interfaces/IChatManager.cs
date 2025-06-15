using System;
using ChatCore.Interfaces;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 聊天管理器接口，负责管理 ChatCore 连接、消息路由和 ChatDisplay 生命周期
    /// 参考 v3 的 ICatCoreManager 设计
    /// </summary>
    public interface IChatManager : IDisposable
    {
        /// <summary>
        /// 文本消息接收事件
        /// </summary>
        event Action<IChatService, IChatMessage> OnTextMessageReceived;
        
        /// <summary>
        /// 加入频道事件
        /// </summary>
        event Action<IChatService, IChatChannel> OnJoinChannel;
        
        /// <summary>
        /// 消息清除事件
        /// </summary>
        event Action<IChatService, string> OnMessageCleared;
        
        /// <summary>
        /// 聊天清除事件
        /// </summary>
        event Action<IChatService, string> OnChatCleared;
        
        /// <summary>
        /// 频道资源数据缓存事件
        /// </summary>
        event Action<IChatService, IChatChannel, System.Collections.Generic.Dictionary<string, IChatResourceData>> OnChannelResourceDataCached;
        
        /// <summary>
        /// ChatDisplay 是否已准备好
        /// </summary>
        bool IsChatDisplayReady { get; }
        
        /// <summary>
        /// 初始化聊天管理器
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// 将消息操作加入队列
        /// </summary>
        void QueueMessage(Action action);
        
        /// <summary>
        /// 获取 ChatCore 实例（用于兼容性）
        /// </summary>
        ChatCore.ChatCoreInstance GetChatCoreInstance();
        
        /// <summary>
        /// 获取 ChatServiceMultiplexer（用于兼容性）
        /// </summary>
        ChatCore.Services.ChatServiceMultiplexer GetChatServiceMultiplexer();
    }
}