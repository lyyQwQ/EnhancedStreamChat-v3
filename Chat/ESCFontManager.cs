using BeatSaberMarkupLanguage;
using EnhancedStreamChat.Configuration;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Zenject;

namespace EnhancedStreamChat.Chat
{
    public class ESCFontManager : IInitializable, IDisposable
    {
        private static readonly string FontPath = Path.Combine(Environment.CurrentDirectory, "UserData", "ESC");
        private static readonly string FontAssetPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FontAssets");
        private static readonly string MainFontPath = Path.Combine(FontAssetPath, "Main");
        private static readonly string FallBackFontPath = Path.Combine(FontAssetPath, "FallBack");
        private const string DynamicFontWarmupText = "成功连接至房间 离开房间 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz，。、：；？！【】（）《》-_=+";
#if BS_1423
        private static readonly string PreferredSourceHanFontPath = Path.Combine(FontPath, "SourceHanSansCN-Medium.ttf");
        private const string PreferredSourceHanFontAssetName = "SourceHanSansCN-Medium SDF";
#endif
        private readonly List<AssetBundle> _loadedFontBundles = new List<AssetBundle>();
        // 1.42.3 读取旧版 TMP 字体 bundle 时，主 atlas 字段可能为空，只在初始化阶段补一次。
        private static readonly FieldInfo AtlasTextureField = typeof(TMP_FontAsset).GetField("m_AtlasTexture", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LegacyAtlasField = typeof(TMP_FontAsset).GetField("atlas", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        private PluginConfig _pluginConfig;

        [Inject]
        public void Constact(PluginConfig pluginConfig)
        {
            this._pluginConfig = pluginConfig;
        }

        public bool IsInitialized { get; private set; } = false;

        private TMP_FontAsset _mainFont = null;

        public TMP_FontAsset MainFont
        {
            get
            {
                if (!this._mainFont) {
                    return null;
                }
                ApplyNoGlowShader(this._mainFont);
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
                    ApplyNoGlowShader(font);
                }
                return this._fallbackFonts;
            }
            private set => this._fallbackFonts = value;
        }
        public EnhancedFontInfo FontInfo { get; private set; } = null;
        public void Initialize()
        {
            _ = SharedCoroutineStarter.Instance.StartCoroutine(this.CreateChatFont());
        }
        public IEnumerator CreateChatFont()
        {
            this.IsInitialized = false;
            yield return new WaitWhile(() => BeatSaberUtils.TMPNoGlowFontShader == null);
            if (this.MainFont != null) {
                GameObject.Destroy(this.MainFont);
            }
            foreach (var font in this.FallBackFonts) {
                if (font != null) {
                    GameObject.Destroy(font);
                }
            }
            this.FontInfo = null;
            ReleaseLoadedFontBundles();

            if (!Directory.Exists(FontPath)) {
                _ = Directory.CreateDirectory(FontPath);
            }
            if (!Directory.Exists(MainFontPath)) {
                _ = Directory.CreateDirectory(MainFontPath);
            }
            if (!Directory.Exists(FallBackFontPath)) {
                _ = Directory.CreateDirectory(FallBackFontPath);
            }

            var fontName = this._pluginConfig.SystemFontName;
            TMP_FontAsset? asset = null;
#if BS_1423
            if (TryCreatePreferredSourceHanFont(out asset)) {
                this.MainFont = asset;
                Logger.Info($"Using local Source Han font file '{Path.GetFileName(PreferredSourceHanFontPath)}' for ESC main font.");
            }

            if (this.MainFont == null) {
                if (TryCreateRuntimeFontAsset(fontName, out asset)) {
                    this.MainFont = asset;
                    Logger.Info($"Using runtime OS font '{fontName}' for ESC main font.");
                }
                else if (TryCreateRuntimeFontAsset("Microsoft YaHei", out asset)) {
                    this.MainFont = asset;
                    Logger.Warn($"Could not create runtime OS font '{fontName}'. Falling back to 'Microsoft YaHei'.");
                }
                else if (TryCreateRuntimeFontAsset("Microsoft YaHei UI", out asset)) {
                    this.MainFont = asset;
                    Logger.Warn($"Could not create runtime OS font '{fontName}'. Falling back to 'Microsoft YaHei UI'.");
                }
            }
#endif
            if (this.MainFont == null) {
                var bundledMainFonts = Directory.EnumerateFiles(MainFontPath, "*.assets", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path).Contains("sourcehansanscn", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
                foreach (var filename in bundledMainFonts) {
                    var bundle = LoadFontBundle(filename);
                    if (bundle == null) {
                        continue;
                    }

                    foreach (var bundleItem in bundle.GetAllAssetNames()) {
                        asset = bundle.LoadAsset<TMP_FontAsset>(Path.GetFileNameWithoutExtension(bundleItem));
                        if (asset != null) {
                            asset.ReadFontAssetDefinition();
                            EnsureAtlasTextures(asset, bundle);
                            ApplyNoGlowShader(asset);
                            if (!HasUsableAtlas(asset)) {
                                Logger.Warn($"Bundled font '{asset.name}' from '{Path.GetFileName(filename)}' does not have a usable atlas on this game version.");
                                continue;
                            }
                            this.MainFont = asset;
                            KeepBundleLoaded(bundle);
                            bundle = null;
                            break;
                        }
                    }
                    bundle?.Unload(false);
                    if (this.MainFont != null) {
                        break;
                    }
                }
            }
            if (this.MainFont == null) {
                foreach (var fontFile in Directory.EnumerateFiles(FontPath, "*", SearchOption.TopDirectoryOnly)) {
                    try {
#if BS_1423
                        if (!TryCreateRuntimeFontAssetFromFile(fontFile, Path.GetFileNameWithoutExtension(fontFile), out asset)) {
                            continue;
                        }

                        this.MainFont = asset;
                        break;
#else
                        var font = new Font(fontFile);
                        font.RequestCharactersInTexture(ExtraCharacters.CNText);
                        font.name = Path.GetFileNameWithoutExtension(fontFile);
                        if (font.name.ToLower() == fontName.ToLower()) {
                            asset = TMP_FontAsset.CreateFontAsset(font, 90, 6, GlyphRenderMode.SDFAA, 8192, 8192);
                            asset.ReadFontAssetDefinition();
                            this.MainFont = asset;
                            break;
                        }
#endif
                    }
                    catch (Exception e) {
                        Logger.Error(e);
                    }
                }
            }
            if (this.MainFont == null) {
                yield return new WaitWhile(() => !FontManager.IsInitialized);
                if (FontManager.TryGetTMPFontByFamily(fontName, out asset)) {
                    asset.ReadFontAssetDefinition();
                    ApplyNoGlowShader(asset);
                    this.MainFont = asset;
                }
                else {
                    Logger.Error($"Could not find font {fontName}! Falling back to Segoe UI");
                    fontName = "Segoe UI";
                    if (FontManager.TryGetTMPFontByFamily(fontName, out asset)) {
                        asset.ReadFontAssetDefinition();
                        ApplyNoGlowShader(asset);
                        this.MainFont = asset;
                    }
                }
            }
            this._fallbackFonts.Clear();
            foreach (var fallbackFontPath in Directory.EnumerateFiles(FallBackFontPath, "*.assets")) {
                var bundle = LoadFontBundle(fallbackFontPath);
                if (bundle == null) {
                    continue;
                }
                var existingFallbackCount = this._fallbackFonts.Count;
                foreach (var bundleItem in bundle.GetAllAssetNames()) {
                    asset = bundle.LoadAsset<TMP_FontAsset>(Path.GetFileNameWithoutExtension(bundleItem));
                    if (asset != null) {
                        asset.ReadFontAssetDefinition();
                        EnsureAtlasTextures(asset, bundle);
                        ApplyNoGlowShader(asset);
                        if (HasUsableAtlas(asset)) {
                            this._fallbackFonts.Add(asset);
                        }
                    }
                }
                if (this._fallbackFonts.Count > existingFallbackCount) {
                    KeepBundleLoaded(bundle);
                    bundle = null;
                }
                bundle?.Unload(false);
            }
            foreach (var osFontPath in Font.GetPathsToOSFonts()) {
                if (Path.GetFileNameWithoutExtension(osFontPath).ToLower() != "meiryo") {
                    continue;
                }
                var meiryo = new Font(osFontPath)
                {
                    name = Path.GetFileNameWithoutExtension(osFontPath)
                };
                asset = TMP_FontAsset.CreateFontAsset(meiryo);
                ApplyNoGlowShader(asset);
                this._fallbackFonts.Add(asset);
            }
            if (this.MainFont != null) {
                this.FontInfo = new EnhancedFontInfo(this.MainFont);
            }
            this.IsInitialized = true;
        }

        public void Dispose()
        {
            ReleaseLoadedFontBundles();
        }

        private static void ApplyNoGlowShader(TMP_FontAsset font)
        {
            if (!font) {
                return;
            }

            _ = BeatSaberUtils.EnsureTMPFontMaterial(font);
            var shader = BeatSaberUtils.TMPNoGlowFontShader;
            BeatSaberUtils.ApplyShaderToTMPFont(font, shader);
        }

        private void KeepBundleLoaded(AssetBundle bundle)
        {
            if (bundle == null) {
                return;
            }

            this._loadedFontBundles.Add(bundle);
        }

        private void ReleaseLoadedFontBundles()
        {
            foreach (var bundle in this._loadedFontBundles) {
                try {
                    bundle?.Unload(true);
                }
                catch (Exception e) {
                    Logger.Warn($"Failed to unload retained font bundle cleanly: {e.Message}");
                }
            }
            this._loadedFontBundles.Clear();
        }

        private static bool TryCreateRuntimeFontAsset(string fontName, out TMP_FontAsset asset)
        {
            asset = null;

            if (string.IsNullOrWhiteSpace(fontName)) {
                return false;
            }

            try {
                var font = Font.CreateDynamicFontFromOSFont(fontName, 90);
                if (font == null) {
                    return false;
                }

                font.name = fontName;
                asset = TMP_FontAsset.CreateFontAsset(font, 90, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                if (asset == null) {
                    Logger.Warn($"Runtime OS font '{fontName}' returned null TMP font asset.");
                    return false;
                }

                WarmUpDynamicFontAsset(asset);
                if (!HasUsableAtlas(asset)) {
                    Logger.Warn($"Runtime OS font '{fontName}' could not produce a usable atlas.");
                    return false;
                }

                ApplyNoGlowShader(asset);
                return true;
            }
            catch (Exception e) {
                Logger.Warn($"Failed to create runtime OS font '{fontName}': {e.Message}");
                asset = null;
                return false;
            }
        }

#if BS_1423
        private static bool TryCreatePreferredSourceHanFont(out TMP_FontAsset asset)
        {
            return TryCreateRuntimeFontAssetFromFile(PreferredSourceHanFontPath, PreferredSourceHanFontAssetName, out asset);
        }

        private static bool TryCreateRuntimeFontAssetFromFile(string fontFilePath, string fontAssetName, out TMP_FontAsset asset)
        {
            asset = null;

            if (string.IsNullOrWhiteSpace(fontFilePath) || !File.Exists(fontFilePath)) {
                return false;
            }

            try {
                asset = TMP_FontAsset.CreateFontAsset(fontFilePath, 0, 90, 6, GlyphRenderMode.SDFAA, 2048, 2048);
                if (asset == null) {
                    Logger.Warn($"Runtime font file '{Path.GetFileName(fontFilePath)}' returned null TMP font asset.");
                    return false;
                }

                asset.name = string.IsNullOrWhiteSpace(fontAssetName) ? Path.GetFileNameWithoutExtension(fontFilePath) : fontAssetName;
                WarmUpDynamicFontAsset(asset);
                if (!HasUsableAtlas(asset)) {
                    Logger.Warn($"Runtime font file '{Path.GetFileName(fontFilePath)}' could not produce a usable atlas.");
                    return false;
                }

                ApplyNoGlowShader(asset);
                return true;
            }
            catch (Exception e) {
                Logger.Warn($"Failed to create runtime font asset from '{Path.GetFileName(fontFilePath)}': {e.Message}");
                asset = null;
                return false;
            }
        }
#endif

        private static void WarmUpDynamicFontAsset(TMP_FontAsset asset)
        {
            if (asset == null) {
                return;
            }

            asset.ReadFontAssetDefinition();
            if (asset.atlasPopulationMode == AtlasPopulationMode.Static) {
                return;
            }

            try {
                asset.TryAddCharacters(DynamicFontWarmupText, false);
            }
            catch (Exception e) {
                Logger.Warn($"Failed to warm up dynamic font asset '{asset.name}': {e.Message}");
            }
        }

        private static bool HasUsableAtlas(TMP_FontAsset asset)
        {
            if (!asset) {
                return false;
            }

            var atlasTexture = asset.atlasTexture ?? asset.atlasTextures?.FirstOrDefault(texture => texture != null);
            if (atlasTexture == null) {
                return false;
            }

            return BeatSaberUtils.EnsureTMPFontMaterial(asset) != null;
        }

        private static AssetBundle LoadFontBundle(string path)
        {
            try {
                return AssetBundle.LoadFromFile(path);
            }
            catch (Exception e) {
                Logger.Warn($"Failed to load font bundle '{Path.GetFileName(path)}': {e.Message}");
                return null;
            }
        }

        private static void EnsureAtlasTextures(TMP_FontAsset asset, AssetBundle bundle)
        {
            if (asset == null || bundle == null || HasUsableAtlas(asset)) {
                return;
            }

            var atlasTextures = bundle.LoadAllAssets<Texture2D>()
                .Where(texture => texture != null)
                .OrderBy(texture => texture.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (atlasTextures.Length == 0) {
                return;
            }

            asset.atlasTextures = atlasTextures;
            AtlasTextureField?.SetValue(asset, atlasTextures[0]);
            LegacyAtlasField?.SetValue(asset, atlasTextures[0]);
            var material = BeatSaberUtils.EnsureTMPFontMaterial(asset);
            if (material != null && material.mainTexture == null) {
                material.mainTexture = atlasTextures[0];
            }
        }
    }
}
