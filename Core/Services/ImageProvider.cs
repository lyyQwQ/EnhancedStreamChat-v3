using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using UnityEngine;

namespace EnhancedStreamChat.Core.Services
{
    /// <summary>
    /// 图片加载和缓存服务实现，适配现有的 ChatImageProvider
    /// </summary>
    public class ImageProvider : IImageProvider
    {
        private readonly ChatImageProvider _legacyProvider;
        private readonly ConcurrentDictionary<string, Texture2D> _textureCache;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<EnhancedImageInfo>> _pendingImageInfoLoads;
        private readonly CacheStatistics _statistics;
        
        public ImageProvider()
        {
            _legacyProvider = ChatImageProvider.instance;
            _textureCache = new ConcurrentDictionary<string, Texture2D>();
            _pendingImageInfoLoads = new ConcurrentDictionary<string, TaskCompletionSource<EnhancedImageInfo>>();
            _statistics = new CacheStatistics();
        }
        
        /// <summary>
        /// 异步获取图片纹理
        /// </summary>
        public async Task<Texture2D> GetImageAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Logger.Warn("Attempted to load image with null or empty URL");
                return null;
            }
            
            // 检查缓存
            if (_textureCache.TryGetValue(url, out var cachedTexture))
            {
                _statistics.CacheHits++;
                return cachedTexture;
            }
            
            _statistics.CacheMisses++;
            
            // 使用 TaskCompletionSource 来将协程转换为 Task
            var tcs = new TaskCompletionSource<Texture2D>();
            
            // 调用旧的下载逻辑
            CoroutineRunner.Instance.StartCoroutine(DownloadAndCacheTexture(url, tcs));
            
