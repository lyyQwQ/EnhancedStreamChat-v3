using BeatSaberMarkupLanguage.Animations;
using ChatCore.Interfaces;
using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections.Concurrent;
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

        private static readonly ObjectMemoryComponentPool<EnhancedImage> _imagePool =
            new ObjectMemoryComponentPool<EnhancedImage>(64,
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
                    try
                    {
                        img.gameObject.SetActive(false);
                        img.animStateUpdater.controllerData = null;
                        img.rectTransform.SetParent(null);
                        img.sprite = null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(
                            $"Exception while freeing EnhancedImage in EnhancedTextMeshProUGUI. {ex.ToString()}");
                    }
                }
            );

        protected override void Awake()
        {
            base.Awake();
            this.FontInfo = ESCFontManager.instance.FontInfo;
            Logger.Debug($"FontInfo: {this.FontInfo}");
            this.raycastTarget = false;
        }

        public void ClearImages()
        {
            while (this._currentImages.TryTake(out var image))
            {
                _imagePool.Free(image);
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
        public override void Rebuild(CanvasUpdate update)
        {
            try
            {
                Logger.Debug($"Into EnhancedTextMeshProUGUI.Rebuild, update: {update}");

                // 如果是处理图片的阶段
                if (update == CanvasUpdate.LatePreRender)
                {
                    MainThreadInvoker.Invoke(() =>
                    {
                        Logger.Debug("Clearing images...");
                        this.ClearImages();
                        Logger.Debug("Images cleared.");


                        Logger.Debug("Rebuilding images...");
                        try
                        {
                            var fieldInfo = typeof(TextMeshProUGUI).GetField("m_TextProcessingArray",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            var mTextProcessingArray = fieldInfo.GetValue(this);
                            Logger.Debug($"m_TextProcessingArray is null: {mTextProcessingArray == null}");
                            if (mTextProcessingArray != null)
                            {
                                Logger.Debug($"m_TextProcessingArray length: {((Array)mTextProcessingArray).Length}");
                                // for (var i = 0; i < ((Array)mTextProcessingArray).Length; i++)
                                // {
                                //     Logger.Debug($"m_TextProcessingArray[{i}]: {((Array)mTextProcessingArray).GetValue(i)}");
                                // }
                            }
                            Logger.Debug($"m_havePropertiesChanged: {this.havePropertiesChanged}");
                            Logger.Debug($"m_text: {this.m_text}");
                            // this.havePropertiesChanged = true;

                            // this.ForceMeshUpdate();
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Exception while trying to force mesh update. {ex}");
                        }

                        Logger.Debug(
                            $"textInfo is null: {this.textInfo == null}, characterCount: {this.textInfo.characterCount}, text: {this.text}");
                        for (var i = 0; i < this.textInfo.characterCount; i++)
                        {
                            var c = this.textInfo.characterInfo[i];

                            Logger.Debug(
                                $"Processing character at index {i}: CharCode={c.character}, Visible={c.isVisible}, Text={this.text}");

                            if (!c.isVisible || string.IsNullOrEmpty(this.text) || c.index >= this.text.Length)
                            {
                                // 跳过不可见字符、空字符或索引越界的字符
                                Logger.Debug(
                                    $"Skipping character at index {i}: CharCode={c.character}, Visible={c.isVisible}, Text={this.text}");
                                continue;
                            }

                            uint character = this.text[c.index];
                            if (c.index + 1 < this.text.Length &&
                                char.IsSurrogatePair(this.text[c.index], this.text[c.index + 1]))
                            {
                                // 处理代理对字符
                                Logger.Debug($"Character at index {i} is a surrogate pair.");
                                character = (uint)char.ConvertToUtf32(this.text[c.index], this.text[c.index + 1]);
                                Logger.Debug($"Converted surrogate pair to: {character}");
                            }

                            Logger.Debug($"Processing character: {character}");

                            if (this.FontInfo == null || !this.FontInfo.TryGetImageInfo(character, out var imageInfo) ||
                                imageInfo == null)
                            {
                                Logger.Warn($"No imageInfo found for character: {character}");
                                continue;
                            }

                            Logger.Debug($"Found imageInfo for character: {character}");

                            MainThreadInvoker.Invoke(() =>
                            {
                                var img = _imagePool.Alloc();
                                try
                                {
                                    Logger.Debug($"Overlaying sprite for character: {character}");
                                    if (imageInfo.AnimControllerData != null)
                                    {
                                        img.animStateUpdater.controllerData = imageInfo.AnimControllerData;
                                        img.sprite =
                                            imageInfo.AnimControllerData.sprites[imageInfo.AnimControllerData.uvIndex];
                                    }
                                    else
                                    {
                                        img.sprite = imageInfo.Sprite;
                                    }

                                    img.material = BeatSaberUtils.UINoGlowMaterial;
                                    img.rectTransform.localScale = new Vector3(this.m_fontScaleMultiplier * 1.08f,
                                        this.m_fontScaleMultiplier * 1.08f, this.m_fontScaleMultiplier * 1.08f);
                                    img.rectTransform.sizeDelta = new Vector2(imageInfo.Width, imageInfo.Height);
                                    img.rectTransform.SetParent(this.rectTransform, false);
                                    img.rectTransform.localPosition = c.topLeft - new Vector3(0,
                                        imageInfo.Height * this.m_fontScaleMultiplier * 0.558f / 2);
                                    img.rectTransform.localRotation = Quaternion.identity;
                                    img.gameObject.SetActive(true);
                                    img.SetAllDirty();
                                    this._currentImages.Add(img);
                                    Logger.Debug($"Sprite overlayed for character: {character}");
                                }
                                catch (Exception ex)
                                {
                                    Logger.Error($"Exception while trying to overlay sprite. {ex}");
                                    _imagePool.Free(img);
                                }
                            });
                        }
                    });
                }

                // 在此处添加日志，检查传递给 SetArraySizes 的 unicodeChars 数组
                Logger.Debug("Before calling SetArraySizes.");
                Logger.Debug(
                    $"textInfo is null: {this.textInfo == null}, characterCount: {this.textInfo.characterCount}, text: {this.text}");
                if (this.textInfo != null && this.textInfo.characterCount > 0)
                {
                    for (var i = 0; i < this.textInfo.characterCount; i++)
                    {
                        Logger.Debug($"UnicodeChar[{i}]: {this.textInfo.characterInfo[i].character}");
                    }
                }

                base.Rebuild(update);
                Logger.Debug("Base rebuild complete.");

                if (update == CanvasUpdate.LatePreRender)
                {
                    Logger.Debug("Calling OnLatePreRenderRebuildComplete.");
                    MainThreadInvoker.Invoke(OnLatePreRenderRebuildComplete);
                    Logger.Debug("OnLatePreRenderRebuildComplete called.");
                }

                Logger.Debug("Exiting EnhancedTextMeshProUGUI.Rebuild. 2");
            }
            catch (Exception ex)
            {
                Logger.Error($"Exception in EnhancedTextMeshProUGUI.Rebuild: {ex}");
            }
        }


    }


}