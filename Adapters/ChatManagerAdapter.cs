using System;
using System.Threading.Tasks;
using ChatCore;
using ChatCore.Interfaces;
using ChatCore.Services;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Adapters
{
    /// <summary>
    /// 适配器类，用于将现有的 ChatManager 单例适配到新的依赖注入架构
    /// </summary>
    public class ChatManagerAdapter : IInitializable, IDisposable
    {
        private ChatConfig _chatConfig => ChatConfig.instance;
        private readonly IMessageParser _messageParser;
        private ChatManager _chatManager;
        private ChatServiceMultiplexer _multiplexer;

        [Inject]
        public ChatManagerAdapter(
            IMessageParser messageParser)
        {
            _messageParser = messageParser;
        }

        public void Initialize()
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Initialize), "Starting initialization");
#endif
            // 获取 ChatManager 单例及其 multiplexer
            _chatManager = ChatManager.instance;
            if (_chatManager != null)
            {
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Initialize), $"ChatManager instance found: {_chatManager}");
#endif
                _multiplexer = _chatManager._chatServiceMultiplexer;
                
                // 订阅 ChatCore 事件
                if (_multiplexer != null)
                {
                    _multiplexer.OnTextMessageReceived += OnTextMessageReceived;
#if DEBUG
                    TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Initialize), "Subscribed to OnTextMessageReceived event");
#endif
                }
#if DEBUG
                else
                {
                    TestAdapters.LogAdapterError(nameof(ChatManagerAdapter), nameof(Initialize), new NullReferenceException("ChatServiceMultiplexer is null"));
                }
#endif
                
                Logger.Log.Info("ChatManagerAdapter initialized");
            }
#if DEBUG
            else
            {
                TestAdapters.LogAdapterError(nameof(ChatManagerAdapter), nameof(Initialize), new NullReferenceException("ChatManager.instance is null"));
            }
#endif
            
            // ChatConfig 使用 OnConfigChanged 事件，暂时不需要订阅
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Initialize), "Initialization completed");
#endif
        }

        public void Dispose()
        {
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Dispose), "Starting disposal");
#endif
            // 取消订阅事件
            if (_multiplexer != null)
            {
                _multiplexer.OnTextMessageReceived -= OnTextMessageReceived;
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Dispose), "Unsubscribed from OnTextMessageReceived");
#endif
            }
            // ChatConfig 事件取消订阅
#if DEBUG
            TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(Dispose), "Disposal completed");
#endif
        }

        private void OnTextMessageReceived(IChatService service, IChatMessage message)
        {
            try
            {
#if DEBUG
                TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(OnTextMessageReceived), 
                    $"Received message from {service.DisplayName}: {message.Message}");
                TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(OnTextMessageReceived), 
                    $"Message details - ID: {message.Id}, Sender: {message.Sender?.DisplayName}, IsSystemMessage: {message.IsSystemMessage}");
#endif

                // 使用新的解析器处理消息
                var parsedMessage = _messageParser.Parse(message.Message, message.Sender);
                
                if (parsedMessage != null)
                {
                    Logger.Log.Info($"Message parsed with {parsedMessage.Segments?.Count ?? 0} segments");
#if DEBUG
                    TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(OnTextMessageReceived), 
                        $"Parsed message segments: {parsedMessage.Segments?.Count ?? 0}");
                    
                    if (parsedMessage.Segments != null)
                    {
                        foreach (var segment in parsedMessage.Segments)
                        {
                            string segmentInfo = segment.Type.ToString();
                            if (segment is TextSegment textSeg)
                            {
                                segmentInfo += $", Text={textSeg.Text}";
                            }
                            else if (segment is EmoteSegment emoteSeg)
                            {
                                segmentInfo += $", EmoteName={emoteSeg.EmoteName}";
                            }
                            TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(OnTextMessageReceived), 
                                $"Segment: {segmentInfo}");
                        }
                    }
                    
                    TestAdapters.LogAdapterState(nameof(ChatManagerAdapter), nameof(OnTextMessageReceived), 
                        $"Emotes count: {parsedMessage.Emotes?.Count ?? 0}, Badges count: {parsedMessage.Badges?.Count ?? 0}");
#endif
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"Error processing message in ChatManagerAdapter: {ex.Message}");
#if DEBUG
                TestAdapters.LogAdapterError(nameof(ChatManagerAdapter), nameof(OnTextMessageReceived), ex);
#endif
            }
        }

        // 配置变更处理已经移除，ChatConfig 使用不同的事件机制

    }
}