            return await tcs.Task;
        }
        
        /// <summary>
        /// 预加载常用图片
        /// </summary>
        public async Task PreloadImagesAsync(string[] urls)
        {
            if (urls == null || urls.Length == 0)
                return;
            
            var tasks = urls.Select(GetImageAsync).ToList();
            await Task.WhenAll(tasks);
            
            Logger.Info($"Preloaded {tasks.Count(t => t.Result != null)} images successfully");
        }
        
        /// <summary>
        /// 清理缓存
        /// </summary>
        public void ClearCache()
        {
            // 清理纹理缓存
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            _textureCache.Clear();
            
            // 清理旧系统的缓存
            _legacyProvider.CachedImageInfo.Clear();
            
            // 重置统计
            _statistics.TotalImages = 0;
            _statistics.TotalMemoryBytes = 0;
            _statistics.CacheHits = 0;
            _statistics.CacheMisses = 0;
            
            Logger.Info("Image cache cleared");
        }
        
        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            // 更新当前统计
            _statistics.TotalImages = _textureCache.Count + _legacyProvider.CachedImageInfo.Count;
            
            // 计算内存使用
            long totalMemory = 0;
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null)
                {
                    // 估算纹理内存使用（width * height * 4 bytes for RGBA32）
                    totalMemory += texture.width * texture.height * 4;
                }
            }
            _statistics.TotalMemoryBytes = totalMemory;
            
            return _statistics;
        }
        
        /// <summary>
        /// 下载并缓存纹理的协程
        /// </summary>
        private IEnumerator DownloadAndCacheTexture(string url, TaskCompletionSource<Texture2D> tcs)
        {
            Texture2D texture = null;
            
            // 使用旧的下载逻辑
            yield return _legacyProvider.DownloadContent(url, (data) =>
            {
                if (data != null && data.Length > 0)
                {
                    try
                    {
                        // 创建纹理
                        texture = new Texture2D(2, 2);
                        if (texture.LoadImage(data))
                        {
                            // 设置纹理属性
                            texture.wrapMode = TextureWrapMode.Clamp;
                            texture.filterMode = FilterMode.Bilinear;
                            
                            // 缓存纹理
                            _textureCache.TryAdd(url, texture);
                            
                            Logger.Debug($"Successfully loaded texture from {url}");
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(texture);
                            texture = null;
                            Logger.Error($"Failed to load texture data from {url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (texture != null)
                        {
                            UnityEngine.Object.Destroy(texture);
                            texture = null;
                        }
                        Logger.Error($"Exception loading texture from {url}: {ex}");
                    }
                }
                else
                {
                    Logger.Error($"Failed to download data from {url}");
                }
            });
            
            // 完成 Task
            tcs.SetResult(texture);
        }
        
        /// <summary>
        /// 从旧的 EnhancedImageInfo 获取纹理
        /// </summary>
        public Texture2D GetTextureFromImageInfo(string imageId)
        {
            if (_legacyProvider.CachedImageInfo.TryGetValue(imageId, out var imageInfo))
            {
                if (imageInfo.Sprite != null && imageInfo.Sprite.texture != null)
                {
                    return imageInfo.Sprite.texture;
                }
                
                // 如果是动画，返回第一帧
                // TODO: 需要查看 AnimationControllerData 的正确属性
                // 暂时返回 null
                // if (imageInfo.AnimControllerData != null && 
                //     imageInfo.AnimControllerData.Textures != null && 
                //     imageInfo.AnimControllerData.Textures.Length > 0)
                // {
                //     return imageInfo.AnimControllerData.Textures[0];
                // }
            }
            
            return null;
        }
        
        /// <summary>
        /// 检查图片是否已缓存
        /// </summary>
        public bool IsImageCached(string url)
        {
            return _textureCache.ContainsKey(url);
        }
        
        /// <summary>
        /// 获取缓存的图片信息
        /// </summary>
        public EnhancedImageInfo GetCachedImageInfo(string imageId)
        {
            _legacyProvider.CachedImageInfo.TryGetValue(imageId, out var info);
            return info;
        }
        
        /// <summary>
        /// 异步获取或加载 EnhancedImageInfo
        /// </summary>
        public async Task<EnhancedImageInfo> GetOrLoadImageInfoAsync(string imageId, string imageUrl, bool isAnimated = false, int forcedHeight = -1)
        {
            if (string.IsNullOrEmpty(imageId) || string.IsNullOrEmpty(imageUrl))
            {
                Logger.Warn($"Invalid imageId or imageUrl: {imageId}, {imageUrl}");
                return null;
            }
            
            // 检查是否已缓存
            if (_legacyProvider.CachedImageInfo.TryGetValue(imageId, out var cachedInfo))
            {
                _statistics.CacheHits++;
                return cachedInfo;
            }
            
            // 检查是否有正在进行的加载
            if (_pendingImageInfoLoads.TryGetValue(imageId, out var existingTcs))
            {
                return await existingTcs.Task;
            }
            
            _statistics.CacheMisses++;
            
            // 创建新的加载任务
            var tcs = new TaskCompletionSource<EnhancedImageInfo>();
            _pendingImageInfoLoads[imageId] = tcs;
            
            try
            {
                // 使用旧系统的协程加载
                MainThreadInvoker.Invoke(() =>
                {
                    SharedCoroutineStarter.Instance.StartCoroutine(
                        _legacyProvider.TryCacheSingleImage(
                            imageId,
                            imageUrl,
                            isAnimated,
                            (info) =>
                            {
                                tcs.SetResult(info);
                                
                                // 更新统计信息
                                if (info != null)
                                {
                                    UpdateStatisticsForImageInfo(info);
                                }
                            },
                            forcedHeight: forcedHeight
                        )
                    );
                });
                
                return await tcs.Task;
            }
            finally
            {
                _pendingImageInfoLoads.TryRemove(imageId, out _);
            }
        }
        
        /// <summary>
        /// 预加载多个图片信息
        /// </summary>
        public async Task PreloadImageInfosAsync(IEnumerable<(string id, string url, bool isAnimated)> images)
        {
            if (images == null)
                return;
                
            var tasks = images.Select(img => GetOrLoadImageInfoAsync(img.id, img.url, img.isAnimated)).ToList();
            await Task.WhenAll(tasks);
            
            Logger.Info($"Preloaded {tasks.Count(t => t.Result != null)} image infos successfully");
        }
        
        /// <summary>
        /// 更新图片信息的统计数据
        /// </summary>
        private void UpdateStatisticsForImageInfo(EnhancedImageInfo info)
        {
            if (info == null)
                return;
                
            long memoryUsage = 0;
            
            // 计算静态图片内存
            if (info.Sprite != null && info.Sprite.texture != null)
            {
                memoryUsage += info.Sprite.texture.width * info.Sprite.texture.height * 4;
            }
            
            // 计算动画内存
            if (info.AnimControllerData != null && info.AnimControllerData.Sprites != null)
            {
                foreach (var sprite in info.AnimControllerData.Sprites)
                {
                    if (sprite != null && sprite.texture != null)
                    {
                        memoryUsage += sprite.texture.width * sprite.texture.height * 4;
                    }
                }
            }
            
            _statistics.TotalMemoryBytes += memoryUsage;
        }
        
        /// <summary>
        /// 注册图片到字体
        /// </summary>
        public bool TryRegisterImageToFont(string imageId, EnhancedFontInfo font)
        {
            if (string.IsNullOrEmpty(imageId) || font == null)
                return false;
                
            // 获取缓存的图片信息
            if (!_legacyProvider.CachedImageInfo.TryGetValue(imageId, out var imageInfo))
            {
                Logger.Warn($"Image {imageId} not found in cache");
                return false;
            }
            
            // 注册到字体
            return font.TryRegisterImageInfo(imageInfo, out _);
        }
    }
}