using System.Collections.Generic;
using System.Threading.Tasks;
using EnhancedStreamChat.Graphics;
using UnityEngine;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 负责加载和缓存图片资源（表情、徽章等）
    /// </summary>
    public interface IImageProvider
    {
        /// <summary>
        /// 异步获取图片纹理
        /// </summary>
        /// <param name="url">图片URL</param>
        /// <returns>加载的纹理，如果失败返回null</returns>
        Task<Texture2D> GetImageAsync(string url);
        
        /// <summary>
        /// 预加载常用图片
        /// </summary>
        /// <param name="urls">要预加载的URL列表</param>
        Task PreloadImagesAsync(string[] urls);
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        void ClearCache();
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        CacheStatistics GetCacheStatistics();
        
        /// <summary>
        /// 异步获取或加载图片信息（包含精灵和动画数据）
        /// </summary>
        /// <param name="imageId">图片唯一ID</param>
        /// <param name="imageUrl">图片URL</param>
        /// <param name="isAnimated">是否为动画图片</param>
        /// <param name="forcedHeight">强制高度，-1表示使用原始高度</param>
        /// <returns>图片信息，如果失败返回null</returns>
        Task<EnhancedImageInfo> GetOrLoadImageInfoAsync(string imageId, string imageUrl, bool isAnimated = false, int forcedHeight = -1);
        
        /// <summary>
        /// 预加载多个图片信息
        /// </summary>
        /// <param name="images">要预加载的图片信息列表</param>
        Task PreloadImageInfosAsync(IEnumerable<(string id, string url, bool isAnimated)> images);
        
        /// <summary>
        /// 获取缓存的图片信息
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <returns>缓存的图片信息，如果不存在返回null</returns>
        EnhancedImageInfo GetCachedImageInfo(string imageId);
        
        /// <summary>
        /// 检查图片是否已缓存
        /// </summary>
        /// <param name="url">图片URL</param>
        /// <returns>是否已缓存</returns>
        bool IsImageCached(string url);
        
        /// <summary>
        /// 注册图片到字体
        /// </summary>
        /// <param name="imageId">图片ID</param>
        /// <param name="font">目标字体</param>
        /// <returns>是否注册成功</returns>
        bool TryRegisterImageToFont(string imageId, EnhancedFontInfo font);
    }
    
    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public int TotalImages { get; set; }
        public long TotalMemoryBytes { get; set; }
        public int CacheHits { get; set; }
        public int CacheMisses { get; set; }
        
        public float HitRate => TotalImages > 0 ? (float)CacheHits / (CacheHits + CacheMisses) : 0;
    }
}