using EnhancedStreamChat.Chat;
using IPA;
using IPA.Loader;
using System;
using System.Reflection;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;

namespace EnhancedStreamChat
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin instance { get; private set; }
        internal static string Name => "EnhancedStreamChat";
        internal static string Version => _meta.Version.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static PluginMetadata _meta;
        [Init]
        public void Init(IPALogger logger, PluginMetadata meta)
        {
            instance = this;
            _meta = meta;
            Logger.log = logger;
            Logger.log.Debug("Logger initialized.");
            var config = ChatConfig.instance;
            Font.textureRebuilt += this.Font_textureRebuilt;
        }
        [OnStart]
        public void OnStart() => ESCFontManager.TouchInstance();


        private void Font_textureRebuilt(Font obj) => Logger.log.Debug($"FontTexture({obj.name}) width: {obj.material.mainTexture.width}, height: {obj.material.mainTexture.height}");

        [OnEnable]
        public void OnEnable()
        {
            try {
                ChatManager.instance.enabled = true;
            }
            catch (Exception ex) {
                Logger.log.Error(ex);
            }
        }

        [OnDisable]
        public void OnDisable() => ChatManager.instance.enabled = false;

        [OnExit]
        public void OnExit()
        {
            Font.textureRebuilt -= this.Font_textureRebuilt;
        }
    }
}
