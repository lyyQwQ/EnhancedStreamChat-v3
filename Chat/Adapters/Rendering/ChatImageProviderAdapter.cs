using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Animations;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Chat.Adapters.Rendering
{
    /// <summary>
    /// Adapter that bridges the legacy ChatImageProvider with the new IImageProvider interface
    /// </summary>
    public class ChatImageProviderAdapter : IImageProvider, IInitializable, IDisposable
    {
        private readonly ChatImageProvider _legacyProvider;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Texture2D>> _pendingLoads;
        private readonly ConcurrentDictionary<string, Texture2D> _textureCache;
        private readonly object _lockObject = new object();
        
        // Statistics tracking
        private long _totalMemoryBytes;
        private int _cacheHits;
        private int _cacheMisses;
        
        public ChatImageProviderAdapter()
        {
            _legacyProvider = ChatImageProvider.instance;
            _pendingLoads = new ConcurrentDictionary<string, TaskCompletionSource<Texture2D>>();
            _textureCache = new ConcurrentDictionary<string, Texture2D>();
        }
        
        public void Initialize()
        {
            Logger.Log.Info("[ChatImageProviderAdapter] Initialized");
        }
        
        public void Dispose()
        {
            ClearCache();
            Logger.Log.Info("[ChatImageProviderAdapter] Disposed");
        }
        
        /// <summary>
        /// Gets a texture from URL asynchronously
        /// </summary>
        public async Task<Texture2D> GetTextureAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                Logger.Log.Warn("Attempted to load texture with null or empty URL");
                return null;
            }
            
            // Check texture cache first
            if (_textureCache.TryGetValue(url, out var cachedTexture))
            {
                _cacheHits++;
                return cachedTexture;
            }
            
            // Check if there's already a pending load for this URL
            if (_pendingLoads.TryGetValue(url, out var existingTcs))
            {
                return await existingTcs.Task;
            }
            
            _cacheMisses++;
            
            // Create new load task
            var tcs = new TaskCompletionSource<Texture2D>();
            _pendingLoads[url] = tcs;
            
            try
            {
                // Use legacy provider to download content
                MainThreadInvoker.Invoke(() =>
                {
                    SharedCoroutineStarter.Instance.StartCoroutine(
                        DownloadAndCreateTexture(url, tcs)
                    );
                });
                
                var texture = await tcs.Task;
                
                // Cache the result
                if (texture != null)
                {
                    _textureCache[url] = texture;
                    UpdateMemoryUsage();
                }
                
                return texture;
            }
            finally
            {
                _pendingLoads.TryRemove(url, out _);
            }
        }
        
        /// <summary>
        /// Gets or creates an EnhancedImageInfo from URL
        /// </summary>
        public async Task<EnhancedImageInfo> GetImageInfoAsync(string id, string url, bool isAnimated = false, int forcedHeight = -1)
        {
            if (string.IsNullOrEmpty(url))
                return null;
                
            // Check if already cached in legacy provider
            if (_legacyProvider.CachedImageInfo.TryGetValue(id, out var cachedInfo))
            {
                _cacheHits++;
                return cachedInfo;
            }
            
            _cacheMisses++;
            
            // Load through legacy provider
            var tcs = new TaskCompletionSource<EnhancedImageInfo>();
            
            MainThreadInvoker.Invoke(() =>
            {
                SharedCoroutineStarter.Instance.StartCoroutine(
                    _legacyProvider.TryCacheSingleImage(
                        id,
                        url,
                        isAnimated,
                        (info) => tcs.SetResult(info),
                        forcedHeight: forcedHeight
                    )
                );
            });
            
            return await tcs.Task;
        }
        
        /// <summary>
        /// Preloads multiple images
        /// </summary>
        public async Task PreloadImagesAsync(IEnumerable<string> urls)
        {
            if (urls == null || !urls.Any())
                return;
                
            var tasks = urls.Select(GetTextureAsync).ToList();
            await Task.WhenAll(tasks);
            
            Logger.Log.Info($"Preloaded {tasks.Count(t => t.Result != null)} images successfully");
        }
        
        /// <summary>
        /// Clears all caches
        /// </summary>
        public void ClearCache()
        {
            lock (_lockObject)
            {
                // Clear texture cache
                foreach (var texture in _textureCache.Values)
                {
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                }
                _textureCache.Clear();
                
                // Clear legacy provider cache
                if (_legacyProvider != null && _legacyProvider.CachedImageInfo != null)
                {
                    _legacyProvider.CachedImageInfo.Clear();
                }
                
                // Reset statistics
                _totalMemoryBytes = 0;
                _cacheHits = 0;
                _cacheMisses = 0;
            }
            
            Logger.Log.Info("Image caches cleared");
        }
        
        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                TotalImages = _textureCache.Count + _legacyProvider.CachedImageInfo.Count,
                TotalMemoryBytes = _totalMemoryBytes,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses
            };
        }
        
        /// <summary>
        /// Checks if an image is cached by ID
        /// </summary>
        public bool IsImageCached(string id)
        {
            return _legacyProvider.CachedImageInfo.ContainsKey(id);
        }
        
        /// <summary>
        /// Gets cached image info by ID
        /// </summary>
        public EnhancedImageInfo GetCachedImageInfo(string id)
        {
            _legacyProvider.CachedImageInfo.TryGetValue(id, out var info);
            return info;
        }
        
        /// <summary>
        /// Downloads content and creates texture
        /// </summary>
        private IEnumerator DownloadAndCreateTexture(string url, TaskCompletionSource<Texture2D> tcs)
        {
            Texture2D texture = null;
            
            yield return _legacyProvider.DownloadContent(url, (data) =>
            {
                if (data != null && data.Length > 0)
                {
                    try
                    {
                        texture = new Texture2D(2, 2);
                        if (texture.LoadImage(data))
                        {
                            texture.wrapMode = TextureWrapMode.Clamp;
                            texture.filterMode = FilterMode.Bilinear;
                            Logger.Log.Debug($"Successfully loaded texture from {url}");
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(texture);
                            texture = null;
                            Logger.Log.Error($"Failed to load texture data from {url}");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (texture != null)
                        {
                            UnityEngine.Object.Destroy(texture);
                            texture = null;
                        }
                        Logger.Log.Error($"Exception loading texture from {url}: {ex}");
                    }
                }
                else
                {
                    Logger.Log.Error($"Failed to download data from {url}");
                }
            });
            
            tcs.SetResult(texture);
        }
        
        /// <summary>
        /// Updates memory usage statistics
        /// </summary>
        private void UpdateMemoryUsage()
        {
            long totalMemory = 0;
            
            // Calculate texture memory
            foreach (var texture in _textureCache.Values)
            {
                if (texture != null)
                {
                    // Estimate: width * height * 4 bytes for RGBA32
                    totalMemory += texture.width * texture.height * 4;
                }
            }
            
            // Add legacy provider memory (sprites and animations)
            foreach (var info in _legacyProvider.CachedImageInfo.Values)
            {
                if (info.Sprite != null && info.Sprite.texture != null)
                {
                    totalMemory += info.Sprite.texture.width * info.Sprite.texture.height * 4;
                }
                
                // Add animation memory if applicable
                if (info.AnimControllerData != null && info.AnimControllerData.Sprites != null)
                {
                    foreach (var sprite in info.AnimControllerData.Sprites)
                    {
                        if (sprite != null && sprite.texture != null)
                        {
                            totalMemory += sprite.texture.width * sprite.texture.height * 4;
                        }
                    }
                }
            }
            
            _totalMemoryBytes = totalMemory;
        }
        
        /// <summary>
        /// Registers an animated image with the animation controller
        /// </summary>
        public async Task<AnimationControllerData> RegisterAnimatedImageAsync(string id, byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;
                
            try
            {
                // Process as GIF
                var animData = await AnimationLoader.ProcessGifAsync(data);
                if (animData != null)
                {
                    var controllerData = AnimationController.Instance.Register(id, animData);
                    return controllerData;
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"Failed to register animated image {id}: {ex}");
            }
            
            return null;
        }
        
        /// <summary>
        /// IImageProvider implementation - Gets image as texture
        /// </summary>
        public async Task<Texture2D> GetImageAsync(string url)
        {
            return await GetTextureAsync(url);
        }
        
        /// <summary>
        /// IImageProvider implementation - Preloads images
        /// </summary>
        public async Task PreloadImagesAsync(string[] urls)
        {
            if (urls == null)
                return;
                
            await PreloadImagesAsync(urls.AsEnumerable());
        }
        
        /// <summary>
        /// IImageProvider implementation - Gets or loads image info
        /// </summary>
        public async Task<EnhancedImageInfo> GetOrLoadImageInfoAsync(string imageId, string imageUrl, bool isAnimated = false, int forcedHeight = -1)
        {
            return await GetImageInfoAsync(imageId, imageUrl, isAnimated, forcedHeight);
        }
        
        /// <summary>
        /// IImageProvider implementation - Preloads image infos
        /// </summary>
        public async Task PreloadImageInfosAsync(IEnumerable<(string id, string url, bool isAnimated)> images)
        {
            if (images == null)
                return;
                
            var tasks = images.Select(img => GetOrLoadImageInfoAsync(img.id, img.url, img.isAnimated)).ToList();
            await Task.WhenAll(tasks);
            
            Logger.Log.Info($"Preloaded {tasks.Count(t => t.Result != null)} image infos successfully");
        }
        
        /// <summary>
        /// IImageProvider implementation - Registers image to font
        /// </summary>
        public bool TryRegisterImageToFont(string imageId, EnhancedFontInfo font)
        {
            if (string.IsNullOrEmpty(imageId) || font == null)
                return false;
                
            var imageInfo = GetCachedImageInfo(imageId);
            if (imageInfo == null)
                return false;
                
            return font.TryRegisterImageInfo(imageInfo, out _);
        }
    }
}