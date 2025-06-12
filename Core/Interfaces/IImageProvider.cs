using System.Threading.Tasks;
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