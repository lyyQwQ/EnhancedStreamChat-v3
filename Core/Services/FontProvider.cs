using System;
using System.Collections.Generic;
using System.Linq;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Graphics;
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
        private readonly Dictionary<string, TMP_FontAsset> _fontCache;
        
        [Inject]
        public FontProvider(ESCFontManager fontManager)
        {
            _legacyFontManager = fontManager;
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
            if (name == ChatConfig.instance.SystemFontName)
            {
                return GetChatFont();
            }
            
            // 尝试从系统查找字体
            // TODO: 需要找到正确的方式获取默认字体
            // TMP_FontAsset.defaultFontAsset 在当前版本中不可用
            
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
                
                // 如果聊天字体未初始化，返回 null
                // TODO: 需要找到正确的方式获取默认字体
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
            return GetFont(ChatConfig.instance.SystemFontName);
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
            // TODO: 需要找到正确的方式获取默认字体
            // if (TMP_FontAsset.defaultFontAsset != null)
            // {
            //     fonts.Add(TMP_FontAsset.defaultFontAsset.name);
            // }
            
            // 添加配置中的系统字体名
            fonts.Add(ChatConfig.instance.SystemFontName);
            
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