using BeatSaberMarkupLanguage.Animations;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

    public class ChatImageProvider
    {
        public enum ESCAnimationType
        {
            NONE,
            GIF,
            APNG,
            WEBP,
            MAYBE_GIF
        }

        [Inject]
        public ChatImageProvider(EnhancedImageInfo.Pool pool)
        {
            this._imageInfoContaner = new MemoryPoolContainer<EnhancedImageInfo>(pool);
        }

        public ConcurrentDictionary<string, EnhancedImageInfo> CachedImageInfo { get; } = new ConcurrentDictionary<string, EnhancedImageInfo>();
        private readonly ConcurrentDictionary<string, ActiveDownload> _activeDownloads = new ConcurrentDictionary<string, ActiveDownload>();
        private readonly MemoryPoolContainer<EnhancedImageInfo> _imageInfoContaner;
        private static readonly byte[] s_animattedGIF89aPattern = Encoding.ASCII.GetBytes("GIF89a");
        private static readonly byte[] s_animattedGIF87aPattern = Encoding.ASCII.GetBytes("GIF87a");
        private const int s_maxConcurrentDownloads = 6;
        private const string s_bilibiliImageUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
        private const string s_bilibiliImageReferer = "https://www.bilibili.com/";
        private readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(s_maxConcurrentDownloads, s_maxConcurrentDownloads);

        //private readonly ConcurrentDictionary<string, Texture2D> _cachedSpriteSheets = new ConcurrentDictionary<string, Texture2D>();
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
            uri = uri.Replace(@"static/dark/3.0", @"default/dark/3.0");
            uri = UpgradeToHttpsIfNeeded(uri);
            ActiveDownload activeDownload;
            if (!isRetry) {
                if (this.TryJoinActiveDownload(uri, Finally, out activeDownload)) {
                    yield return new WaitUntil(() => activeDownload.IsCompleted);
                    yield break;
                }

                activeDownload = new ActiveDownload() {
                    Finally = Finally,
                };

                if (!this._activeDownloads.TryAdd(uri, activeDownload)) {
                    if (this.TryJoinActiveDownload(uri, Finally, out activeDownload)) {
                        yield return new WaitUntil(() => activeDownload.IsCompleted);
                        yield break;
                    }

                    Logger.Error($"Failed to start or join active download for {uri}.");
                    Finally?.Invoke(new byte[0]);
                    yield break;
                }
            }
            else if (!this._activeDownloads.TryGetValue(uri, out activeDownload)) {
                activeDownload = new ActiveDownload();
                _ = this._activeDownloads.TryAdd(uri, activeDownload);
            }

            var waitTask = this._downloadSemaphore.WaitAsync();
            yield return new WaitUntil(() => waitTask.IsCompleted);
            if (waitTask.IsFaulted || waitTask.IsCanceled) {
                Logger.Error($"Failed to acquire download slot for {uri}.");
                this.CompleteActiveDownload(uri, activeDownload, new byte[0]);
                yield break;
            }

            try {
                using (var wr = UnityWebRequest.Get(uri)) {
                    if (this.IsBilibiliImageHost(uri)) {
                        wr.SetRequestHeader("User-Agent", s_bilibiliImageUserAgent);
                        wr.SetRequestHeader("Referer", s_bilibiliImageReferer);
                    }

                    activeDownload.Request = wr;

                    yield return wr.SendWebRequest();
                    switch (wr.result) {
                        case UnityWebRequest.Result.InProgress:
                            Logger.Error($"Unexpected in-progress state after send for {uri}.");
                            this.CompleteActiveDownload(uri, activeDownload, new byte[0]);
                            yield break;
                        case UnityWebRequest.Result.Success:
                            break;
                        case UnityWebRequest.Result.ConnectionError:
                        case UnityWebRequest.Result.DataProcessingError:
                            if (!isRetry) {
                                Logger.Error($"A network error occurred during request to {uri}. Retrying in 3 seconds... {wr.error}");
                                yield return new WaitForSeconds(3);
                                _ = SharedCoroutineStarter.Instance.StartCoroutine(this.DownloadContent(uri, null, true));
                                yield break;
                            }

                            this.CompleteActiveDownload(uri, activeDownload, new byte[0]);
                            yield break;
                        case UnityWebRequest.Result.ProtocolError:
                        default:
                            // Failed to download due to http error, don't retry
                            Logger.Error($"An http error occurred during request to {uri}. Aborting! {wr.error}");
                            this.CompleteActiveDownload(uri, activeDownload, new byte[0]);
                            yield break;
                    }

                    this.CompleteActiveDownload(uri, activeDownload, wr.downloadHandler.data);
                }
            }
            finally {
                this._downloadSemaphore.Release();
            }
        }

        private bool TryJoinActiveDownload(string uri, Action<byte[]> callback, out ActiveDownload activeDownload)
        {
            if (this._activeDownloads.TryGetValue(uri, out activeDownload)) {
                Logger.Info($"Request already active for {uri}");
                if (callback != null) {
                    activeDownload.Finally -= callback;
                    activeDownload.Finally += callback;
                }
                return true;
            }

            return false;
        }

        private static string UpgradeToHttpsIfNeeded(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) || !uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) {
                return uri;
            }

            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)
                && (string.Equals(parsedUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parsedUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))) {
                return uri;
            }

            return $"https://{uri.Substring("http://".Length)}";
        }

        private bool IsBilibiliImageHost(string uri)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri)) {
                return false;
            }

            var host = parsedUri.Host;
            return host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".hdslb.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "bilibili.com", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "hdslb.com", StringComparison.OrdinalIgnoreCase);
        }

        private void CompleteActiveDownload(string uri, ActiveDownload activeDownload, byte[] bytes)
        {
            activeDownload.IsCompleted = true;
            activeDownload.Finally?.Invoke(bytes);
            _ = this._activeDownloads.TryRemove(uri, out _);
        }

        public IEnumerator PrecacheAnimatedImage(string uri, string id, int forcedHeight = -1)
        {
            yield return this.TryCacheSingleImage(id, uri, ESCAnimationType.GIF);
        }

        private void SetImageHeight(ref int spriteHeight, ref int spriteWidth, int height)
        {
            var scale = 1.0f;
            if (spriteHeight != (float)height) {
                scale = (float)height / spriteHeight;
            }
            spriteWidth = (int)(scale * spriteWidth);
            spriteHeight = (int)(scale * spriteHeight);
        }

        public IEnumerator TryCacheSingleImage(string id, string uri, ESCAnimationType animatedType, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (this.CachedImageInfo.TryGetValue(id, out var info)) {
                Finally?.Invoke(info);
                yield break;
            }
            var bytes = new byte[0];
            yield return this.DownloadContent(uri, (b) => bytes = b);
            yield return this.OnSingleImageCached(bytes, id, animatedType, Finally, forcedHeight);
        }

        public IEnumerator OnSingleImageCached(byte[] bytes, string id, ESCAnimationType animatedType, Action<EnhancedImageInfo> Finally = null, int forcedHeight = -1)
        {
            if (bytes == null || bytes.Length == 0) {
                Finally?.Invoke(null);
                yield break;
            }

            Sprite sprite = null;
            int spriteWidth = 0, spriteHeight = 0;
            AnimationControllerData animControllerData = null;
            AnimationData anmData = null;
            Task<AnimationData> task;
            switch (animatedType) {
                case ESCAnimationType.GIF:
                    task = AnimationLoader.ProcessGifAsync(bytes);
                    yield return new WaitWhile(() => !task.IsCompleted);
                    anmData = task.Result;
                    animControllerData = AnimationController.Instance.Register(id, anmData);
                    sprite = animControllerData.Sprites.FirstOrDefault();
                    spriteHeight = anmData.Height;
                    spriteWidth = anmData.Width;
                    break;
                case ESCAnimationType.APNG:
                    task = AnimationLoader.ProcessApngAsync(bytes);
                    yield return new WaitWhile(() => !task.IsCompleted);
                    anmData = task.Result;
                    animControllerData = AnimationController.Instance.Register(id, anmData);
                    sprite = animControllerData.Sprites.FirstOrDefault();
                    spriteHeight = anmData.Height;
                    spriteWidth = anmData.Width;
                    break;

                case ESCAnimationType.MAYBE_GIF:
                    if (6 <= bytes.Length && (this.ContainBytePattern(bytes, s_animattedGIF89aPattern) || this.ContainBytePattern(bytes, s_animattedGIF87aPattern))) {
                        task = AnimationLoader.ProcessGifAsync(bytes);
                        yield return new WaitWhile(() => !task.IsCompleted);
                        anmData = task.Result;
                        animControllerData = AnimationController.Instance.Register(id, anmData);
                        sprite = animControllerData.Sprites.FirstOrDefault();
                        spriteHeight = anmData.Height;
                        spriteWidth = anmData.Width;
                    }
                    else {
                        try {
                            sprite = GraphicUtils.LoadSpriteRaw(bytes);
                            spriteWidth = sprite.texture.width;
                            spriteHeight = sprite.texture.height;
                        }
                        catch (Exception ex) {
                            Logger.Error(ex);
                            sprite = null;
                        }
                    }
                    break;
                case ESCAnimationType.WEBP:
                case ESCAnimationType.NONE:
                default:
                    try {
                        sprite = GraphicUtils.LoadSpriteRaw(bytes);
                        spriteWidth = sprite.texture.width;
                        spriteHeight = sprite.texture.height;
                    }
                    catch (Exception ex) {
                        Logger.Error(ex);
                        sprite = null;
                    }
                    break;
            }
            var ret = this._imageInfoContaner.Spawn();
            EnhancedImageInfo finalInfo = null;
            if (sprite != null) {
                if (forcedHeight != -1) {
                    this.SetImageHeight(ref spriteHeight, ref spriteWidth, forcedHeight);
                }
                ret.ImageId = id;
                ret.Sprite = sprite;
                ret.Width = spriteWidth;
                ret.Height = spriteHeight;
                ret.AnimControllerData = animControllerData;
                _ = this.CachedImageInfo.TryAdd(id, ret);
                finalInfo = ret;
            }
            else {
                this._imageInfoContaner.Despawn(ret);
            }
            Finally?.Invoke(finalInfo);
        }
        internal void ClearCache()
        {
            if (this.CachedImageInfo.Count > 0) {
                var textureRefCounts = new Dictionary<int, int>();
                foreach (var info in this.CachedImageInfo.Values) {
                    var texture = info?.Sprite?.texture;
                    if (texture == null) {
                        continue;
                    }

                    var textureId = texture.GetInstanceID();
                    _ = textureRefCounts.TryGetValue(textureId, out var refCount);
                    textureRefCounts[textureId] = refCount + 1;
                }

                var destroyedTextureIds = new HashSet<int>();
                foreach (var info in this.CachedImageInfo.Values) {
                    if (info == null) {
                        continue;
                    }

                    var sprite = info.Sprite;
                    var texture = sprite?.texture;
                    var textureId = texture?.GetInstanceID();
                    var isSharedAnimationTexture = info.AnimControllerData != null;
                    var isSharedTexture = textureId != null
                        && textureRefCounts.TryGetValue(textureId.Value, out var textureRefCount)
                        && textureRefCount > 1;

                    if (sprite != null && !isSharedAnimationTexture) {
                        GameObject.Destroy(sprite);
                    }

                    if (texture != null && !isSharedAnimationTexture && !isSharedTexture && destroyedTextureIds.Add(texture.GetInstanceID())) {
                        GameObject.Destroy(texture);
                    }

                    info.ImageId = null;
                    info.Sprite = null;
                    info.Width = 0;
                    info.Height = 0;
                    info.AnimControllerData = null;
                    this._imageInfoContaner.Despawn(info);
                }
                this.CachedImageInfo.Clear();
            }
        }

        /// <summary>
        /// Fast lookup for byte pattern
        /// </summary>
        /// <param name="array">Input array</param>
        /// <param name="pattern">Lookup pattern</param>
        /// <returns></returns>
        private bool ContainBytePattern(byte[] array, byte[] pattern)
        {
            var patternPosition = 0;
            for (var i = 0; i < array.Length; ++i) {
                if (array[i] != pattern[patternPosition]) {
                    patternPosition = 0;
                    continue;
                }

                patternPosition++;
                if (patternPosition == pattern.Length) {
                    return true;
                }
            }
            return patternPosition == pattern.Length;
        }
    }
}
