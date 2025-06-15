using UnityEngine;

namespace EnhancedStreamChat.Core.Utils
{
    /// <summary>
    /// Color 扩展方法
    /// </summary>
    public static class ColorExtensions
    {
        /// <summary>
        /// 返回一个设置了指定 alpha 值的颜色副本
        /// </summary>
        public static Color ColorWithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}