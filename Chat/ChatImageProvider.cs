using BeatSaberMarkupLanguage.Animations;
using ChatCore.Models;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Zenject;


namespace EnhancedStreamChat.Chat
{
    public class ActiveDownload
    {
        public bool IsCompleted = false;
        public UnityWebRequest Request;
        public Action<byte[]> Finally;
    }

    public class ChatImageProvider : MonoBehaviour, IInitializable
    {
        public event Action<string> OnImageCached;
        private const int MAX_IMAGE_CACHE = 800; // 上限以避免长期运行占用过大
        private const int MAX_SPRITESHEET_CACHE = 200;
        public enum ESCAnimationType
        {
            NONE,
            GIF,
            APNG,
            WEBP,
            MAYBE_GIF
        }
        public ConcurrentDictionary<string, EnhancedImageInfo> CachedImageInfo { get; } = new ConcurrentDictionary<string, EnhancedImageInfo>();
        private readonly ConcurrentDictionary<string, ActiveDownload> _activeDownloads = new ConcurrentDictionary<string, ActiveDownload>();
        private readonly ConcurrentDictionary<string, Texture2D> _cachedSpriteSheets = new ConcurrentDictionary<string, Texture2D>();
        
        // 单例实例保留用于向后兼容
        private static ChatImageProvider _instance;
        public static ChatImageProvider instance 
        { 
            get 
            {
                if (_instance == null)
                {
                    Logger.Warn("ChatImageProvider.instance accessed before initialization. This should be replaced with dependency injection.");
                }
                return _instance;
            }
        }
        
        // Zenject 构造函数
        [Inject]
        public void Construct()
        {
            // 当前 ChatImageProvider 没有依赖项
            // 未来可以在这里注入其他服务
        }
        
        // IInitializable 实现
        public void Initialize()
        {
            _instance = this; // 设置单例实例用于向后兼容
            Logger.Log.Info("ChatImageProvider initialized as Zenject service");
        }
        /// <summary>
        /// Retrieves the requested content from the provided Uri.
        /// <para>
        /// The <paramref name="Finally"/> callback will *always* be called for this function. If it returns an empty byte array, that should be considered a failure.
        /// </para>
        /// </summary>
        /// <param name="uri">The resource location</param>
        /// <param name="Finally">A callback that occurs after the resource is retrieved. This will always occur even if the resource is already cached.</param>
        /// <param name="isRetry">Retry</param>
        public IEnumerator DownloadContent(string uri, Action<byte[]> Finally, bool isRetry = false)
        {
            if (string.IsNullOrEmpty(uri)) {
                Logger.Error($"URI is null or empty in request for resource {uri}. Aborting!");
                Finally?.Invoke(null);
                yield break;
            }
            
            // 处理特殊的URL路径（参考v3版本）
            uri = uri.Replace(@"static/dark/3.0", @"default/dark/3.0");
            
            // 将 HTTP URL 转换为 HTTPS 以避免 Unity 的安全限制
            if (uri.StartsWith("http://") && !uri.StartsWith("http://localhost") && !uri.StartsWith("http://127.0.0.1")) {
                uri = uri.Replace("http://", "https://");
                Logger.Debug($"Converted HTTP to HTTPS: {uri}");
            }

            if (!isRetry && this._activeDownloads.TryGetValue(uri, out var activeDownload)) {
                Logger.Info($"Request already active for {uri}");
                activeDownload.Finally -= Finally;
                activeDownload.Finally += Finally;
                yield return new WaitUntil(() => activeDownload.IsCompleted);
                yield break;
            }

            using (var wr = UnityWebRequest.Get(uri)) {
                activeDownload = new ActiveDownload()
                {
                    Finally = Finally,
                    Request = wr
                };
                this._activeDownloads.TryAdd(uri, activeDownload);

                yield return wr.SendWebRequest();
                if (wr.result == UnityWebRequest.Result.ProtocolError) {
                    // Failed to download due to http error, don't retry
                    Logger.Error($"An http error occurred during request to {uri}. Aborting! {wr.error}");
                    activeDownload.Finally?.Invoke(new byte[0]);
                    this._activeDownloads.TryRemove(uri, out var d1);
                    yield break;
                }

                if (wr.result == UnityWebRequest.Result.ConnectionError) {
                    if (!isRetry) {
                        Logger.Error($"A network error occurred during request to {uri}. Retrying in 3 seconds... {wr.error}");
                        yield return new WaitForSeconds(3);
                        // this.StartCoroutine(this.DownloadContent(uri, Finally, true));
                        _ = SharedCoroutineStarter.Instance.StartCoroutine(this.DownloadContent(uri, Finally, true));
                        yield break;
                    }
                    activeDownload.Finally?.Invoke(new byte[0]);
                    this._activeDownloads.TryRemove(uri, out var d2);
                    yield break;
                }

                var data = wr.downloadHandler.data;
                activeDownload.Finally?.Invoke(data);
                activeDownload.IsCompleted = true;
                this._activeDownloads.TryRemove(uri, out var d3);
            }
        }

