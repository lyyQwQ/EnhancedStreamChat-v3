using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Interfaces;
using EnhancedStreamChat.Utilities;
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
        private const int BadgePerfSampleBatchSize = 100;
        private readonly Stopwatch _badgeRebuildStopwatch = new Stopwatch();
        private readonly List<long> _badgeRebuildDurationsUs = new List<long>(BadgePerfSampleBatchSize);
        private int _badgeRebuildSampleCount;
        private float _badgeGcSampleTimer;
        private long _lastGcTotalMemory;
        private int _lastGen0CollectionCount;
        private bool _hasGcBaseline;
#endif

        private static readonly object s_avatarMaskLock = new object();
        private static Sprite s_avatarCircleMaskSprite;

        public void Constract(EnhancedImage.Pool image, ESCFontManager fontManager)
        {
            this._imagePool = new MemoryPoolContainer<EnhancedImage>(image);
            this._fontManager = fontManager;
        }

        protected override void Awake()
        {
            base.Awake();
            this.raycastTarget = false;
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

#if DEBUG
        private void TryLogBadgePerfSampleBatch()
        {
            if (this._badgeRebuildSampleCount < BadgePerfSampleBatchSize || this._badgeRebuildDurationsUs.Count <= 0) {
                return;
            }

            long total = 0;
            for (var i = 0; i < this._badgeRebuildDurationsUs.Count; i++) {
                total += this._badgeRebuildDurationsUs[i];
            }

            this._badgeRebuildDurationsUs.Sort();
            var count = this._badgeRebuildDurationsUs.Count;
            var max = this._badgeRebuildDurationsUs[count - 1];
            var p95Index = (int)Math.Ceiling(count * 0.95d) - 1;
            if (p95Index < 0) {
                p95Index = 0;
            }
            if (p95Index >= count) {
                p95Index = count - 1;
            }

            var avg = (double)total / count;
            var p95 = this._badgeRebuildDurationsUs[p95Index];
            Logger.Debug($"[BadgePerf] samples={count} avg={avg:F2}us p95={p95}us max={max}us");

            this._badgeRebuildDurationsUs.Clear();
            this._badgeRebuildSampleCount = 0;
        }
#endif

        public override void Rebuild(CanvasUpdate update)
        {
            switch (update) {
                case CanvasUpdate.LatePreRender:
                    _ = MainThreadInvoker.Invoke(() =>
                    {
                        var textSnapshot = this.text ?? string.Empty;
                        if (textSnapshot.Length > 80) {
                            textSnapshot = textSnapshot.Substring(0, 80) + "...";
                        }
                        textSnapshot = textSnapshot.Replace("\r", "\\r").Replace("\n", "\\n");
                        var linkCount = this.textInfo?.linkCount ?? -1;
                        #if BADGE_DEBUG
                        Logger.Info("[TASK14DBG][ESC-REBUILD] " + $"Rebuild LatePreRender start textPrefix={textSnapshot} linkCount={linkCount}");
                        #endif

                        this.ClearImages();
                        using (BadgeRebuildProfilerMarker.Auto()) {
#if DEBUG
                            this._badgeRebuildStopwatch.Restart();
#endif
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
#if DEBUG
                            this._badgeRebuildStopwatch.Stop();
                            var elapsedMicroseconds = (this._badgeRebuildStopwatch.ElapsedTicks * 1000000L) / Stopwatch.Frequency;
                            this._badgeRebuildDurationsUs.Add(elapsedMicroseconds);
                            this._badgeRebuildSampleCount++;
                            this.TryLogBadgePerfSampleBatch();
#endif
                        }
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
            var frameDt = Time.unscaledDeltaTime;
            if (frameDt > 0.01667f) {
                Logger.Debug($"[BadgeFrameSpike] dtMs={(frameDt * 1000f):F2}");
            }

            this._badgeGcSampleTimer += frameDt;
            if (this._badgeGcSampleTimer < 1f) {
                return;
            }

            this._badgeGcSampleTimer = 0f;
            var totalMemory = GC.GetTotalMemory(false);
            var gen0CollectionCount = GC.CollectionCount(0);
            if (!this._hasGcBaseline) {
                this._lastGcTotalMemory = totalMemory;
                this._lastGen0CollectionCount = gen0CollectionCount;
                this._hasGcBaseline = true;
                return;
            }

            var memoryDelta = totalMemory - this._lastGcTotalMemory;
            var gen0Delta = gen0CollectionCount - this._lastGen0CollectionCount;
            if (gen0Delta > 0 || Math.Abs(memoryDelta) >= 262144L) {
                Logger.Debug($"[BadgeGC] totalMemKB={totalMemory / 1024L} deltaKB={memoryDelta / 1024L} gen0Delta={gen0Delta}");
            }

            this._lastGcTotalMemory = totalMemory;
            this._lastGen0CollectionCount = gen0CollectionCount;
#endif
        }

        public class Factory : PlaceholderFactory<EnhancedTextMeshProUGUI>
        {
        }
    }
}
