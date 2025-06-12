using System;
using ChatCore.Interfaces;

namespace EnhancedStreamChat.Core.Events
{
    /// <summary>
    /// 聊天服务连接事件
    /// </summary>
    public class ChatConnectedEvent
    {
        /// <summary>
        /// 连接的聊天服务
        /// </summary>
        public IChatService Service { get; }
        
        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedAt { get; }
        
        /// <summary>
        /// 是否为重连
        /// </summary>
        public bool IsReconnect { get; }
        
        /// <summary>
        /// 创建聊天连接事件
        /// </summary>
        public ChatConnectedEvent(IChatService service, bool isReconnect = false)
        {
            Service = service;
            ConnectedAt = DateTime.UtcNow;
            IsReconnect = isReconnect;
        }
    }
    
    /// <summary>
    /// 聊天服务断开事件
    /// </summary>
    public class ChatDisconnectedEvent
    {
        /// <summary>
        /// 断开的聊天服务
        /// </summary>
        public IChatService Service { get; }
        
        /// <summary>
        /// 断开时间
        /// </summary>
        public DateTime DisconnectedAt { get; }
        
        /// <summary>
        /// 断开原因
        /// </summary>
        public string Reason { get; }
        
        /// <summary>
        /// 是否会自动重连
        /// </summary>
        public bool WillReconnect { get; }
        
        /// <summary>
        /// 创建聊天断开事件
        /// </summary>
        public ChatDisconnectedEvent(
            IChatService service, 
            string reason = null, 
            bool willReconnect = true)
        {
            Service = service;
            DisconnectedAt = DateTime.UtcNow;
            Reason = reason;
            WillReconnect = willReconnect;
        }
    }
    
    /// <summary>
    /// 聊天服务错误事件
    /// </summary>
    public class ChatErrorEvent
    {
        /// <summary>
        /// 发生错误的服务
        /// </summary>
        public IChatService Service { get; }
        
        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; }
        
        /// <summary>
        /// 异常对象（如果有）
        /// </summary>
        public Exception Exception { get; }
        
        /// <summary>
        /// 错误级别
        /// </summary>
        public ErrorLevel Level { get; }
        
        /// <summary>
        /// 创建聊天错误事件
        /// </summary>
        public ChatErrorEvent(
            IChatService service,
            string errorMessage,
            Exception exception = null,
            ErrorLevel level = ErrorLevel.Warning)
        {
            Service = service;
            ErrorMessage = errorMessage;
            Exception = exception;
            Level = level;
        }
    }
    
    /// <summary>
    /// 错误级别
    /// </summary>
    public enum ErrorLevel
    {
        /// <summary>
        /// 信息性错误
        /// </summary>
        Info,
        
        /// <summary>
        /// 警告级别错误
        /// </summary>
        Warning,
        
        /// <summary>
        /// 错误级别
        /// </summary>
        Error,
        
        /// <summary>
        /// 严重错误
        /// </summary>
        Critical
    }
}