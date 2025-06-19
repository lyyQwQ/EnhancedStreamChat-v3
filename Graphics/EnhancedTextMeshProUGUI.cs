using BeatSaberMarkupLanguage.Animations;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Utilities;
using EnhancedStream_139.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
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
        
        private readonly HashSet<ILatePreRenderRebuildReceiver> _receivers = new HashSet<ILatePreRenderRebuildReceiver>();
        private bool _rebuilt = false;

        private static readonly ObjectMemoryComponentPool<EnhancedImage> _imagePool =
            new ObjectMemoryComponentPool<EnhancedImage>(64,
                constructor: () =>
                {
                    var img = new GameObject("EnhancedImage").AddComponent<EnhancedImage>();
                    // 创建时设置为不激活，避免触发重建
                    img.gameObject.SetActive(false);
                    img.raycastTarget = false;
                    img.color = Color.white;
                    // 恢复原来的 anchor 和 pivot 设置
                    img.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    img.rectTransform.pivot = new Vector2(0, 0);
                    img.animStateUpdater = img.gameObject.AddComponent<AnimationStateUpdater>();
                    img.animStateUpdater.Image = img;
                    return img;
                },
                onFree: img =>
                {
                    try
                    {
                        if (img != null && img.gameObject != null)
                        {
                            // 直接同步执行，避免时序问题
                            img.gameObject.SetActive(false);
                            img.rectTransform.SetParent(null);
                            img.animStateUpdater.ControllerData = null;
                            img.sprite = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Exception while freeing EnhancedImage. {ex}");
                    }
                }
            );

        protected override void Awake()
        {
            base.Awake();
            // 延迟获取 FontInfo，避免在 prefab 创建时访问未初始化的 instance
            // this.FontInfo = ESCFontManager.instance.FontInfo;
            // Logger.Debug($"FontInfo: {this.FontInfo}");
            this.raycastTarget = false;
        }
        
        // 延迟初始化 FontInfo
        private void EnsureFontInfo()
        {
            if (this.FontInfo == null && ESCFontManager.instance != null)
            {
                this.FontInfo = ESCFontManager.instance.FontInfo;
            }
        }

        public void ClearImages()
        {
            // 直接清理，不需要延迟
            while (this._currentImages.TryTake(out var image))
            {
                if (image != null)
                {
                    _imagePool.Free(image);
                }
            }
        }

        private readonly ConcurrentBag<EnhancedImage> _currentImages = new ConcurrentBag<EnhancedImage>();

        // public override void Rebuild(CanvasUpdate update)
        // {
        //     Logger.Debug($"Into EnhancedTextMeshProUGUI.Rebuild, update: {update}");
        //     if (update == CanvasUpdate.LatePreRender) {
        //         MainThreadInvoker.Invoke(() =>
        //         {
        //             Logger.Debug("Clearing images...");
        //             this.ClearImages();
        //             Logger.Debug("Images cleared.");
        //         });
        //         Logger.Debug("Rebuilding images...");
        //         for (var i = 0; i < this.textInfo.characterCount; i++) {
        //             var c = this.textInfo.characterInfo[i];
        //             if (!c.isVisible || string.IsNullOrEmpty(this.text) || c.index >= this.text.Length) {
        //                 // Skip invisible/empty/out of range chars
        //                 Logger.Debug($"Skipping character: {this.text[c.index]}, isVisible: {c.isVisible}, text: {this.text}, index: {c.index}, textLength: {this.text.Length}");
        //                 continue;
        //             }
        //
        //             Logger.Debug($"Processing character: {this.text[c.index]}");
        //             uint character = this.text[c.index];
        //             if (c.index + 1 < this.text.Length && char.IsSurrogatePair(this.text[c.index], this.text[c.index + 1])) {
        //                 // If it's a surrogate pair, convert the character
        //                 Logger.Debug($"Character is a surrogate pair.");
        //                 character = (uint)char.ConvertToUtf32(this.text[c.index], this.text[c.index + 1]);
        //                 Logger.Debug($"Converted character: {character}");
        //             }
        //
        //             if (this.FontInfo == null || !this.FontInfo.TryGetImageInfo(character, out var imageInfo) || imageInfo is null) {
        //                 // Skip characters that have no imageInfo registered
        //                 Logger.Debug($"No imageInfo found for character: {this.text[c.index]}");
        //                 continue;
        //             }
        //
        //             Logger.Debug($"Found imageInfo for character: {this.text[c.index]}");
        //             MainThreadInvoker.Invoke(() =>
        //             {
        //                 var img = _imagePool.Alloc();
        //                 try {
        //                     Logger.Debug($"Overlaying sprite for character: {this.text[c.index]}");
        //                     if (imageInfo.AnimControllerData != null) {
        //                         Logger.Debug($"Overlaying animated sprite for character: {this.text[c.index]}");
        //                         img.animStateUpdater.controllerData = imageInfo.AnimControllerData;
        //                         img.sprite = imageInfo.AnimControllerData.sprites[imageInfo.AnimControllerData.uvIndex];
        //                     }
        //                     else {
        //                         Logger.Debug($"Overlaying static sprite for character: {this.text[c.index]}");
        //                         img.sprite = imageInfo.Sprite;
        //                     }
        //                     Logger.Debug($"Sprite overlayed for character: {this.text[c.index]}");
        //                     img.material = BeatSaberUtils.UINoGlowMaterial;
        //                     // img.rectTransform.localScale = new Vector3(this.fontScale * 1.08f, this.fontScale * 1.08f, this.fontScale * 1.08f);
        //                     img.rectTransform.localScale = new Vector3(this.m_fontScaleMultiplier * 1.08f, this.m_fontScaleMultiplier * 1.08f, this.m_fontScaleMultiplier * 1.08f);
        //                     img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
        //                     img.rectTransform.SetParent(this.rectTransform, false);
        //                     img.rectTransform.localPosition = c.topLeft - new Vector3(0, imageInfo.Height * this.m_fontScaleMultiplier * 0.558f / 2);
        //                     img.rectTransform.localRotation = Quaternion.identity;
        //                     img.gameObject.SetActive(true);
        //                     img.SetAllDirty();
        //                     this._currentImages.Add(img);
        //                     Logger.Debug($"Sprite overlayed for character: {this.text[c.index]}");
        //                 }
        //                 catch (Exception ex) {
        //                     Logger.Error($"Exception while trying to overlay sprite. {ex.ToString()}");
        //                     _imagePool.Free(img);
        //                 }
        //             });
        //             Logger.Debug($"Image overlayed for character: {this.text[c.index]}");
        //         }
        //     }
        //     Logger.Debug("Exiting EnhancedTextMeshProUGUI.Rebuild.");
        //     base.Rebuild(update);
        //     Logger.Debug("Base rebuild complete.");
        //     if (update == CanvasUpdate.LatePreRender) {
        //         MainThreadInvoker.Invoke(OnLatePreRenderRebuildComplete);
        //     }
        //     Logger.Debug("Exiting EnhancedTextMeshProUGUI.Rebuild. 2");
        // }
        //
        // public override void Rebuild(CanvasUpdate update)
        // {
        //     // try
        //     // {
        //     // Logger.Debug($"Into EnhancedTextMeshProUGUI.Rebuild, update: {update}");

        //     // 如果是处理图片的阶段
        //     if (update == CanvasUpdate.LatePreRender)
        //     {
        //         MainThreadInvoker.Invoke(() =>
        //         {
        //             // Logger.Debug("Clearing images...");
        //             this.ClearImages();
        //             // Logger.Debug("Images cleared.");


        //             // Logger.Debug("Rebuilding images...");
        //             // try
        //             // {
        //             //     var fieldInfo = typeof(TextMeshProUGUI).GetField("m_TextProcessingArray",
        //             //         BindingFlags.NonPublic | BindingFlags.Instance);
        //             //     var mTextProcessingArray = fieldInfo.GetValue(this);
        //             //     // Logger.Debug($"m_TextProcessingArray is null: {mTextProcessingArray == null}");
        //             //     if (mTextProcessingArray != null)
        //             //     {
        //             //         // Logger.Debug($"m_TextProcessingArray length: {((Array)mTextProcessingArray).Length}");
        //             //         // for (var i = 0; i < ((Array)mTextProcessingArray).Length; i++)
        //             //         // {
        //             //         //     Logger.Debug($"m_TextProcessingArray[{i}]: {((Array)mTextProcessingArray).GetValue(i)}");
        //             //         // }
        //             //     }
        //             //     // Logger.Debug($"m_havePropertiesChanged: {this.havePropertiesChanged}");
        //             //     // Logger.Debug($"m_text: {this.m_text}");
        //             //     // this.havePropertiesChanged = true;
        //             //
        //             //     // this.ForceMeshUpdate();
        //             // }
        //             // catch (Exception ex)
        //             // {
        //             //     Logger.Error($"Exception while trying to force mesh update. {ex}");
        //             // }

        //             // Logger.Debug(
        //             //     $"textInfo is null: {this.textInfo == null}, characterCount: {this.textInfo.characterCount}, text: {this.text}");
        //             for (var i = 0; i < this.textInfo.characterCount; i++)
        //             {
        //                 var c = this.textInfo.characterInfo[i];

        //                 // Logger.Debug(
        //                 //     $"Processing character at index {i}: CharCode={c.character}, Visible={c.isVisible}, Text={this.text}");

        //                 if (!c.isVisible || string.IsNullOrEmpty(this.text) || c.index >= this.text.Length)
        //                 {
        //                     // 跳过不可见字符、空字符或索引越界的字符
        //                     // Logger.Debug(
        //                     //     $"Skipping character at index {i}: CharCode={c.character}, Visible={c.isVisible}, Text={this.text}");
        //                     continue;
        //                 }

        //                 uint character = this.text[c.index];
        //                 if (c.index + 1 < this.text.Length &&
        //                     char.IsSurrogatePair(this.text[c.index], this.text[c.index + 1]))
        //                 {
        //                     // 处理代理对字符
        //                     // Logger.Debug($"Character at index {i} is a surrogate pair.");
        //                     character = (uint)char.ConvertToUtf32(this.text[c.index], this.text[c.index + 1]);
        //                     // Logger.Debug($"Converted surrogate pair to: {character}");
        //                 }

        //                 // Logger.Debug($"Processing character: {character}");

        //                 if (this.FontInfo == null || !this.FontInfo.TryGetImageInfo(character, out var imageInfo) ||
        //                     imageInfo == null)
        //                 {
        //                     // Logger.Warn($"No imageInfo found for character: {character:X}");
        //                     continue;
        //                 }

        //                 // Logger.Debug($"Found imageInfo for character: {character}");

        //                 MainThreadInvoker.Invoke(() =>
        //                 {
        //                     var img = _imagePool.Alloc();
        //                     try
        //                     {
        //                         // Logger.Debug($"Overlaying sprite for character: {character}");
        //                         if (imageInfo.AnimControllerData != null)
        //                         {
        //                             img.animStateUpdater.ControllerData = imageInfo.AnimControllerData;
        //                             img.sprite =
        //                                 imageInfo.AnimControllerData.Sprites[imageInfo.AnimControllerData.UvIndex];
        //                         }
        //                         else
        //                         {
        //                             img.sprite = imageInfo.Sprite;
        //                         }

        //                         img.material = BeatSaberUtils.UINoGlowMaterial;
        //                         img.rectTransform.localScale = new Vector3(this.m_fontScaleMultiplier * 1.08f,
        //                             this.m_fontScaleMultiplier * 1.08f, this.m_fontScaleMultiplier * 1.08f);
        //                         img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
        //                         img.rectTransform.SetParent(this.rectTransform, false);
        //                         img.rectTransform.localPosition = c.topLeft - new Vector3(0,
        //                             imageInfo.Height * this.m_fontScaleMultiplier * 0.558f / 2);
        //                         img.rectTransform.localRotation = Quaternion.identity;
        //                         img.gameObject.SetActive(true);
        //                         img.SetAllDirty();
        //                         this._currentImages.Add(img);
        //                         // Logger.Debug($"Sprite overlayed for character: {character}");
        //                     }
        //                     catch (Exception ex)
        //                     {
        //                         // Logger.Error($"Exception while trying to overlay sprite. {ex}");
        //                         _imagePool.Free(img);
        //                     }
        //                 });
        //             }
        //         });
        //     }

        //     // 在此处添加日志，检查传递给 SetArraySizes 的 unicodeChars 数组
        //     // Logger.Debug("Before calling SetArraySizes.");
        //     // Logger.Debug(
        //     //     $"textInfo is null: {this.textInfo == null}, characterCount: {this.textInfo.characterCount}, text: {this.text}");
        //     // if (this.textInfo != null && this.textInfo.characterCount > 0)
        //     // {
        //     //     for (var i = 0; i < this.textInfo.characterCount; i++)
        //     //     {
        //     //         Logger.Debug($"UnicodeChar[{i}]: {this.textInfo.characterInfo[i].character}");
        //     //     }
        //     // }

        //     base.Rebuild(update);
        //     // Logger.Debug("Base rebuild complete.");

        //     if (update == CanvasUpdate.LatePreRender)
        //     {
        //         // Logger.Debug("Calling OnLatePreRenderRebuildComplete.");
        //         MainThreadInvoker.Invoke(OnLatePreRenderRebuildComplete);
        //         // Logger.Debug("OnLatePreRenderRebuildComplete called.");
        //     }

        //     // Logger.Debug("Exiting EnhancedTextMeshProUGUI.Rebuild. 2");
        //     // }
        //     // catch (Exception ex)
        //     // {
        //     //     Logger.Error($"Exception in EnhancedTextMeshProUGUI.Rebuild: {ex}");
        //     // }
        // }
        private bool _isRebuilding = false;
        private float _lastRebuildTime = 0f;
        private const float REBUILD_COOLDOWN = 0.1f; // 100ms 冷却时间
        private bool _needsRebuild = false; // 标记是否需要重建
        
        public override void Rebuild(CanvasUpdate update)
        {
            if (update == CanvasUpdate.LatePreRender)
            {
                // 如果正在重建中，标记需要重建并返回
                if (_isRebuilding)
                {
                    _needsRebuild = true;
                    base.Rebuild(update);
                    return;
                }
                
                // 添加时间检查，避免频繁重建
                var currentTime = Time.time;
                if (currentTime - _lastRebuildTime < REBUILD_COOLDOWN)
                {
                    _needsRebuild = true;
                    base.Rebuild(update);
                    return;
                }
                
                _isRebuilding = true;
                _lastRebuildTime = currentTime;
                _needsRebuild = false;
                
                // 直接调用 RebuildImages，它内部会使用 MainThreadInvoker 延迟执行
                RebuildImages();
            }

            base.Rebuild(update);
        }
        
        private void RebuildImages()
        {
            // 使用 MainThreadInvoker 延迟到下一帧执行所有操作
            MainThreadInvoker.Invoke(() =>
            {
                try
                {
                    // 采用 v3 的简单方法：总是完全重建
                    // 这样可以避免 ConcurrentBag 无序导致的位置错乱问题
                    this.ClearImages();
                    
                    // 确保 FontInfo 已初始化
                    EnsureFontInfo();
                    
                    for (var i = 0; i < this.textInfo.characterCount; i++)
                    {
                        var c = this.textInfo.characterInfo[i];
                        if (!c.isVisible || string.IsNullOrEmpty(this.text) || c.index >= this.text.Length)
                        {
                            continue;
                        }

                        uint character = this.text[c.index];
                        if (c.index + 1 < this.text.Length && char.IsSurrogatePair(this.text[c.index], this.text[c.index + 1]))
                        {
                            character = (uint)char.ConvertToUtf32(this.text[c.index], this.text[c.index + 1]);
                        }
                        
                        if (this.FontInfo == null || !this.FontInfo.TryGetImageInfo(character, out var imageInfo) || imageInfo is null)
                        {
                            continue;
                        }

                        var img = _imagePool.Alloc();
                        try
                        {
                            img.gameObject.SetActive(true);
                            img.rectTransform.SetParent(this.rectTransform, false);
                            
                            if (imageInfo.AnimControllerData != null)
                            {
                                img.animStateUpdater.ControllerData = imageInfo.AnimControllerData;
                                img.sprite = imageInfo.AnimControllerData.Sprites[imageInfo.AnimControllerData.UvIndex];
                            }
                            else
                            {
                                img.sprite = imageInfo.Sprite;
                            }

                            var fontScale = 0.010f * this.fontSize;
                            img.rectTransform.localScale = new Vector3(fontScale * 1.08f, fontScale * 1.08f, fontScale * 1.08f);
                            img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                            // 恢复原来的位置计算
                            img.rectTransform.localPosition = c.topLeft - new Vector3(0, imageInfo.Height * fontScale * 0.558f / 2);
                            img.rectTransform.localRotation = Quaternion.identity;
                            img.material = BeatSaberUtils.UINoGlowMaterial;
                            
                            this._currentImages.Add(img);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Exception while trying to overlay sprite. {ex}");
                            _imagePool.Free(img);
                        }
                    }
                    
                    OnLatePreRenderRebuildComplete?.Invoke();
                    _rebuilt = true;
                }
                finally
                {
                    // 在实际重建完成后才重置标志
                    _isRebuilding = false;
                    
                    // 如果在重建期间有新的重建请求，立即处理
                    if (_needsRebuild)
                    {
                        _needsRebuild = false;
                        // 标记需要重建，下一帧会自动调用
                        this.SetVerticesDirty();
                    }
                }
            });
        }
        
        public void AddReceiver(ILatePreRenderRebuildReceiver receiver)
        {
            if (receiver != null)
            {
                _receivers.Add(receiver);
            }
        }
        
        public void RemoveReceiver(ILatePreRenderRebuildReceiver receiver)
        {
            if (receiver != null)
            {
                _receivers.Remove(receiver);
            }
        }
        
        protected void LateUpdate()
        {
            if (_rebuilt)
            {
                foreach (var receiver in _receivers)
                {
                    receiver?.LatePreRenderRebuildHandler(this, EventArgs.Empty);
                }
                _rebuilt = false;
            }
        }
        
    }
}