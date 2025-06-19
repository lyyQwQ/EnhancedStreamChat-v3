using BeatSaberMarkupLanguage.Animations;
using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using EnhancedStreamChat;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedImage : Image
    {
        public AnimationStateUpdater animStateUpdater { get; set; } = null;
        
        public class Pool : MonoMemoryPool<EnhancedImage>
        {
            protected override void OnCreated(EnhancedImage img)
            {
                base.OnCreated(img);
                img.raycastTarget = false;
                img.color = Color.white;
                img.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                img.rectTransform.pivot = new Vector2(0, 0);
                img.animStateUpdater = img.gameObject.GetComponent<AnimationStateUpdater>();
                if (img.animStateUpdater != null)
                {
                    img.animStateUpdater.Image = img;
                }
                // 移除 SetAllDirty() 调用，避免在对象创建时触发重建循环
                // img.SetAllDirty();
            }

            protected override void OnDespawned(EnhancedImage img)
            {
                if (img == null || img.gameObject == null) 
                {
                    return;
                }
                try 
                {
                    if (img.animStateUpdater != null)
                    {
                        img.animStateUpdater.ControllerData = null;
                    }
                    img.sprite = null;
                    // 移除 SetAllDirty() 调用，避免在对象回收时触发重建循环
                    // img.SetAllDirty();
                    base.OnDespawned(img);
                }
                catch (Exception ex) 
                {
                    Logger.Error($"Exception while freeing EnhancedImage. {ex}");
                }
            }
        }
    }
}