        public IEnumerator PrecacheAnimatedImage(string uri, string id, int forcedHeight = -1)
        {
            yield return this.TryCacheSingleImage(id, uri, true);
        }


        private void SetImageHeight(ref int spriteHeight, ref int spriteWidth, int height)
        {
            /*Logger.Debug($"Origin size: {spriteHeight}x{spriteWidth}");*/
            var scale = 1.0f;
            if (spriteHeight != (float)height) {
                scale = (float)height / spriteHeight;
            }
            spriteWidth = (int)(scale * spriteWidth);
            spriteHeight = (int)(scale * spriteHeight);
            /*Logger.Debug($"New size: {spriteHeight}x{spriteWidth}, Scale: {scale}");*/
        }

        public IEnumerator TryCacheSingleImage(string id, string uri, bool isAnimated, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (this.CachedImageInfo.TryGetValue(id, out var info)) {
                Finally?.Invoke(info);
                yield break;
            }
            var bytes = new byte[0];
            yield return this.DownloadContent(uri, (b) => bytes = b);
            yield return this.OnSingleImageCached(bytes, id, isAnimated, Finally, forcedHeight);
        }

        public IEnumerator OnSingleImageCached(byte[] bytes, string id, bool isAnimated, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (bytes.Length == 0) {
                Finally?.Invoke(null);
                yield break;
            }

            Sprite sprite = null;
            int spriteWidth = 0, spriteHeight = 0;
            AnimationControllerData animControllerData = null;
            if (isAnimated) {
                // 参考 v3 实现，直接使用 AnimationLoader，不需要 Task.Run 包装
                // AnimationLoader 内部已经正确处理了线程安全问题
                AnimationData animData = null;
                Task<AnimationData> task = null;
                
                // 将 try-catch 移到 yield 之外
                try {
                    // 直接调用 AnimationLoader.ProcessGifAsync
                    task = AnimationLoader.ProcessGifAsync(bytes);
                }
                catch (Exception ex) {
                    Logger.Error($"Exception starting animation processing: {ex}");
                    Finally?.Invoke(null);
                    yield break;
                }
                
                // 使用协程等待任务完成
                yield return new WaitUntil(() => task.IsCompleted);
                
                if (task.IsFaulted) {
                    Logger.Error($"Error processing animated image: {task.Exception?.GetBaseException()}");
                    Finally?.Invoke(null);
                    yield break;
                }
                
                try {
                    animData = task.Result;
                    
                    // 注册动画到 AnimationController
                    animControllerData = AnimationController.Instance.Register(id, animData);
                    
                    // 使用第一帧作为预览 sprite
                    sprite = animControllerData.Sprites.FirstOrDefault();
                    spriteWidth = animData.Width;
                    spriteHeight = animData.Height;
                }
                catch (Exception ex) {
                    Logger.Error($"Exception registering animation: {ex}");
                    Finally?.Invoke(null);
                    yield break;
                }
            }
            else {
                // 确保在主线程上创建纹理，并添加重试机制
                var maxRetries = 3;
                var retryCount = 0;
                var success = false;
                
                while (retryCount < maxRetries && !success) {
                    var tcs = new TaskCompletionSource<bool>();
                    MainThreadInvoker.Invoke(() => {
                        try {
                            // 检查图形设备是否可用
                            if (UnityEngine.SystemInfo.graphicsDeviceID == 0) {
                                Logger.Warn($"Graphics device not available, retry {retryCount + 1}/{maxRetries}");
                                tcs.SetResult(false);
                                return;
                            }
                            
                            sprite = GraphicUtils.LoadSpriteRaw(bytes);
                            if (sprite != null && sprite.texture != null) {
                                spriteWidth = sprite.texture.width;
                                spriteHeight = sprite.texture.height;
                                tcs.SetResult(true);
                            }
                            else {
                                Logger.Error("Failed to load sprite from bytes - sprite or texture is null");
                                sprite = null;
                                tcs.SetResult(false);
                            }
                        }
                        catch (Exception ex) {
                            Logger.Error($"Exception loading sprite: {ex}");
                            sprite = null;
                            tcs.SetResult(false);
                        }
                    });
                    
                    yield return new WaitUntil(() => tcs.Task.IsCompleted);
                    success = tcs.Task.Result;
                    
                    if (!success) {
                        retryCount++;
                        if (retryCount < maxRetries) {
                            // 等待一小段时间再重试
                            yield return new WaitForSeconds(0.1f);
                        }
                    }
                }
                
                if (!success) {
                    Logger.Error($"Failed to load sprite after {maxRetries} attempts");
                    sprite = null;
                }
            }
            EnhancedImageInfo ret = null;
            if (sprite != null) {
                if (forcedHeight != -1) {
                    this.SetImageHeight(ref spriteHeight, ref spriteWidth, forcedHeight);
                }
                ret = new EnhancedImageInfo()
                {
                    ImageId = id,
                    Sprite = sprite,
                    Width = spriteWidth,
                    Height = spriteHeight,
                    AnimControllerData = animControllerData
                };
                if (this.CachedImageInfo.TryAdd(id, ret))
                {
                    TrimCachesIfNeeded();
                    try { OnImageCached?.Invoke(id); } catch (Exception ex) { Logger.Warn($"OnImageCached handler threw: {ex.Message}"); }
                }
            }
            Finally?.Invoke(ret);
        }

