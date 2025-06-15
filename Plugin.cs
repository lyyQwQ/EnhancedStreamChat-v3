using EnhancedStreamChat.Chat;
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
            this.harmony.PatchAll(Assembly.GetExecutingAssembly());
            try {
                // 确保 ChatManager 单例已创建
                ChatManager.TouchInstance();
                // 然后启用它
                ChatManager.instance.enabled = true;
            }
            catch (Exception ex) {
                Logger.Error(ex);
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
