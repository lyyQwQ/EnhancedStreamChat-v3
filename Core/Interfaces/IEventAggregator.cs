using System;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 事件聚合器接口，用于解耦的事件发布/订阅
    /// </summary>
    public interface IEventAggregator
    {
        /// <summary>
        /// 发布事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="eventData">事件数据</param>
        void Publish<TEvent>(TEvent eventData) where TEvent : class;
        
        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <returns>用于取消订阅的令牌</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
        
        /// <summary>
        /// 订阅事件（弱引用）
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">事件处理器</param>
        /// <returns>用于取消订阅的令牌</returns>
        IDisposable SubscribeWeak<TEvent>(Action<TEvent> handler) where TEvent : class;
        
        /// <summary>
        /// 取消订阅事件
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="handler">要取消的事件处理器</param>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
        
        /// <summary>
        /// 清除所有订阅
        /// </summary>
        void Clear();
    }
    
    /// <summary>
    /// 事件订阅令牌
    /// </summary>
    public interface IEventSubscription : IDisposable
    {
        /// <summary>
        /// 订阅是否仍然有效
        /// </summary>
        bool IsAlive { get; }
        
        /// <summary>
        /// 暂停订阅
        /// </summary>
        void Pause();
        
        /// <summary>
        /// 恢复订阅
        /// </summary>
        void Resume();
        
        /// <summary>
        /// 订阅是否已暂停
        /// </summary>
        bool IsPaused { get; }
    }
}