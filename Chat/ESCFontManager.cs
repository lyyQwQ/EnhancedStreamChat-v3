using BeatSaberMarkupLanguage;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Zenject;
using IPA.Utilities;
using BeatSaberMarkupLanguage.Util;

namespace EnhancedStreamChat.Chat
{
    public class ESCFontManager : MonoBehaviour, IInitializable
    {
        private static readonly string FontPath = Path.Combine(Environment.CurrentDirectory, "UserData", "ESC");
        private static readonly string FontAssetPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FontAssets");
        private static readonly string MainFontPath = Path.Combine(FontAssetPath, "Main");
        private static readonly string FallBackFontPath = Path.Combine(FontAssetPath, "FallBack");

        public bool IsInitialized { get; private set; } = false;

        private TMP_FontAsset _mainFont = null;

        public TMP_FontAsset MainFont
        {
            get
            {
                if (!this._mainFont) {
                    return null;
                }
                if (this._mainFont.material.shader != BeatSaberUtils.TMPNoGlowFontShader) {
                    this._mainFont.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
                }
                return this._mainFont;
            }
            private set => this._mainFont = value;
        }

        private List<TMP_FontAsset> _fallbackFonts = new List<TMP_FontAsset>();
        public List<TMP_FontAsset> FallBackFonts
        {
            get
            {
                foreach (var font in this._fallbackFonts) {
                    if (font.material.shader != BeatSaberUtils.TMPNoGlowFontShader) {
                        font.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
                    }
                }
                return this._fallbackFonts;
            }
            private set => this._fallbackFonts = value;
        }
        public EnhancedFontInfo FontInfo { get; private set; } = null;
        
        // 单例实例保留用于向后兼容
        private static ESCFontManager _instance;
        public static ESCFontManager instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    Logger.Warn("ESCFontManager.instance accessed before initialization. This should be replaced with dependency injection.");
                }
                return _instance;
            }
        }
        
        // Zenject 注入的依赖
        private ChatConfig _chatConfig;
        
        [Inject]
        public void Construct()
        {
            _chatConfig = ChatConfig.instance; // 暂时使用单例，直到 ChatConfig 也被迁移
        }
        
        public void Initialize()
        {
            _instance = this; // 设置单例实例用于向后兼容
            Logger.Debug("ESCFontManager Initialize");
            // 等待主菜单 UI 就绪后再创建字体，避免过早访问 BSML DiContainer
            this.StartCoroutine(this.InitializeAfterMenuReady());
        }

        private IEnumerator InitializeAfterMenuReady()
        {
            // 通过 BSML MainMenuAwaiter 等待主菜单初始化（避免触发 “Tried getting DiContainer too early!”）
            var waitTask = MainMenuAwaiter.WaitForMainMenuAsync();
            while (!waitTask.IsCompleted)
            {
                yield return null;
            }
            yield return this.CreateChatFont();
        }

        public IEnumerator CreateChatFont()
        {
            Logger.Debug("Creating chat font");
            this.IsInitialized = false;
            Logger.Debug("Waiting for TMPNoGlowFontShader");
            yield return new WaitWhile(() => BeatSaberUtils.TMPNoGlowFontShader == null);
            Logger.Debug("TMPNoGlowFontShader loaded");
            if (this.MainFont != null)
            {
                Destroy(this.MainFont);
            }

            foreach (var font in this.FallBackFonts)
            {
                if (font != null)
                {
                    Destroy(font);
                }
            }

            if (!Directory.Exists(FontPath))
            {
                Directory.CreateDirectory(FontPath);
            }

            if (!Directory.Exists(MainFontPath))
            {
                Directory.CreateDirectory(MainFontPath);
            }

            if (!Directory.Exists(FallBackFontPath))
            {
                Directory.CreateDirectory(FallBackFontPath);
            }

            var fontName = _chatConfig.SystemFontName;
            TMP_FontAsset? asset = null;
            AssetBundle? bundle = null;
            foreach (var filename in Directory.EnumerateFiles(MainFontPath, "*.assets", SearchOption.TopDirectoryOnly))
            {
                using (var fs = File.OpenRead(filename))
                {
                    bundle = AssetBundle.LoadFromStream(fs);
                }

                if (bundle != null)
                {
                    break;
                }
            }

            if (bundle != null)
            {
                foreach (var bundleItem in bundle.GetAllAssetNames())
                {
                    asset = bundle.LoadAsset<TMP_FontAsset>(Path.GetFileNameWithoutExtension(bundleItem));
                    if (asset != null)
                    {
                        this.MainFont = asset;
                        bundle.Unload(false);
                        break;
                    }
                }
            }

            if (this.MainFont == null)
            {
                foreach (var fontFile in Directory.EnumerateFiles(FontPath, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var font = new Font(fontFile);
                        font.RequestCharactersInTexture(ExtraCharacters.CNText);
                        font.name = Path.GetFileNameWithoutExtension(fontFile);
                        if (font.name.ToLower() == fontName.ToLower())
                        {
                            asset = TMP_FontAsset.CreateFontAsset(font, 60, 6, GlyphRenderMode.SDFAA, 8192, 8192);
                            asset.ReadFontAssetDefinition();
                            this.MainFont = asset;
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
            }

            if (this.MainFont == null)
            {
                yield return new WaitWhile(() => !FontManager.IsInitialized);
                if (FontManager.TryGetTMPFontByFamily(fontName, out asset))
                {
                    asset.ReadFontAssetDefinition();
                    asset.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
                    this.MainFont = asset;
                }
                else
                {
                    Logger.Error($"Could not find font {fontName}! Falling back to Segoe UI");
                    fontName = "Segoe UI";
                    FontManager.TryGetTMPFontByFamily(fontName, out asset);
                    asset.ReadFontAssetDefinition();
                    this.MainFont = asset;
                }
            }

            this._fallbackFonts.Clear();
            foreach (var fallbackFontPath in Directory.EnumerateFiles(FallBackFontPath, "*.assets"))
            {
                using (var fs = File.OpenRead(fallbackFontPath))
                {
                    bundle = AssetBundle.LoadFromStream(fs);
                }

                if (bundle == null)
                {
                    continue;
                }

                foreach (var bundleItem in bundle.GetAllAssetNames())
                {
                    asset = bundle.LoadAsset<TMP_FontAsset>(Path.GetFileNameWithoutExtension(bundleItem));
                    if (asset != null)
                    {
                        this._fallbackFonts.Add(asset);
                    }
                }

                bundle.Unload(false);
            }

            foreach (var osFontPath in Font.GetPathsToOSFonts())
            {
                if (Path.GetFileNameWithoutExtension(osFontPath).ToLower() != "meiryo")
                {
                    continue;
                }

                var meiryo = new Font(osFontPath);
                meiryo.name = Path.GetFileNameWithoutExtension(osFontPath);
                asset = TMP_FontAsset.CreateFontAsset(meiryo);
                this._fallbackFonts.Add(asset);
            }

            if (this.MainFont != null)
            {
                this.FontInfo = new EnhancedFontInfo(this.MainFont);
            }

            this.IsInitialized = true;
            Logger.Debug($"Chat font created, this.IsInitialized = ({this.IsInitialized})");
        }
    }
}
