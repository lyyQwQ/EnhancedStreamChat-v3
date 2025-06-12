using System.Collections.Generic;
using TMPro;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 负责管理和提供字体资源
    /// </summary>
    public interface IFontProvider
    {
        /// <summary>
        /// 获取指定名称的字体
        /// </summary>
        /// <param name="name">字体名称</param>
        /// <returns>TMP字体资源，如果找不到返回默认字体</returns>
        TMP_FontAsset GetFont(string name);
        
        /// <summary>
        /// 获取默认字体
        /// </summary>
        TMP_FontAsset DefaultFont { get; }
        
        /// <summary>
        /// 获取所有可用字体名称
        /// </summary>
        IEnumerable<string> GetAvailableFonts();
        
        /// <summary>
        /// 检查字体是否支持指定字符
        /// </summary>
        /// <param name="font">要检查的字体</param>
        /// <param name="character">要检查的字符</param>
        /// <returns>是否支持该字符</returns>
        bool HasCharacter(TMP_FontAsset font, char character);
        
        /// <summary>
        /// 设置字体回退链
        /// </summary>
        /// <param name="mainFont">主字体</param>
        /// <param name="fallbackFonts">回退字体列表</param>
        void SetupFontFallbackChain(TMP_FontAsset mainFont, IEnumerable<TMP_FontAsset> fallbackFonts);
    }
}