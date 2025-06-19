using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Chat;
using Zenject;

namespace EnhancedStreamChat.Core.Models
{
    /// <summary>
    /// 可渲染的消息对象，包含UI元素引用
    /// </summary>
    public class RenderableMessage : IPoolable
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// 消息GameObject
        /// </summary>
        public GameObject GameObject { get; set; }
        
        /// <summary>
        /// 文本组件
        /// </summary>
        public TextMeshProUGUI TextComponent { get; set; }
        
        /// <summary>
        /// 图片列表（表情、徽章等）
        /// </summary>
        public List<EnhancedImage> Images { get; set; } = new List<EnhancedImage>();
        
        /// <summary>
        /// 消息高度
        /// </summary>
        public float Height { get; set; }
        
        /// <summary>
        /// 是否包含动画元素
        /// </summary>
        public bool IsAnimated { get; set; }
        
        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// 原始聊天消息引用
        /// </summary>
        public ChatMessage SourceMessage { get; set; }
        
        /// <summary>
        /// 当前透明度（用于淡入淡出）
        /// </summary>
        public float Alpha { get; set; } = 1f;
        
        /// <summary>
        /// 是否正在淡出
        /// </summary>
        public bool IsFadingOut { get; set; }
        
        /// <summary>
        /// RectTransform组件引用（缓存）
        /// </summary>
        private RectTransform _rectTransform;
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null && GameObject != null)
                    _rectTransform = GameObject.GetComponent<RectTransform>();
                return _rectTransform;
            }
        }
        
        /// <summary>
        /// CanvasGroup组件引用（缓存）
        /// </summary>
        private CanvasGroup _canvasGroup;
        public CanvasGroup CanvasGroup
        {
            get
            {
                if (_canvasGroup == null && GameObject != null)
                    _canvasGroup = GameObject.GetComponent<CanvasGroup>();
                return _canvasGroup;
            }
        }
        
        #region IPoolable Implementation
        
        /// <summary>
        /// 对象从池中生成时调用
        /// </summary>
        public void OnSpawn()
        {
            if (GameObject != null)
            {
                GameObject.SetActive(true);
                
                if (CanvasGroup != null)
                    CanvasGroup.alpha = 1f;
            }
            
            Images.Clear();
            IsAnimated = false;
            IsFadingOut = false;
            Alpha = 1f;
            CreatedAt = DateTime.UtcNow;
        }
        
        /// <summary>
        /// 对象回收到池时调用
        /// </summary>
        public void OnDespawn()
        {
            if (GameObject != null)
            {
                GameObject.SetActive(false);
            }
            
            if (TextComponent != null)
            {
                TextComponent.text = string.Empty;
                // 避免在对象回收时调用 SetAllDirty()，可能导致重建循环
                // 清空文本已经足够，Unity 会自动处理必要的更新
                // TextComponent.SetAllDirty();
            }
            
            // 释放图片资源
            foreach (var image in Images)
            {
                if (image != null)
                {
                    // EnhancedImage 没有 OnDespawn 方法，直接设置为不活动
                    image.gameObject.SetActive(false);
                    
                    // 清理动画状态
                    if (image.animStateUpdater != null)
                    {
                        image.animStateUpdater.enabled = false;
                    }
                }
            }
            Images.Clear();
            
            // 清理引用
            Id = null;
            SourceMessage = null;
            IsAnimated = false;
            IsFadingOut = false;
            Height = 0;
        }
        
        #endregion
        
        /// <summary>
        /// 更新淡出动画
        /// </summary>
        /// <param name="deltaTime">时间间隔</param>
        /// <param name="fadeSpeed">淡出速度</param>
        /// <returns>是否已完全淡出</returns>
        public bool UpdateFade(float deltaTime, float fadeSpeed = 2f)
        {
            if (!IsFadingOut) return false;
            
            Alpha -= deltaTime * fadeSpeed;
            if (Alpha <= 0)
            {
                Alpha = 0;
                return true;
            }
            
            if (CanvasGroup != null)
            {
                CanvasGroup.alpha = Alpha;
            }
            
            return false;
        }
        
        /// <summary>
        /// 开始淡出
        /// </summary>
        public void StartFadeOut()
        {
            IsFadingOut = true;
        }
        
        /// <summary>
        /// 更新消息高度
        /// </summary>
        public void UpdateHeight()
        {
            if (TextComponent != null)
            {
                // 强制更新布局
                Canvas.ForceUpdateCanvases();
                Height = TextComponent.preferredHeight;
                
                if (RectTransform != null)
                {
                    RectTransform.sizeDelta = new Vector2(RectTransform.sizeDelta.x, Height);
                }
            }
        }
        
        /// <summary>
        /// 重置对象状态（用于内存池）
        /// </summary>
        public void Reset()
        {
            OnDespawn();
        }
        
        /// <summary>
        /// 内存池定义
        /// </summary>
        public class Pool : Zenject.MemoryPool<RenderableMessage>
        {
            [Inject]
            private readonly IFontProvider _fontProvider;
            
            protected override void Reinitialize(RenderableMessage item)
            {
                base.Reinitialize(item);
                
                if (item.GameObject == null)
                {
                    // 创建 GameObject
                    var go = new GameObject("ChatMessage");
                    item.GameObject = go;
                    
                    // 添加 RectTransform
                    var rectTransform = go.AddComponent<RectTransform>();
                    rectTransform.sizeDelta = new Vector2(ChatConfig.instance.ChatWidth, 0);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMin = new Vector2(0, 0);
                    rectTransform.anchorMax = new Vector2(1, 0);
                    
                    // 添加 CanvasGroup（用于淡入淡出）
                    go.AddComponent<CanvasGroup>();
                    
                    // 添加 LayoutElement
                    var layoutElement = go.AddComponent<LayoutElement>();
                    layoutElement.preferredWidth = ChatConfig.instance.ChatWidth;
                    
                    // 添加文本组件
                    var text = go.AddComponent<EnhancedTextMeshProUGUI>();
                    text.fontSize = ChatConfig.instance.FontSize;
                    text.color = ChatConfig.instance.TextColor;
                    text.enableWordWrapping = true;
                    text.richText = true;
                    text.fontStyle = FontStyles.Normal;
                    text.alignment = TextAlignmentOptions.TopLeft;
                    text.overflowMode = TextOverflowModes.Overflow;
                    item.TextComponent = text;
                    
                    // 设置字体
                    var font = _fontProvider.DefaultFont;
                    if (font != null)
                    {
                        text.font = font;
                    }
                }
            }
            
            protected override void OnSpawned(RenderableMessage item)
            {
                base.OnSpawned(item);
                item.OnSpawn();
            }
            
            protected override void OnDespawned(RenderableMessage item)
            {
                if (item != null)
                {
                    item.OnDespawn();
                }
                base.OnDespawned(item);
            }
        }
    }
    
    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 当对象从池中取出时调用
        /// </summary>
        void OnSpawn();
        
        /// <summary>
        /// 当对象返回池中时调用
        /// </summary>
        void OnDespawn();
    }
}