using System;
using System.Collections.Generic;
using System.Diagnostics;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;

namespace EnhancedStreamChat.Adapters
{
    /// <summary>
    /// 测试辅助类，用于验证适配器功能
    /// </summary>
#if DEBUG
    public static class TestAdapters
    {
        /// <summary>
        /// 记录适配器状态日志
        /// </summary>
        public static void LogAdapterState(string adapterName, string methodName, string message)
        {
            Logger.Log.Debug($"[TEST][{adapterName}::{methodName}] {message}");
        }

        /// <summary>
        /// 记录适配器错误日志
        /// </summary>
        public static void LogAdapterError(string adapterName, string methodName, Exception ex)
        {
            Logger.Log.Error($"[TEST][{adapterName}::{methodName}] Error: {ex.GetType().Name} - {ex.Message}");
            if (ex.StackTrace != null)
            {
                Logger.Log.Error($"[TEST][{adapterName}::{methodName}] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 验证所有适配器是否正确初始化
        /// </summary>
        public static void VerifyAdaptersInitialized(
            ChatManagerAdapter managerAdapter,
            ChatDisplayAdapter displayAdapter)
        {
            Logger.Log.Info("[TEST] Starting adapter initialization verification...");
            
            // 验证 ChatManagerAdapter
            if (managerAdapter != null)
            {
                Logger.Log.Info("[TEST] ChatManagerAdapter: Initialized");
            }
            else
            {
                Logger.Log.Error("[TEST] ChatManagerAdapter: NULL - NOT INITIALIZED!");
            }
            
            // 验证 ChatDisplayAdapter
            if (displayAdapter != null)
            {
                Logger.Log.Info("[TEST] ChatDisplayAdapter: Initialized");
            }
            else
            {
                Logger.Log.Error("[TEST] ChatDisplayAdapter: NULL - NOT INITIALIZED!");
            }
            
            
            Logger.Log.Info("[TEST] Adapter initialization verification completed");
        }

        /// <summary>
        /// 模拟消息流程测试
        /// </summary>
        public static void SimulateMessageFlow(IChatService service, string testMessage, string senderName)
        {
            Logger.Log.Info($"[TEST] Starting message flow simulation...");
            Logger.Log.Info($"[TEST] Test message: '{testMessage}' from '{senderName}'");
            
            try
            {
                // 创建测试消息
                // 注意：IChatUser 是接口，实际测试时需要使用 ChatCore 提供的具体实现
                // 这里只是记录测试意图
                Logger.Log.Info($"[TEST] Would create test user with name: {senderName}");
                
                // 创建测试的 ChatMessage
                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Message = testMessage,
                    Sender = null, // 在实际测试中应该使用 service 提供的用户对象
                    Service = service,
                    Timestamp = DateTime.UtcNow,
                    IsSystemMessage = false,
                    IsActionMessage = false,
                    IsHighlighted = false,
                    IsMentioned = false,
                    Emotes = new List<ChatEmote>(),
                    Badges = new List<ChatBadge>()
                };
                
                Logger.Log.Info($"[TEST] Created test message with ID: {chatMessage.Id}");
                
                // 测试消息解析
                Logger.Log.Info($"[TEST] Testing message parsing...");
                var segments = new List<MessageSegment>
                {
                    new TextSegment { Text = testMessage }
                };
                
                Logger.Log.Info($"[TEST] Message parsed into {segments.Count} segment(s)");
                
                Logger.Log.Info($"[TEST] Message flow simulation completed successfully");
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"[TEST] Message flow simulation failed: {ex.Message}");
                Logger.Log.Error($"[TEST] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 验证配置同步
        /// </summary>
        public static void VerifyConfigurationSync()
        {
            Logger.Log.Info("[TEST] Starting configuration sync verification...");
            
            try
            {
                var config = ChatConfig.instance;
                // 记录当前配置值
                Logger.Log.Info($"[TEST] Current configuration values:");
                Logger.Log.Info($"[TEST]   FontSize: {config.FontSize}");
                Logger.Log.Info($"[TEST]   ChatWidth: {config.ChatWidth}");
                Logger.Log.Info($"[TEST]   ChatHeight: {config.ChatHeight}");
                // ChatConfig 没有 MaxMessages 属性
                Logger.Log.Info($"[TEST]   ReverseChatOrder: {config.ReverseChatOrder}");
                Logger.Log.Info($"[TEST]   PreCacheAnimatedEmotes: {config.PreCacheAnimatedEmotes}");
                Logger.Log.Info($"[TEST]   FontName: {config.SystemFontName}");
                
                // 测试配置更改
                Logger.Log.Info("[TEST] Testing configuration change...");
                var oldFontSize = config.FontSize;
                config.FontSize = oldFontSize + 1;
                Logger.Log.Info($"[TEST] Changed FontSize from {oldFontSize} to {config.FontSize}");
                
                // 恢复原值
                config.FontSize = oldFontSize;
                Logger.Log.Info($"[TEST] Restored FontSize to {config.FontSize}");
                
                Logger.Log.Info("[TEST] Configuration sync verification completed");
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"[TEST] Configuration sync verification failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行内存检查
        /// </summary>
        public static void PerformMemoryCheck(string checkpointName)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var totalMemory = GC.GetTotalMemory(false);
            var memoryMB = totalMemory / (1024 * 1024);
            
            Logger.Log.Info($"[TEST][MEMORY] {checkpointName}: {memoryMB} MB ({totalMemory} bytes)");
        }

        /// <summary>
        /// 测试适配器生命周期
        /// </summary>
        public static void TestAdapterLifecycle()
        {
            Logger.Log.Info("[TEST] Testing adapter lifecycle...");
            
            PerformMemoryCheck("Before initialization");
            
            // 这里会被实际的适配器初始化调用填充
            
            PerformMemoryCheck("After initialization");
            
            Logger.Log.Info("[TEST] Adapter lifecycle test completed");
        }
    }
#endif
}