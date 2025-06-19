using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Utilities;
using HarmonyLib;
using IPA;
using IPA.Loader;
using System;
using System.Reflection;
using EnhancedStreamChat.Installers;
using SiraUtil.Zenject;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace EnhancedStreamChat
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static string Name => "EnhancedStreamChat";
        internal static string Version => _meta.HVersion.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public const string HARMONY_ID = "EnhancedStreamChat.denpadokei.com.github";
        private Harmony harmony;
        private static PluginMetadata _meta;
        private static bool _hasBeenInitialized = false;
        [Init]
        public void Init(IPALogger logger, PluginMetadata meta, Zenjector zenjector)
        {
            Instance = this;
            _meta = meta;
            Logger.Log = logger;
            Logger.Log.Debug("Logger initialized.");
            var config = ChatConfig.instance;
            Font.textureRebuilt += this.Font_textureRebuilt;
            this.harmony = new Harmony(HARMONY_ID);
            zenjector.Install<ESCMenuInstaller>(Location.Menu);
            zenjector.Install<ESCInstaller>(Location.App);
        }
        [OnStart]
        public void OnStart()
        {
            Logger.Info("[Plugin] OnStart called");
#if DEBUG
            TestAdapters.AddTestLogs();
#endif
            // 延迟初始化 ChatManager，等待 Zenject 容器准备好
            // ChatManager 会在第一次被访问时自动创建（通过 OnEnable 中的 ChatManager.instance）
            // 这样可以确保所有 Zenject 绑定的服务都已经准备好
        }


        private void Font_textureRebuilt(Font obj) => Logger.Log.Debug($"FontTexture({obj.name}) width: {obj.material.mainTexture.width}, height: {obj.material.mainTexture.height}");

        [OnEnable]
        public void OnEnable()
        {
            Logger.Info($"[Plugin] OnEnable called, _hasBeenInitialized = {_hasBeenInitialized}");
            
            // 检测软重启
            if (_hasBeenInitialized)
            {
                Logger.Info("[Plugin] Soft restart detected, resetting static states");
                ResetStaticStates();
            }
            _hasBeenInitialized = true;
            
            this.harmony.PatchAll(Assembly.GetExecutingAssembly());
            try {
                // 确保 ChatManager 单例已创建
                ChatManager.TouchInstance();
                // 然后启用它
                if (ChatManager.instance != null)
                {
                    ChatManager.instance.enabled = true;
                }
                else
                {
                    Logger.Error("[Plugin] Failed to create ChatManager instance");
                }
            }
            catch (Exception ex) {
                Logger.Error($"[Plugin] Error during OnEnable: {ex}");
            }
        }
        
        private void ResetStaticStates()
        {
            try
            {
                // 重置 ChatManager 的 PersistentSingleton 状态
                ChatManager.ResetSingleton();
                
                // 重置 MainThreadInvoker
                MainThreadInvoker.Reset();
                
                // 清空 ChatDisplay 的静态消息队列
                ChatDisplay.ClearBackupMessageQueue();
                
                // 重置其他可能的 PersistentSingleton 实例
                // 注意：ChatImageProvider 和 ESCFontManager 现在由 Zenject 管理，不需要手动重置
                
                Logger.Info("[Plugin] Static states reset completed");
            }
            catch (Exception ex)
            {
                Logger.Error($"[Plugin] Error during static state reset: {ex}");
            }
        }

        [OnDisable]
        public void OnDisable()
        {
            this.harmony.UnpatchSelf();
            // 安全地禁用 ChatManager（如果存在）
            if (ChatManager.IsSingletonAvailable) {
                ChatManager.instance.enabled = false;
            }
        }

        [OnExit]
        public void OnExit() => Font.textureRebuilt -= this.Font_textureRebuilt;
    }
}
