#if DEBUG
using System;
using UnityEngine;

namespace EnhancedStreamChat
{
    /// <summary>
    /// 用于测试适配器功能的辅助类
    /// </summary>
    internal static class TestAdapters
    {
        public static void AddTestLogs()
        {
            Logger.Log.Info("=== EnhancedStreamChat Adapter Test Mode Enabled ===");
            Logger.Log.Info("This build includes additional logging for testing adapters.");
            Logger.Log.Info("Look for [ADAPTER_TEST] tags in the log file.");
            Logger.Log.Info("Log location: Beat Saber/Logs/_latest.log");
            Logger.Log.Info("===================================================");
        }

        public static void LogAdapterState(string adapterName, string method, string state)
        {
            Logger.Log.Info($"[ADAPTER_TEST] {adapterName}.{method}: {state}");
        }

        public static void LogAdapterError(string adapterName, string method, Exception ex)
        {
            Logger.Log.Error($"[ADAPTER_TEST] {adapterName}.{method} ERROR: {ex}");
        }

        public static void LogAdapterEvent(string adapterName, string eventName, object data = null)
        {
            var dataStr = data != null ? $" Data: {data}" : "";
            Logger.Log.Info($"[ADAPTER_TEST] {adapterName} Event: {eventName}{dataStr}");
        }
    }
}
#endif