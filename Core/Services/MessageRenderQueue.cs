using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using EnhancedStreamChat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using EnhancedStreamChat.Core.Services;
using EnhancedStreamChat.Adapters;
using EnhancedStreamChat.Utilities;
using EnhancedStream_139.Utils;
using UnityEngine;
using Zenject;

namespace EnhancedStream_139.Core.Services
{
    /// <summary>
    /// 消息渲染队列，确保所有渲染操作在主线程执行
    /// </summary>
    public class MessageRenderQueue : ITickable, IInitializable, IDisposable
    {
        private readonly ConcurrentQueue<RenderRequest> _pendingMessages = new ConcurrentQueue<RenderRequest>();
        private readonly IMessageRenderer _messageRenderer;
        private readonly ChatDisplayAdapter _chatDisplayAdapter;
        private bool _isProcessing;
        private const int MaxBatchSize = 10; // 每帧最多处理的消息数

        public MessageRenderQueue(
            IMessageRenderer messageRenderer,
            ChatDisplayAdapter chatDisplayAdapter)
        {
            _messageRenderer = messageRenderer;
            _chatDisplayAdapter = chatDisplayAdapter;
        }

        public void Initialize()
        {
            EnhancedStreamChat.Logger.Info("[MessageRenderQueue] Initialized");
            MainThreadDispatcher.EnsureInitialized();
        }

        public void Dispose()
        {
            _pendingMessages.Clear();
            EnhancedStreamChat.Logger.Info("[MessageRenderQueue] Disposed");
        }

        /// <summary>
        /// 将消息加入渲染队列
        /// </summary>
        public void EnqueueMessage(RenderRequest request)
        {
            if (request == null)
            {
                EnhancedStreamChat.Logger.Warn("[MessageRenderQueue] Attempted to enqueue null request");
                return;
            }

            _pendingMessages.Enqueue(request);
            EnhancedStreamChat.Logger.Debug($"[MessageRenderQueue] Enqueued message");
        }

        /// <summary>
        /// 每帧调用，处理待渲染的消息
        /// </summary>
        public void Tick()
        {
            if (_isProcessing || _pendingMessages.IsEmpty)
                return;

            _isProcessing = true;

            try
            {
                ProcessPendingMessages();
            }
            catch (Exception ex)
            {
                EnhancedStreamChat.Logger.Error($"[MessageRenderQueue] Error in Tick: {ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void ProcessPendingMessages()
        {
            var processed = 0;

            while (processed < MaxBatchSize && _pendingMessages.TryDequeue(out var request))
            {
                try
                {
                    ProcessMessage(request);
                    processed++;
                }
                catch (Exception ex)
                {
                    EnhancedStreamChat.Logger.Error($"[MessageRenderQueue] Error processing message: {ex}");
                    
                    // 如果有回调，通知失败
                    request.ErrorCallback?.Invoke(ex);
                }
            }

            if (processed > 0)
            {
                EnhancedStreamChat.Logger.Debug($"[MessageRenderQueue] Processed {processed} messages, {_pendingMessages.Count} remaining");
            }
        }

        private async void ProcessMessage(RenderRequest request)
        {
            try
            {
                // 确保在主线程
                if (!MainThreadDispatcher.IsMainThread)
                {
                    await MainThreadDispatcher.InvokeAsync(() => ProcessMessage(request));
                    return;
                }

                // 执行预渲染
                var renderableMessage = await _chatDisplayAdapter.PreRenderMessage(
                    request.Service, 
                    request.Message, 
                    request.ParsedMessage);

                // 通知成功
                if (request.SuccessCallback != null)
                {
                    await request.SuccessCallback(renderableMessage);
                }
            }
            catch (Exception ex)
            {
                EnhancedStreamChat.Logger.Error($"[MessageRenderQueue] Error in ProcessMessage: {ex}");
                request.ErrorCallback?.Invoke(ex);
            }
        }
    }

    /// <summary>
    /// 渲染请求
    /// </summary>
    public class RenderRequest
    {
        public IChatService Service { get; set; }
        public IChatMessage Message { get; set; }
        public ParsedMessage ParsedMessage { get; set; }
        public Func<RenderableMessage, Task> SuccessCallback { get; set; }
        public Action<Exception> ErrorCallback { get; set; }
        public DateTime EnqueueTime { get; } = DateTime.UtcNow;

        public TimeSpan Age => DateTime.UtcNow - EnqueueTime;
    }
}