        public IEnumerator TryCacheSpriteSheetImage(string id, string uri, ImageRect rect, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (this.CachedImageInfo.TryGetValue(id, out var info)) {
                Finally?.Invoke(info);
                yield break;
            }
            if (!this._cachedSpriteSheets.TryGetValue(uri, out var tex) || tex == null) {
                byte[] downloadedBytes = null;
                yield return this.DownloadContent(uri, (bytes) => downloadedBytes = bytes);
                
                if (downloadedBytes != null) {
                    // 确保在主线程上创建纹理，并添加重试机制
                    var maxRetries = 3;
                    var retryCount = 0;
                    var success = false;
                    
                    while (retryCount < maxRetries && !success) {
                        var tcs = new TaskCompletionSource<bool>();
                        MainThreadInvoker.Invoke(() => {
                            try {
                                // 检查图形设备是否可用
                                if (UnityEngine.SystemInfo.graphicsDeviceID == 0) {
                                    Logger.Warn($"Graphics device not available for sprite sheet, retry {retryCount + 1}/{maxRetries}");
                                    tcs.SetResult(false);
                                    return;
                                }
                                
                                tex = GraphicUtils.LoadTextureRaw(downloadedBytes);
                                tcs.SetResult(tex != null);
                            }
                            catch (Exception ex) {
                                Logger.Error($"Failed to load texture: {ex}");
                                tex = null;
                                tcs.SetResult(false);
                            }
                        });
                        
                        yield return new WaitUntil(() => tcs.Task.IsCompleted);
                        success = tcs.Task.Result;
                        
                        if (!success) {
                            retryCount++;
                            if (retryCount < maxRetries) {
                                yield return new WaitForSeconds(0.1f);
                            }
                        }
                    }
                    
                    if (success && tex != null) {
                        this._cachedSpriteSheets[uri] = tex;
                        TrimCachesIfNeeded();
                    }
                    else {
                        Logger.Error($"Failed to load sprite sheet after {maxRetries} attempts");
                    }
                }
            }
            this.CacheSpriteSheetImage(id, rect, tex, Finally, forcedHeight);
        }

        private void CacheSpriteSheetImage(string id, ImageRect rect, Texture2D tex, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (tex == null) {
                Finally?.Invoke(null);
                return;
            }
            int spriteWidth = rect.Width, spriteHeight = rect.Height;
            var sprite = Sprite.Create(tex, new Rect(rect.X, tex.height - rect.Y - spriteHeight, spriteWidth, spriteHeight), new Vector2(0, 0));
            sprite.texture.wrapMode = TextureWrapMode.Clamp;
            EnhancedImageInfo ret = null;
            if (sprite != null) {
                if (forcedHeight != -1) {
                    this.SetImageHeight(ref spriteWidth, ref spriteHeight, forcedHeight);
                }
                ret = new EnhancedImageInfo()
                {
                    ImageId = id,
                    Sprite = sprite,
                    Width = spriteWidth,
                    Height = spriteHeight,
                    AnimControllerData = null
                };
                if (this.CachedImageInfo.TryAdd(id, ret))
                {
                    TrimCachesIfNeeded();
                    try { OnImageCached?.Invoke(id); } catch (Exception ex) { Logger.Warn($"OnImageCached handler threw: {ex.Message}"); }
                }
            }
            Finally?.Invoke(ret);
        }

        internal void ClearCache()
        {
            if (CachedImageInfo.Count > 0) {
                foreach (var info in CachedImageInfo.Values) {
                    GameObject.Destroy(info.Sprite);
                }
                CachedImageInfo.Clear();
            }
        }

        private void TrimCachesIfNeeded()
        {
            // 限制单图缓存大小
            var over = this.CachedImageInfo.Count - MAX_IMAGE_CACHE;
            if (over > 0)
            {
                foreach (var key in this.CachedImageInfo.Keys.Take(over))
                {
                    if (this.CachedImageInfo.TryRemove(key, out var info))
                    {
                        if (info?.Sprite != null)
                        {
                            GameObject.Destroy(info.Sprite);
                        }
                    }
                }
            }

            // 限制雪碧图缓存大小
            over = this._cachedSpriteSheets.Count - MAX_SPRITESHEET_CACHE;
            if (over > 0)
            {
                foreach (var key in this._cachedSpriteSheets.Keys.Take(over))
                {
                    if (this._cachedSpriteSheets.TryRemove(key, out var tex) && tex != null)
                    {
                        GameObject.Destroy(tex);
                    }
                }
            }
        }
    }
}
