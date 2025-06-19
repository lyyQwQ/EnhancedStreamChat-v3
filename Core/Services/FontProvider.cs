using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberMarkupLanguage;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using TMPro;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Core.Services
{
    /// <summary>
    /// 字体管理服务实现，适配现有的 ESCFontManager
    /// </summary>
    public class FontProvider : IFontProvider
    {
        private readonly ESCFontManager _legacyFontManager;
        private readonly IChatConfiguration _chatConfig;
        private readonly Dictionary<string, TMP_FontAsset> _fontCache;
        
        [Inject]
        public FontProvider(ESCFontManager fontManager, IChatConfiguration chatConfig)
        {
            _legacyFontManager = fontManager;
            _chatConfig = chatConfig;
            _fontCache = new Dictionary<string, TMP_FontAsset>();
            
            // 等待字体管理器初始化
            if (!_legacyFontManager.IsInitialized)
            {
                Logger.Info("Waiting for ESCFontManager to initialize...");
            }
        }
        
        /// <summary>
        /// 获取指定名称的字体
        /// </summary>
        public TMP_FontAsset GetFont(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DefaultFont;
            }
            
            // 检查缓存
            if (_fontCache.TryGetValue(name, out var cachedFont))
            {
                return cachedFont;
            }
            
            // 如果请求的是主字体名称，返回主字体
            if (name == _chatConfig.SystemFontName)
            {
                return GetChatFont();
            }
            
            // 尝试通过 BeatSaberUI 获取字体
            if (BeatSaberUtils.TryGetTMPFontByFamily(name, out var beatSaberFont))
            {
                _fontCache[name] = beatSaberFont;
                return beatSaberFont;
            }
            
            // 如果找不到，返回默认字体
            Logger.Warn($"Font '{name}' not found, using default font");
            return DefaultFont;
        }
        
        /// <summary>
        /// 获取默认字体
        /// </summary>
        public TMP_FontAsset DefaultFont
        {
            get
            {
                // 优先返回主聊天字体
                var chatFont = GetChatFont();
                if (chatFont != null)
                {
                    return chatFont;
                }
                
                // 如果聊天字体未初始化，尝试获取 Beat Saber 主字体
                if (BeatSaberUI.MainTextFont != null)
                {
                    return BeatSaberUI.MainTextFont;
                }
                
                // 最后尝试从 Resources 查找默认字体
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts != null && fonts.Length > 0)
                {
                    var defaultFont = fonts.FirstOrDefault(f => f.name.Contains("Teko-Medium") || f.name.Contains("Default"));
                    if (defaultFont != null)
                    {
                        Logger.Info($"Using fallback font: {defaultFont.name}");
                        return defaultFont;
                    }
                }
                
                Logger.Error("No default font found!");
                return null;
            }
        }
        
        /// <summary>
        /// 获取主聊天字体
        /// </summary>
        public TMP_FontAsset GetChatFont()
        {
            return _legacyFontManager.MainFont;
        }
        
        /// <summary>
        /// 获取系统字体
        /// </summary>
        public TMP_FontAsset GetSystemFont()
        {
            return GetFont(_chatConfig.SystemFontName);
        }
        
        /// <summary>
        /// 获取所有可用字体名称
        /// </summary>
        public IEnumerable<string> GetAvailableFonts()
        {
            var fonts = new List<string>();
            
            // 添加主字体
            if (_legacyFontManager.MainFont != null)
            {
                fonts.Add(_legacyFontManager.MainFont.name);
            }
            
            // 添加回退字体
            if (_legacyFontManager.FallBackFonts != null)
            {
                fonts.AddRange(_legacyFontManager.FallBackFonts.Select(f => f.name));
            }
            
            // 添加系统默认字体
            if (BeatSaberUI.MainTextFont != null)
            {
                fonts.Add(BeatSaberUI.MainTextFont.name);
            }
            
            // 添加配置中的系统字体名
            fonts.Add(_chatConfig.SystemFontName);
            
            // 去重
            return fonts.Distinct();
        }
        
        /// <summary>
        /// 检查字体是否支持指定字符
        /// </summary>
        public bool HasCharacter(TMP_FontAsset font, char character)
        {
            if (font == null) return false;
            
            // 检查主字体
            if (font.HasCharacter(character))
            {
                return true;
            }
            
            // 检查回退字体
            if (font.fallbackFontAssetTable != null)
            {
                foreach (var fallback in font.fallbackFontAssetTable)
                {
                    if (fallback != null && fallback.HasCharacter(character))
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 设置字体回退链
        /// </summary>
        public void SetupFontFallbackChain(TMP_FontAsset mainFont, IEnumerable<TMP_FontAsset> fallbackFonts)
        {
            if (mainFont == null) return;
            
            // 清空现有的回退字体
            if (mainFont.fallbackFontAssetTable == null)
            {
                mainFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }
            else
            {
                mainFont.fallbackFontAssetTable.Clear();
            }
            
            // 添加新的回退字体
            if (fallbackFonts != null)
            {
                foreach (var fallback in fallbackFonts)
                {
                    if (fallback != null && fallback != mainFont)
                    {
                        mainFont.fallbackFontAssetTable.Add(fallback);
                    }
                }
            }
            
            // 添加旧系统的回退字体
            if (_legacyFontManager.FallBackFonts != null)
            {
                foreach (var fallback in _legacyFontManager.FallBackFonts)
                {
                    if (fallback != null && 
                        fallback != mainFont && 
                        !mainFont.fallbackFontAssetTable.Contains(fallback))
                    {
                        mainFont.fallbackFontAssetTable.Add(fallback);
                    }
                }
            }
            
            Logger.Info($"Set up font fallback chain for {mainFont.name} with {mainFont.fallbackFontAssetTable.Count} fallback fonts");
        }
        
        /// <summary>
        /// 获取字体信息
        /// </summary>
        public EnhancedFontInfo GetFontInfo()
        {
            return _legacyFontManager.FontInfo;
        }
        
        /// <summary>
        /// 检查字体管理器是否已初始化
        /// </summary>
        public bool IsInitialized => _legacyFontManager.IsInitialized;
        
        /// <summary>
        /// 在字体中注册表情字符
        /// </summary>
        public bool TryRegisterImageCharacter(string imageId, out uint character)
        {
            character = 0;
            
            var fontInfo = GetFontInfo();
            if (fontInfo == null)
            {
                Logger.Warn("FontInfo not initialized");
                return false;
            }
            
            // 这里需要调用 EnhancedFontInfo 的注册方法
            // 由于没有看到具体实现，暂时返回 false
            return false;
        }
    }
}