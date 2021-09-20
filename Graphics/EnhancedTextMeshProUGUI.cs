﻿using BeatSaberMarkupLanguage.Animations;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections.Concurrent;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUI : TextMeshProUGUI
    {
        public IChatMessage ChatMessage { get; set; } = null;
        public EnhancedFontInfo FontInfo { get; private set; }
        public event Action OnLatePreRenderRebuildComplete;

        private static readonly ObjectMemoryComponentPool<EnhancedImage> _imagePool = new ObjectMemoryComponentPool<EnhancedImage>(64,
            constructor: () =>
            {
                var img = new GameObject().AddComponent<EnhancedImage>();
                DontDestroyOnLoad(img.gameObject);
                img.gameObject.SetActive(false);
                img.raycastTarget = false;
                img.color = Color.white;
                img.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.pivot = new Vector2(0, 0);
                img.animStateUpdater = img.gameObject.AddComponent<AnimationStateUpdater>();
                img.animStateUpdater.image = img;
                img.SetAllDirty();
                return img;
            },
            onFree: img =>
            {
                try {
                    img.gameObject.SetActive(false);
                    img.animStateUpdater.controllerData = null;
                    img.rectTransform.SetParent(null);
                    img.sprite = null;
                }
                catch (Exception ex) {
                    Logger.Error($"Exception while freeing EnhancedImage in EnhancedTextMeshProUGUI. {ex.ToString()}");
                }
            }
        );

        protected override void Awake()
        {
            base.Awake();
            this.FontInfo = ESCFontManager.instance.FontInfo;
            this.raycastTarget = false;
        }

        public void ClearImages()
        {
            while (this._currentImages.TryTake(out var image)) {
                _imagePool.Free(image);
            }
        }

        private readonly ConcurrentBag<EnhancedImage> _currentImages = new ConcurrentBag<EnhancedImage>();
        public override void Rebuild(CanvasUpdate update)
        {
            if (update == CanvasUpdate.LatePreRender) {
                MainThreadInvoker.Invoke(() =>
                {
                    this.ClearImages();
                });
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
                        // Skip characters that have no imageInfo registered
                        continue;
                    }

                    MainThreadInvoker.Invoke(() =>
                    {
                        var img = _imagePool.Alloc();
                        try {
                            if (imageInfo.AnimControllerData != null) {
                                img.animStateUpdater.controllerData = imageInfo.AnimControllerData;
                                img.sprite = imageInfo.AnimControllerData.sprites[imageInfo.AnimControllerData.uvIndex];
                            }
                            else {
                                img.sprite = imageInfo.Sprite;
                            }
                            img.material = BeatSaberUtils.UINoGlowMaterial;
                            img.rectTransform.localScale = new Vector3(this.fontScale * 1.08f, this.fontScale * 1.08f, this.fontScale * 1.08f);
                            img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                            img.rectTransform.SetParent(this.rectTransform, false);
                            img.rectTransform.localPosition = c.topLeft - new Vector3(0, imageInfo.Height * this.fontScale * 0.558f / 2);
                            img.rectTransform.localRotation = Quaternion.identity;
                            img.gameObject.SetActive(true);
                            img.SetAllDirty();
                            this._currentImages.Add(img);
                        }
                        catch (Exception ex) {
                            Logger.Error($"Exception while trying to overlay sprite. {ex.ToString()}");
                            _imagePool.Free(img);
                        }
                    });
                }
            }
            base.Rebuild(update);
            if (update == CanvasUpdate.LatePreRender) {
                MainThreadInvoker.Invoke(OnLatePreRenderRebuildComplete);
            }
        }
    }
}
