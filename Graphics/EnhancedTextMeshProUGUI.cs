using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Interfaces;
using EnhancedStreamChat.Utilities;
using BeatSaberMarkupLanguage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUI : TextMeshProUGUI
    {
        public IESCChatMessage ChatMessage { get; set; } = null;
        public EnhancedFontInfo FontInfo => this._fontManager.FontInfo;

        private MemoryPoolContainer<EnhancedImage> _imagePool;
        private ESCFontManager _fontManager;
        private bool _rebuiled = false;
        private readonly LazyCopyHashSet<ILatePreRenderRebuildReciver> _recivers = new LazyCopyHashSet<ILatePreRenderRebuildReciver>();
        public ILazyCopyHashSet<ILatePreRenderRebuildReciver> LazyCopyHashSet => this._recivers;

        private const string BilibiliAvatarImageIdPrefix = "Bili_avatar_";
        private readonly Dictionary<EnhancedImage, RectTransform> _avatarMaskWrappersByImage = new Dictionary<EnhancedImage, RectTransform>();
        private readonly Stack<RectTransform> _avatarMaskWrapperPool = new Stack<RectTransform>();
        private static readonly ProfilerMarker BadgeRebuildProfilerMarker = new ProfilerMarker("ESC.BadgeRebuild");

#if DEBUG
        private static readonly BadgePerfAggregator s_badgePerfAggregator = new BadgePerfAggregator();
#endif

#if BADGE_DEBUG
        private static long s_nextTask14DebugLogTicks;
#endif

        private static readonly object s_avatarMaskLock = new object();
        private static Sprite s_avatarCircleMaskSprite;
        private static TMP_FontAsset s_bootstrapFont;

        public void Constract(EnhancedImage.Pool image, ESCFontManager fontManager)
        {
            this._imagePool = new MemoryPoolContainer<EnhancedImage>(image);
            this._fontManager = fontManager;
        }

        protected override void Awake()
        {
            base.Awake();
            this.raycastTarget = false;
            if (!this.font) {
                this.font = GetBootstrapFont();
            }
        }

        private static TMP_FontAsset GetBootstrapFont()
        {
            if (s_bootstrapFont) {
                return s_bootstrapFont;
            }

            s_bootstrapFont = BeatSaberUI.MainTextFont != null ? BeatSaberUI.MainTextFont : TMP_Settings.defaultFontAsset;
            if (s_bootstrapFont) {
                return s_bootstrapFont;
            }

            s_bootstrapFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                .FirstOrDefault(font => font != null && (font.atlasTexture != null || (font.atlasTextures != null && font.atlasTextures.Any(texture => texture != null))));

            return s_bootstrapFont;
        }

        private static Sprite GetOrCreateAvatarCircleMaskSprite()
        {
            lock (s_avatarMaskLock) {
                if (s_avatarCircleMaskSprite != null) {
                    return s_avatarCircleMaskSprite;
                }

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "ESC_AvatarCircleMask"
                };

                var pixels = new Color32[size * size];
                var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                var radius = (size - 1) * 0.5f;

                for (var y = 0; y < size; y++) {
                    for (var x = 0; x < size; x++) {
                        var dx = x - center.x;
                        var dy = y - center.y;
                        var dist = Mathf.Sqrt(dx * dx + dy * dy);
                        var alpha01 = Mathf.Clamp01(radius - dist);
                        var a = (byte)Mathf.RoundToInt(alpha01 * 255f);
                        pixels[(y * size) + x] = new Color32(255, 255, 255, a);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                texture.hideFlags = HideFlags.HideAndDontSave;

                s_avatarCircleMaskSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
                s_avatarCircleMaskSprite.hideFlags = HideFlags.HideAndDontSave;
                return s_avatarCircleMaskSprite;
            }
        }

        private RectTransform RentAvatarMaskWrapper()
        {
            RectTransform wrapper;
            if (this._avatarMaskWrapperPool.Count > 0) {
                wrapper = this._avatarMaskWrapperPool.Pop();
                if (wrapper != null) {
                    wrapper.gameObject.SetActive(true);
                    return wrapper;
                }
            }

            var go = new GameObject("ESC_AvatarMask", typeof(RectTransform), typeof(Image), typeof(Mask));
            go.layer = this.gameObject.layer;

            wrapper = go.GetComponent<RectTransform>();
            wrapper.SetParent(this.rectTransform, false);
            wrapper.anchorMin = new Vector2(0.5f, 0.5f);
            wrapper.anchorMax = new Vector2(0.5f, 0.5f);
            wrapper.pivot = new Vector2(0, 0);

            var maskImage = go.GetComponent<Image>();
            maskImage.raycastTarget = false;
            maskImage.color = Color.white;
            maskImage.sprite = GetOrCreateAvatarCircleMaskSprite();

            var mask = go.GetComponent<Mask>();
            mask.showMaskGraphic = false;

            return wrapper;
        }

        private void ReturnAvatarMaskWrapper(RectTransform wrapper)
        {
            if (wrapper == null) {
                return;
            }

            try {
                for (var i = wrapper.childCount - 1; i >= 0; i--) {
                    wrapper.GetChild(i).SetParent(this.rectTransform, false);
                }
                wrapper.gameObject.SetActive(false);
                this._avatarMaskWrapperPool.Push(wrapper);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private bool IsBilibiliAvatar(EnhancedImageInfo imageInfo)
        {
            return imageInfo?.ImageId?.StartsWith(BilibiliAvatarImageIdPrefix, StringComparison.Ordinal) == true;
        }

        private void DespawnImage(EnhancedImage img)
        {
            if (img == null) {
                return;
            }

            this._imagePool?.Despawn(img);

            if (this._avatarMaskWrappersByImage.TryGetValue(img, out var wrapper)) {
                this._avatarMaskWrappersByImage.Remove(img);
                this.ReturnAvatarMaskWrapper(wrapper);
            }
        }

        public void ClearImages()
        {
            try {
                while (this._imagePool?.activeItems != null && this._imagePool?.activeItems.Any() == true) {
                    this.DespawnImage(this._imagePool.activeItems[0]);
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        public override void Rebuild(CanvasUpdate update)
        {
            switch (update) {
                case CanvasUpdate.LatePreRender:
                    _ = MainThreadInvoker.Invoke(() =>
                    {
                        #if BADGE_DEBUG
                        if (ShouldLogTask14Debug()) {
                            var textSnapshot = this.text ?? string.Empty;
                            if (textSnapshot.Length > 80) {
                                textSnapshot = textSnapshot.Substring(0, 80) + "...";
                            }
                            textSnapshot = textSnapshot.Replace("\r", "\\r").Replace("\n", "\\n");
                            var linkCount = this.textInfo?.linkCount ?? -1;
                            Logger.Info("[TASK14DBG][ESC-REBUILD] " + $"Rebuild LatePreRender start textPrefix={textSnapshot} linkCount={linkCount}");
                        }
                        #endif

                        this.ClearImages();
#if DEBUG
                        var rebuildStartTicks = Stopwatch.GetTimestamp();
#endif
                        using (BadgeRebuildProfilerMarker.Auto()) {
                            for (var i = 0; i < this.textInfo.characterCount; i++) {
                            var c = this.textInfo.characterInfo[i];
                            if (!c.isVisible || string.IsNullOrEmpty(this.text) || c.index >= this.text.Length) {
                                // Skip invisible/empty/out of range chars
                                continue;
                            }

                            uint character = this.text[c.index];
                            if (c.index + 1 < this.text.Length && char.IsSurrogatePair(this.text[c.index], this.text[c.index + 1])) {
                                // If it's a surrogate pair, convert the character
                                character = (uint)char.ConvertToUtf32(this.text[c.index], this.text[c.index + 1]);
                            }
                            if (this.FontInfo == null || !this.FontInfo.TryGetImageInfo(character, out var imageInfo) || imageInfo is null) {
                                //Logger.Debug($"{c.character}:{character}, Skip characters that have no imageInfo registered");
                                continue;
                            }
                            var img = this._imagePool?.Spawn();
                            if (img == null) {
                                continue;
                            }
                            try {
                                var fontScale = 0.010f * this.fontSize;
                                var isAvatar = this.IsBilibiliAvatar(imageInfo);

                                if (isAvatar) {
                                    var wrapper = this.RentAvatarMaskWrapper();
                                    this._avatarMaskWrappersByImage[img] = wrapper;

                                    wrapper.SetParent(this.rectTransform, false);
                                    wrapper.localScale = new Vector3(fontScale * 1.08f, fontScale * 1.08f, fontScale * 1.08f);
                                    wrapper.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                                    wrapper.localPosition = c.topLeft - new Vector3(0, imageInfo.Height * fontScale * 0.558f / 2);
                                    wrapper.localRotation = Quaternion.identity;

                                    img.rectTransform.SetParent(wrapper, false);
                                    img.rectTransform.localScale = Vector3.one;
                                    img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                                    img.rectTransform.localPosition = Vector3.zero;
                                    img.rectTransform.localRotation = Quaternion.identity;
                                }
                                else {
                                    img.rectTransform.SetParent(this.rectTransform, false);
                                    img.rectTransform.localScale = new Vector3(fontScale * 1.08f, fontScale * 1.08f, fontScale * 1.08f);
                                    img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                                    img.rectTransform.localPosition = c.topLeft - new Vector3(0, imageInfo.Height * fontScale * 0.558f / 2);
                                    img.rectTransform.localRotation = Quaternion.identity;
                                }

                                if (imageInfo.AnimControllerData != null) {
                                    img.AnimStateUpdater.ControllerData = imageInfo.AnimControllerData;
                                    img.sprite = imageInfo.AnimControllerData.Sprites[imageInfo.AnimControllerData.UvIndex];
                                }
                                else {
                                    img.sprite = imageInfo.Sprite;
                                }
                                // Keep avatar and metadata overlays on no-glow UI material to avoid halo artifacts.
                                img.material = BeatSaberUtils.UINoGlowMaterial;
                                img.SetAllDirty();
                            }
                            catch (Exception ex) {
                                Logger.Error($"Exception while trying to overlay sprite. {ex}");
                                this.DespawnImage(img);
                            }
                        }
                        }
#if DEBUG
                        var rebuildElapsedMicroseconds = ((Stopwatch.GetTimestamp() - rebuildStartTicks) * 1000000L) / Stopwatch.Frequency;
                        s_badgePerfAggregator.RecordRebuildSample(rebuildElapsedMicroseconds);
#endif
                        this._rebuiled = true;
                    });
                    break;
                case CanvasUpdate.Prelayout:
                case CanvasUpdate.Layout:
                case CanvasUpdate.PostLayout:
                case CanvasUpdate.PreRender:
                case CanvasUpdate.MaxUpdateValue:
                default:
                    break;
            }
            base.Rebuild(update);
        }

        public void AddReciver(ILatePreRenderRebuildReciver reciver)
        {
            this.LazyCopyHashSet.Add(reciver);
        }

        public void RemoveReciver(ILatePreRenderRebuildReciver reciver)
        {
            this.LazyCopyHashSet.Remove(reciver);
        }

        protected void LateUpdate()
        {
            if (this._rebuiled) {
                foreach (var reciver in this._recivers.items) {
                    reciver?.LatePreRenderRebuildHandler(this, EventArgs.Empty);
                }
                this._rebuiled = false;
            }

#if DEBUG
            s_badgePerfAggregator.RecordFrameSample(Time.frameCount, Time.unscaledDeltaTime);
#endif
        }

#if DEBUG
        private sealed class BadgePerfAggregator
        {
            private static readonly long FlushIntervalTicks = Stopwatch.Frequency * 60;
            private static readonly long[] FrameHistogramUpperBoundsUs = { 16667, 20000, 25000, 33333, 50000, 100000, 250000, 500000, 1000000, 2000000, long.MaxValue };
            private static readonly long[] RebuildHistogramUpperBoundsUs = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, long.MaxValue };

            private readonly long[] _frameHistogram = new long[FrameHistogramUpperBoundsUs.Length];
            private readonly long[] _rebuildHistogram = new long[RebuildHistogramUpperBoundsUs.Length];

            private int _lastFrameCount = -1;
            private long _nextFlushTicks = Stopwatch.GetTimestamp() + FlushIntervalTicks;

            private long _frameSampleCount;
            private long _frameTotalUs;
            private long _frameMaxUs;
            private long _frameSpikeCount;

            private long _rebuildSampleCount;
            private long _rebuildTotalUs;
            private long _rebuildMaxUs;

            private bool _hasGcBaseline;
            private long _lastGcTotalMemory;
            private int _lastGen0CollectionCount;
            private int _lastGen1CollectionCount;
            private int _lastGen2CollectionCount;

            public void RecordFrameSample(int frameCount, float frameDeltaTime)
            {
                if (frameCount == this._lastFrameCount) {
                    return;
                }

                this._lastFrameCount = frameCount;
                var frameUs = (long)Math.Max(0f, frameDeltaTime * 1000000f);
                this._frameSampleCount++;
                this._frameTotalUs += frameUs;
                if (frameUs > this._frameMaxUs) {
                    this._frameMaxUs = frameUs;
                }
                if (frameUs > 16667) {
                    this._frameSpikeCount++;
                }

                this._frameHistogram[GetHistogramBucket(FrameHistogramUpperBoundsUs, frameUs)]++;
                this.TryFlushIfNeeded();
            }

            public void RecordRebuildSample(long rebuildUs)
            {
                var clampedUs = Math.Max(0L, rebuildUs);
                this._rebuildSampleCount++;
                this._rebuildTotalUs += clampedUs;
                if (clampedUs > this._rebuildMaxUs) {
                    this._rebuildMaxUs = clampedUs;
                }

                this._rebuildHistogram[GetHistogramBucket(RebuildHistogramUpperBoundsUs, clampedUs)]++;
                this.TryFlushIfNeeded();
            }

            private void TryFlushIfNeeded()
            {
                var now = Stopwatch.GetTimestamp();
                if (now < this._nextFlushTicks) {
                    return;
                }

                this._nextFlushTicks = now + FlushIntervalTicks;

                var totalMemory = GC.GetTotalMemory(false);
                var gen0CollectionCount = GC.CollectionCount(0);
                var gen1CollectionCount = GC.CollectionCount(1);
                var gen2CollectionCount = GC.CollectionCount(2);

                long memoryDeltaBytes = 0;
                var gen0Delta = 0;
                var gen1Delta = 0;
                var gen2Delta = 0;

                if (this._hasGcBaseline) {
                    memoryDeltaBytes = totalMemory - this._lastGcTotalMemory;
                    gen0Delta = gen0CollectionCount - this._lastGen0CollectionCount;
                    gen1Delta = gen1CollectionCount - this._lastGen1CollectionCount;
                    gen2Delta = gen2CollectionCount - this._lastGen2CollectionCount;
                }
                else {
                    this._hasGcBaseline = true;
                }

                this._lastGcTotalMemory = totalMemory;
                this._lastGen0CollectionCount = gen0CollectionCount;
                this._lastGen1CollectionCount = gen1CollectionCount;
                this._lastGen2CollectionCount = gen2CollectionCount;

                var frameAvgMs = this._frameSampleCount > 0 ? (this._frameTotalUs / (double)this._frameSampleCount) / 1000d : 0d;
                var frameP95Ms = GetP95FromHistogram(this._frameHistogram, FrameHistogramUpperBoundsUs, this._frameSampleCount) / 1000d;
                var frameMaxMs = this._frameMaxUs / 1000d;

                var rebuildAvgUs = this._rebuildSampleCount > 0 ? this._rebuildTotalUs / (double)this._rebuildSampleCount : 0d;
                var rebuildP95Us = GetP95FromHistogram(this._rebuildHistogram, RebuildHistogramUpperBoundsUs, this._rebuildSampleCount);

                Logger.Info(
                    $"[BadgePerfAgg/60s] frameSamples={this._frameSampleCount} frameAvgMs={frameAvgMs:F2} frameP95Ms={frameP95Ms:F2} frameMaxMs={frameMaxMs:F2} " +
                    $"frameSpikeCount={this._frameSpikeCount} rebuildSamples={this._rebuildSampleCount} rebuildAvgUs={rebuildAvgUs:F2} rebuildP95Us={rebuildP95Us} " +
                    $"rebuildMaxUs={this._rebuildMaxUs} gcDeltaKB={memoryDeltaBytes / 1024L} gen0Delta={gen0Delta} gen1Delta={gen1Delta} gen2Delta={gen2Delta}");

                this._frameSampleCount = 0;
                this._frameTotalUs = 0;
                this._frameMaxUs = 0;
                this._frameSpikeCount = 0;
                this._rebuildSampleCount = 0;
                this._rebuildTotalUs = 0;
                this._rebuildMaxUs = 0;
                Array.Clear(this._frameHistogram, 0, this._frameHistogram.Length);
                Array.Clear(this._rebuildHistogram, 0, this._rebuildHistogram.Length);
            }

            private static int GetHistogramBucket(long[] bounds, long value)
            {
                for (var i = 0; i < bounds.Length; i++) {
                    if (value <= bounds[i]) {
                        return i;
                    }
                }

                return bounds.Length - 1;
            }

            private static long GetP95FromHistogram(long[] histogram, long[] bounds, long totalCount)
            {
                if (totalCount <= 0) {
                    return 0;
                }

                var threshold = (long)Math.Ceiling(totalCount * 0.95d);
                long cumulative = 0;
                for (var i = 0; i < histogram.Length; i++) {
                    cumulative += histogram[i];
                    if (cumulative >= threshold) {
                        return bounds[i];
                    }
                }

                return bounds[bounds.Length - 1];
            }
        }
#endif

#if BADGE_DEBUG
        private static bool ShouldLogTask14Debug()
        {
            var now = Stopwatch.GetTimestamp();
            if (now < s_nextTask14DebugLogTicks) {
                return false;
            }

            s_nextTask14DebugLogTicks = now + (Stopwatch.Frequency * 60);
            return true;
        }
#endif

        public class Factory : PlaceholderFactory<EnhancedTextMeshProUGUI>
        {
        }
    }
}
