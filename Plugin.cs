using System;
using IPA;
using IPALogger = IPA.Logging.Logger;
using EnhancedStreamChat.Chat;
using IPA.Loader;
using System.Reflection;
using UnityEngine;

namespace EnhancedStreamChat
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        internal static Plugin instance { get; private set; }
        internal static string Name => "EnhancedStreamChat";
        internal static string Version => _meta.Version.ToString() ?? Assembly.GetExecutingAssembly().GetName().Version.ToString();

        private static PluginMetadata _meta;

        private ESCFontManager FontManager { get; set; }

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
        public void OnStart()
        {
            this.FontManager = new GameObject().AddComponent<ESCFontManager>();
        }


        private void Font_textureRebuilt(Font obj)
        {
            Logger.log.Debug($"FontTexture({obj.name}) width: {obj.material.mainTexture.width}, height: {obj.material.mainTexture.height}");
        }

        [OnEnable]
        public void OnEnable()
        {
            try
            {
                ChatManager.instance.enabled = true;
            }
            catch (Exception ex)
            {
                Logger.log.Error(ex);
            }
        }

        [OnDisable]
        public void OnDisable()
        {
            ChatManager.instance.enabled = false;
        }

        [OnExit]
        public void OnExit()
        {
            Font.textureRebuilt -= Font_textureRebuilt;
            GameObject.Destroy(FontManager);
        }
    }
}
