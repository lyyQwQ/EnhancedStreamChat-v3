using BeatSaberMarkupLanguage;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace EnhancedStreamChat.Chat
{
    public class ESCFontManager : MonoBehaviour
    {
        public static ESCFontManager instance;

        private static readonly string FontPath = Path.Combine(Environment.CurrentDirectory, "UserData", "ESC");
        private static readonly string FontAssetPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FontAssets");
        private static readonly string MainFontPath = Path.Combine(FontAssetPath, "Main");
        private static readonly string FallBackFontPath = Path.Combine(FontAssetPath, "FallBack");

        public bool IsInitialized { get; private set; } = false;

        private TMP_FontAsset _mainFont = null;

        public TMP_FontAsset MainFont {
            get
            {
                if (_mainFont?.material.shader != BeatSaberUtils.TMPNoGlowFontShader) {
                    _mainFont.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
                }
                return _mainFont;
            }
            private set { _mainFont = value; }
        }

        private List<TMP_FontAsset> _fallbackFonts = new List<TMP_FontAsset>();
        public List<TMP_FontAsset> FallBackFonts
        {
            get
            {
                foreach (var font in _fallbackFonts) {
                    if (font.material.shader != BeatSaberUtils.TMPNoGlowFontShader) {
                        font.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
                    }
                }
                return _fallbackFonts;
            }
            private set { _fallbackFonts = value; }
        }
        public EnhancedFontInfo FontInfo { get; private set; } = null;

        private void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            instance = this;
            HMMainThreadDispatcher.instance.Enqueue(CreateChatFont());
        }

        private void OnDestroy()
        {
            Destroy(this.gameObject);
            instance = null;
        }

        public IEnumerator CreateChatFont()
        {
            this.IsInitialized = false;
            if (MainFont != null) {
                Destroy(MainFont);
            }
            foreach (var font in FallBackFonts) {
                if (font != null) {
                    Destroy(font);
                }
            }

            if (!Directory.Exists(FontPath)) {
                Directory.CreateDirectory(FontPath);
            }
            if (!Directory.Exists(MainFontPath)) {
                Directory.CreateDirectory(MainFontPath);
            }
            if (!Directory.Exists(FallBackFontPath)) {
                Directory.CreateDirectory(FallBackFontPath);
            }

            var fontName = ChatConfig.instance.SystemFontName;
            TMP_FontAsset? asset = null;
            AssetBundle? bundle = null;
            foreach (var filename in Directory.EnumerateFiles(MainFontPath, "*.assets", SearchOption.TopDirectoryOnly)) {
                using (var fs = File.OpenRead(filename)) {
                    bundle = AssetBundle.LoadFromStream(fs);
                }
                if (bundle != null) {
                    break;
                }
            }
            if (bundle != null) {
                foreach (var bundleItem in bundle.GetAllAssetNames()) {
                    asset = bundle.LoadAsset<TMP_FontAsset>(Path.GetFileNameWithoutExtension(bundleItem));
                    if (asset != null) {
                        MainFont = asset;
                        bundle.Unload(false);
                        break;
                    }
                }
            }
            if (MainFont == null) {
                foreach (var fontFile in Directory.EnumerateFiles(FontPath, "*", SearchOption.TopDirectoryOnly)) {
                    try {
                        var font = new Font(fontFile);
                        font.RequestCharactersInTexture(JPAll.JPText);
                        font.name = Path.GetFileNameWithoutExtension(fontFile);
                        if (font.name.ToLower() == fontName.ToLower()) {
                            asset = TMP_FontAsset.CreateFontAsset(font, 90, 6, GlyphRenderMode.SDFAA, 8192, 8192);
                            asset.ReadFontAssetDefinition();
                            MainFont = asset;
                            break;
                        }
                    }
                    catch (Exception e) {
                        Logger.log.Error(e);
                    }
                }
            }
            if (MainFont == null) {
                yield return new WaitWhile(() => !FontManager.IsInitialized);
                if (FontManager.TryGetTMPFontByFamily(fontName, out asset)) {
                    asset.ReadFontAssetDefinition();
                    asset.material.shader = BeatSaberUtils.TMPNoGlowFontShader;
                    MainFont = asset;
                }
                else {
                    Logger.log.Error($"Could not find font {fontName}! Falling back to Segoe UI");
                    fontName = "Segoe UI";
                    FontManager.TryGetTMPFontByFamily(fontName, out asset);
                    asset.ReadFontAssetDefinition();
                    MainFont = asset;
                }
            }
            if (MainFont != null) {
                _fallbackFonts.Clear();
                foreach (var fallbackFontPath in Directory.EnumerateFiles(FallBackFontPath, "*.assets")) {
                    using (var fs = File.OpenRead(fallbackFontPath)) {
                        bundle = AssetBundle.LoadFromStream(fs);
                    }
                    if (bundle == null) {
                        continue;
                    }
                    foreach (var bundleItem in bundle.GetAllAssetNames()) {
                        asset = bundle.LoadAsset<TMP_FontAsset>(Path.GetFileNameWithoutExtension(bundleItem));
                        if (asset != null) {
                            _fallbackFonts.Add(asset);
                        }
                    }
                    bundle.Unload(false);
                }
                this.FontInfo = new EnhancedFontInfo(MainFont);
            }
            this.IsInitialized = true;
        }
    }
}
