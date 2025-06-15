using System;
using System.Collections.Generic;
using System.Diagnostics;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;

namespace EnhancedStreamChat
{
    /// <summary>
    /// 测试辅助类，用于验证适配器功能
    /// </summary>
#if DEBUG
    internal static class TestAdapters
    {
        /// <summary>
        /// 在插件启动时输出测试模式信息
        /// </summary>
        public static void AddTestLogs()
        {
            Logger.Log.Warn("======================== TEST MODE ENABLED ========================");
            Logger.Log.Warn("This build includes testing features. DO NOT use in production!");
            Logger.Log.Warn("=================================================================");
        }

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

    }
#endif
}