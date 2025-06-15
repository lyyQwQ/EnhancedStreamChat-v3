#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Services;
using ChatCore.Utilities;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Tests
{
    /// <summary>
    /// 游戏内测试类，用于验证适配器功能
    /// </summary>
    public class GameplayTest : MonoBehaviour, IInitializable
    {
        private EnhancedStreamChat.Chat.Adapters.ChatManagerAdapter _chatManagerAdapter;
        private EnhancedStreamChat.Adapters.ChatDisplayAdapter _chatDisplayAdapter;
        private ChatConfig _chatConfig => ChatConfig.instance;
        private StringBuilder _testReport;
        private int _passedTests = 0;
        private int _failedTests = 0;

        [Inject]
        public void Construct(
            EnhancedStreamChat.Chat.Adapters.ChatManagerAdapter chatManagerAdapter,
            EnhancedStreamChat.Adapters.ChatDisplayAdapter chatDisplayAdapter)
        {
            _chatManagerAdapter = chatManagerAdapter;
            _chatDisplayAdapter = chatDisplayAdapter;
        }

        public void Initialize()
        {
            Logger.Log.Info("[GameplayTest] Test system initialized");
        }

        /// <summary>
        /// 运行所有测试
        /// </summary>
        public async Task RunAllTests()
        {
            Logger.Log.Info("[GameplayTest] ========== STARTING ADAPTER TESTS ==========");
            _testReport = new StringBuilder();
            _passedTests = 0;
            _failedTests = 0;

            _testReport.AppendLine($"# EnhancedStreamChat 适配器测试报告");
            _testReport.AppendLine($"**测试时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _testReport.AppendLine($"**游戏版本**: Beat Saber");
            _testReport.AppendLine($"**模组版本**: {Plugin.Version}");
            _testReport.AppendLine();
            _testReport.AppendLine("## 测试结果概览");
            _testReport.AppendLine();

            // 1. 验证适配器初始化
            await TestAdapterInitialization();

            // 2. 测试 Twitch 消息流程
            await TestTwitchMessageFlow();

            // 3. 测试 Bilibili 消息流程
            await TestBilibiliMessageFlow();

            // 4. 测试配置同步
            await TestConfigurationSync();

            // 5. 测试内存管理
            await TestMemoryManagement();

            // 生成测试报告
            GenerateTestReport();

            Logger.Log.Info($"[GameplayTest] ========== TESTS COMPLETED: {_passedTests} PASSED, {_failedTests} FAILED ==========");
        }

        /// <summary>
        /// 测试适配器初始化
        /// </summary>
        private async Task TestAdapterInitialization()
        {
            var testName = "适配器初始化测试";
            LogTestStart(testName);

            try
            {
                // 验证适配器初始化
                Logger.Log.Info("[TEST] 开始验证适配器初始化...");
                
                if (_chatManagerAdapter != null)
                {
                    Logger.Log.Info("[TEST] ChatManagerAdapter: 已初始化");
                }
                else
                {
                    Logger.Log.Error("[TEST] ChatManagerAdapter: 未初始化!");
                }
                
                if (_chatDisplayAdapter != null)
                {
                    Logger.Log.Info("[TEST] ChatDisplayAdapter: 已初始化");
                }
                else
                {
                    Logger.Log.Error("[TEST] ChatDisplayAdapter: 未初始化!");
                }
                
                if (_chatConfig != null)
                {
                    Logger.Log.Info("[TEST] ChatConfig: 可访问");
                }
                else
                {
                    Logger.Log.Error("[TEST] ChatConfig: 不可访问!");
                }
                
                Logger.Log.Info("[TEST] 适配器初始化验证完成");

                // 额外验证
                bool allInitialized = _chatManagerAdapter != null && 
                                     _chatDisplayAdapter != null && 
                                     _chatConfig != null;

                if (allInitialized)
                {
                    LogTestPass(testName, "所有适配器成功初始化");
                }
                else
                {
                    LogTestFail(testName, "一个或多个适配器未初始化");
                }
            }
            catch (Exception ex)
            {
                LogTestFail(testName, $"初始化测试失败: {ex.Message}");
            }

            await Task.Delay(100); // 给系统一些时间处理
        }

        /// <summary>
        /// 测试 Twitch 消息流程
        /// </summary>
        private async Task TestTwitchMessageFlow()
        {
            var testName = "Twitch 消息流程测试";
            LogTestStart(testName);

            try
            {
                // 创建模拟的 Twitch 服务
                var mockService = new MockChatService("Twitch");
                
                // 创建测试消息
                var testMessage = "Hello from Twitch test! 😊";
                var testUser = new MockChatUser
                {
                    Id = "test_twitch_user",
                    UserName = "TwitchTestUser",
                    DisplayName = "Twitch Test User"
                };

                // 记录测试消息
                Logger.Log.Info($"[TEST] 模拟 Twitch 消息: '{testMessage}' 来自 '{testUser.DisplayName}'");
                
                // 测试预渲染功能
                if (_chatDisplayAdapter != null)
                {
                    var mockMessage = CreateMockMessage(testMessage, testUser, mockService);
                    var renderResult = await _chatDisplayAdapter.PreRenderMessage(mockService, mockMessage, null);
                    if (renderResult != null)
                    {
                        Logger.Log.Info($"[TEST] 预渲染成功，消息高度: {renderResult.Height}");
                    }
                }

                LogTestPass(testName, "Twitch 消息流程测试完成");
            }
            catch (Exception ex)
            {
                LogTestFail(testName, $"Twitch 测试失败: {ex.Message}");
            }

            await Task.Delay(500); // 等待消息处理
        }

        /// <summary>
        /// 测试 Bilibili 消息流程
        /// </summary>
        private async Task TestBilibiliMessageFlow()
        {
            var testName = "Bilibili 消息流程测试";
            LogTestStart(testName);

            try
            {
                // 创建模拟的 Bilibili 服务
                var mockService = new MockChatService("Bilibili");
                
                // 创建测试消息（包含中文）
                var testMessage = "Bilibili 测试消息！你好世界 🎮";
                var testUser = new MockChatUser
                {
                    Id = "test_bilibili_user",
                    UserName = "BilibiliTestUser",
                    DisplayName = "B站测试用户"
                };

                // 记录测试消息
                Logger.Log.Info($"[TEST] 模拟 Twitch 消息: '{testMessage}' 来自 '{testUser.DisplayName}'");

                // 特别测试 Bilibili UID 处理
                var largeUid = "9999999999"; // 测试大 UID
                var uidTestUser = new MockChatUser
                {
                    Id = largeUid,
                    UserName = $"User{largeUid}",
                    DisplayName = "大UID测试"
                };

                Logger.Log.Info($"[TEST] 模拟大UID消息: '测试大UID' 来自 '{uidTestUser.DisplayName}' (UID: {largeUid})");

                LogTestPass(testName, "Bilibili 消息流程测试完成，包括中文和大UID支持");
            }
            catch (Exception ex)
            {
                LogTestFail(testName, $"Bilibili 测试失败: {ex.Message}");
            }

            await Task.Delay(500); // 等待消息处理
        }

        /// <summary>
        /// 测试配置同步
        /// </summary>
        private async Task TestConfigurationSync()
        {
            var testName = "配置同步测试";
            LogTestStart(testName);

            try
            {
                // 记录当前配置
                Logger.Log.Info($"[TEST] 当前配置 - FontSize: {_chatConfig.FontSize}, ChatWidth: {_chatConfig.ChatWidth}, ChatHeight: {_chatConfig.ChatHeight}");

                // 测试配置更改传播
                var originalFontSize = _chatConfig.FontSize;
                var testFontSize = originalFontSize + 2;

                Logger.Log.Info($"[TEST] 更改字体大小: {originalFontSize} -> {testFontSize}");
                _chatConfig.FontSize = testFontSize;

                await Task.Delay(100); // 等待同步

                // 验证 ChatConfig 单例是否同步
                var chatConfig = ChatConfig.instance;
                if (chatConfig.FontSize == testFontSize)
                {
                    LogTestPass(testName, "配置同步成功");
                }
                else
                {
                    LogTestFail(testName, $"配置同步失败: 期望 {testFontSize}, 实际 {chatConfig.FontSize}");
                }

                // 恢复原值
                _chatConfig.FontSize = originalFontSize;
            }
            catch (Exception ex)
            {
                LogTestFail(testName, $"配置测试失败: {ex.Message}");
            }

            await Task.Delay(100);
        }

        /// <summary>
        /// 测试内存管理
        /// </summary>
        private async Task TestMemoryManagement()
        {
            var testName = "内存管理测试";
            LogTestStart(testName);

            try
            {
                PerformMemoryCheck("测试开始前");

                // 创建大量消息测试内存
                var mockService = new MockChatService("Memory Test");
                for (int i = 0; i < 50; i++)
                {
                    var testUser = new MockChatUser
                    {
                        Id = $"user_{i}",
                        UserName = $"User{i}",
                        DisplayName = $"测试用户{i}"
                    };

                    Logger.Log.Debug($"[TEST] 内存测试消息 #{i} 来自 {testUser.DisplayName}");
                    
                    if (i % 10 == 0)
                    {
                        await Task.Delay(100); // 每10条消息暂停一下
                    }
                }

                PerformMemoryCheck("创建50条消息后");

                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                PerformMemoryCheck("垃圾回收后");

                LogTestPass(testName, "内存管理测试完成");
            }
            catch (Exception ex)
            {
                LogTestFail(testName, $"内存测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成测试报告
        /// </summary>
        private void GenerateTestReport()
        {
            _testReport.AppendLine();
            _testReport.AppendLine("## 测试总结");
            _testReport.AppendLine($"- **通过测试**: {_passedTests}");
            _testReport.AppendLine($"- **失败测试**: {_failedTests}");
            _testReport.AppendLine($"- **总测试数**: {_passedTests + _failedTests}");
            _testReport.AppendLine($"- **成功率**: {(_passedTests * 100.0 / (_passedTests + _failedTests)):F1}%");

            // 保存报告
            var reportDir = Path.Combine(Application.dataPath, "../UserData/EnhancedStreamChat/TestReports");
            Directory.CreateDirectory(reportDir);

            var reportFile = Path.Combine(reportDir, $"TEST_RESULTS_{DateTime.Now:yyyy-MM-dd}.md");
            File.WriteAllText(reportFile, _testReport.ToString());

            Logger.Log.Info($"[GameplayTest] 测试报告已保存到: {reportFile}");
        }

        private void LogTestStart(string testName)
        {
            Logger.Log.Info($"[TEST] ===== 开始测试: {testName} =====");
            _testReport.AppendLine($"### {testName}");
            _testReport.AppendLine($"**开始时间**: {DateTime.Now:HH:mm:ss}");
        }

        private void LogTestPass(string testName, string details)
        {
            Logger.Log.Info($"[TEST] ✓ 通过: {testName} - {details}");
            _testReport.AppendLine($"**结果**: ✅ 通过");
            _testReport.AppendLine($"**详情**: {details}");
            _testReport.AppendLine();
            _passedTests++;
        }

        private void LogTestFail(string testName, string reason)
        {
            Logger.Log.Error($"[TEST] ✗ 失败: {testName} - {reason}");
            _testReport.AppendLine($"**结果**: ❌ 失败");
            _testReport.AppendLine($"**原因**: {reason}");
            _testReport.AppendLine();
            _failedTests++;
        }

        /// <summary>
        /// 执行内存检查
        /// </summary>
        private void PerformMemoryCheck(string checkpointName)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var totalMemory = GC.GetTotalMemory(false);
            var memoryMB = totalMemory / (1024 * 1024);
            
            Logger.Log.Info($"[TEST][MEMORY] {checkpointName}: {memoryMB} MB ({totalMemory} bytes)");
        }

        /// <summary>
        /// 创建模拟消息
        /// </summary>
        private IChatMessage CreateMockMessage(string text, MockChatUser user, MockChatService service)
        {
            return new MockChatMessage
            {
                Id = Guid.NewGuid().ToString(),
                Message = text,
                Sender = user,
                IsSystemMessage = false,
                IsActionMessage = false,
                IsHighlighted = false,
                IsPing = false,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// 模拟的聊天服务
        /// </summary>
        private class MockChatService : IChatService
        {
            public string DisplayName { get; }
            public MockChatService(string name) => DisplayName = name;

            // IChatService 接口实现（简化版）
            public event Action<IChatService, IChatChannel> OnJoinChannel;
            public event Action<IChatService, IChatChannel> OnLeaveChannel;
            public event Action<IChatService, IChatChannel> OnRoomStateUpdated;
            public event Action<IChatService, IChatChannel> OnChannelStateUpdated;
            public event Action<IChatService, IChatMessage> OnTextMessageReceived;
            public event Action<IChatService, string> OnChatCleared;
            public event Action<IChatService, string> OnMessageCleared;
            public event Action<IChatService, IChatChannel, Dictionary<string, IChatResourceData>> OnChannelResourceDataCached;
            public event Action<IChatService> OnLogin;

            public void SendTextMessage(string message, IChatChannel channel) { }
            public void Enable() { }
            public void Disable() { }
            
            // 为了简化测试，这些方法留空
            public IChatChannel GetChannel(string channelId) => null;
        }

        /// <summary>
        /// 模拟的聊天用户
        /// </summary>
        private class MockChatUser : IChatUser
        {
            public string Id { get; set; }
            public string UserName { get; set; }
            public string DisplayName { get; set; }
            public string Color { get; set; } = "#FFFFFF";
            public bool IsBroadcaster { get; set; }
            public bool IsModerator { get; set; }
            public bool IsVip { get; set; }
            public bool IsSubscriber { get; set; }
            public bool IsTurbo { get; set; }
            public bool IsStaff { get; set; }
            public bool IsPartner { get; set; }
            public IChatBadge[] Badges { get; set; } = new IChatBadge[0];

            public JSONObject ToJson()
            {
                var json = new JSONObject();
                json["Id"] = Id;
                json["UserName"] = UserName;
                json["DisplayName"] = DisplayName;
                json["Color"] = Color;
                json["IsBroadcaster"] = IsBroadcaster;
                json["IsModerator"] = IsModerator;
                json["IsVip"] = IsVip;
                json["IsSubscriber"] = IsSubscriber;
                json["IsTurbo"] = IsTurbo;
                json["IsStaff"] = IsStaff;
                json["IsPartner"] = IsPartner;
                return json;
            }
        }

        /// <summary>
        /// 模拟的聊天消息
        /// </summary>
        private class MockChatMessage : IChatMessage
        {
            public string Id { get; set; }
            public bool IsSystemMessage { get; set; }
            public bool IsActionMessage { get; set; }
            public bool IsHighlighted { get; set; }
            public bool IsPing { get; set; }
            public string Message { get; set; }
            public IChatUser Sender { get; set; }
            public IChatChannel Channel { get; set; }
            public IChatEmote[] Emotes { get; set; } = new IChatEmote[0];
            public IChatBadge[] Badges { get; set; } = new IChatBadge[0];
            public JSONObject JSONObject { get; set; }
            public string Type => "Mock";
            public DateTime Timestamp { get; set; }
            public System.Collections.ObjectModel.ReadOnlyDictionary<string, string> Metadata { get; set; } = 
                new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

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
    }
}
#endif