using EnhancedStreamChat.Utilities;
using HMUI;
using System;
using EnhancedStreamChat.Interfaces;
using EnhancedStream_139.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUIWithBackground : MonoBehaviour, ILatePreRenderRebuildReceiver
    {
        public EnhancedTextMeshProUGUI Text { get; internal set; }

        public EnhancedTextMeshProUGUI SubText { get; internal set; }

        public DateTime ReceivedDate { get; internal set; }

        public event Action OnLatePreRenderRebuildComplete;

        internal ImageView _highlight;
        internal ImageView _accent;
        internal VerticalLayoutGroup _verticalLayoutGroup;
        private bool _rebuiled = false;

        public Vector2 Size
        {
            get => (this.transform as RectTransform).sizeDelta;
            set => (this.transform as RectTransform).sizeDelta = value;
        }

        public Color AccentColor
        {
            get => this._accent.color;
            set => this._accent.color = value;
        }

        public Color HighlightColor
        {
            get => this._highlight.color;
            set => this._highlight.color = value;
        }

        public bool HighlightEnabled
        {
            get => this._highlight.enabled;
            set
            {
                this._highlight.enabled = value;
                if (value)
                {
                    this._verticalLayoutGroup.padding = new RectOffset(5, 5, 2, 2);
                }
                else
                {
                    this._verticalLayoutGroup.padding = new RectOffset(5, 5, 1, 1);
                }
            }
        }

        public bool AccentEnabled
        {
            get => this._accent.enabled;
            set => this._accent.enabled = value;
        }

        public bool SubTextEnabled
        {
            get => this.SubText != null && this.SubText.enabled;
            set
            {
                if (this.SubText == null)
                {
                    return;
                }
                this.SubText.enabled = value;
                if (value)
                {
                    this.SubText.rectTransform.SetParent(this.gameObject.transform, false);
                }
                else
                {
                    this.SubText.rectTransform.SetParent(null, false);
                }
            }
        }

        private void Awake()
        {
            // 获取或添加 ImageView 组件（prefab 可能已经有了）
            this._highlight = this.gameObject.GetComponent<ImageView>();
            if (this._highlight == null)
            {
                this._highlight = this.gameObject.AddComponent<ImageView>();
            }
            this._highlight.raycastTarget = false;
            this._highlight.material = BeatSaberUtils.UINoGlowMaterial;
            
            // 创建文本组件
            if (this.Text == null)
            {
                this.Text = new GameObject().AddComponent<EnhancedTextMeshProUGUI>();
                DontDestroyOnLoad(this.Text.gameObject);
            }
            this.Text.OnLatePreRenderRebuildComplete += this.Text_OnLatePreRenderRebuildComplete;
            this.Text.AddReceiver(this);

            if (this.SubText == null)
            {
                this.SubText = new GameObject().AddComponent<EnhancedTextMeshProUGUI>();
                DontDestroyOnLoad(this.SubText.gameObject);
            }
            this.SubText.OnLatePreRenderRebuildComplete += this.Text_OnLatePreRenderRebuildComplete;
            this.SubText.AddReceiver(this);

            // 创建强调色组件
            this._accent = new GameObject().AddComponent<ImageView>();
            this._accent.raycastTarget = false;
            DontDestroyOnLoad(this._accent.gameObject);
            this._accent.material = BeatSaberUtils.UINoGlowMaterial;
            this._accent.color = Color.yellow;

            // 获取或添加 VerticalLayoutGroup 组件
            this._verticalLayoutGroup = this.gameObject.GetComponent<VerticalLayoutGroup>();
            if (this._verticalLayoutGroup == null)
            {
                this._verticalLayoutGroup = this.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            this._verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            this._verticalLayoutGroup.spacing = 1;

            var highlightFitter = this._accent.gameObject.AddComponent<LayoutElement>();
            highlightFitter.ignoreLayout = true;
            var textFitter = this.Text.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // 获取或添加 ContentSizeFitter 组件
            var backgroundFitter = this.gameObject.GetComponent<ContentSizeFitter>();
            if (backgroundFitter == null)
            {
                backgroundFitter = this.gameObject.AddComponent<ContentSizeFitter>();
            }
            backgroundFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            this.SubTextEnabled = false;
            this.HighlightEnabled = false;
            this.AccentEnabled = false;
            this._accent.gameObject.transform.SetParent(this.gameObject.transform, false);
            (this._accent.gameObject.transform as RectTransform).anchorMin = new Vector2(0, 0.5f);
            (this._accent.gameObject.transform as RectTransform).anchorMax = new Vector2(0, 0.5f);
            (this._accent.gameObject.transform as RectTransform).sizeDelta = new Vector2(1, 10);
            (this._accent.gameObject.transform as RectTransform).pivot = new Vector2(0, 0.5f);
            //var highlightLayoutGroup =_highlight.gameObject.AddComponent<VerticalLayoutGroup>();

            this.Text.rectTransform.SetParent(this.gameObject.transform, false);
        }

        public void LatePreRenderRebuildHandler(object sender, EventArgs e)
        {
            (this._accent.gameObject.transform as RectTransform).sizeDelta = new Vector2(1, (this.transform as RectTransform).sizeDelta.y);
            this._rebuiled = true;
        }
        
        protected void Update()
        {
            if (this._rebuiled)
            {
                OnLatePreRenderRebuildComplete?.Invoke();
                this._rebuiled = false;
            }
        }
        
        private void OnDestroy()
        {
            try
            {
                // 移除事件监听器
                if (this.Text != null)
                {
                    this.Text.OnLatePreRenderRebuildComplete -= this.Text_OnLatePreRenderRebuildComplete;
                    this.Text.RemoveReceiver(this);
                }
                
                if (this.SubText != null)
                {
                    this.SubText.OnLatePreRenderRebuildComplete -= this.Text_OnLatePreRenderRebuildComplete;
                    this.SubText.RemoveReceiver(this);
                }
                
                // 清除所有事件订阅者，防止在销毁后被调用
                OnLatePreRenderRebuildComplete = null;
                
                // 确保子对象不会在父对象销毁后继续存在
                if (this._accent != null && this._accent.gameObject != null)
                {
                    this._accent.gameObject.transform.SetParent(null);
                }
                
                if (this.Text != null && this.Text.gameObject != null)
                {
                    this.Text.rectTransform.SetParent(null);
                }
                
                if (this.SubText != null && this.SubText.gameObject != null)
                {
                    this.SubText.rectTransform.SetParent(null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[OnDestroy] Error during cleanup: {ex}");
            }
        }

        private void Text_OnLatePreRenderRebuildComplete()
        {
            // Logger.Debug("Text_OnLatePreRenderRebuildComplete");
            (this._accent.gameObject.transform as RectTransform).sizeDelta =
                new Vector2(1, (this.transform as RectTransform).sizeDelta.y);
            OnLatePreRenderRebuildComplete?.Invoke();
            // Logger.Debug("Text_OnLatePreRenderRebuildComplete: Invoke");
        }
        
        public class Pool : Zenject.MonoMemoryPool<EnhancedTextMeshProUGUIWithBackground>
    {
        protected override void OnCreated(EnhancedTextMeshProUGUIWithBackground item)
        {
            base.OnCreated(item);
            
            // 设置布局组件
            var verticalLayoutGroup = item.gameObject.GetComponent<VerticalLayoutGroup>();
            if (verticalLayoutGroup == null)
            {
                verticalLayoutGroup = item.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            verticalLayoutGroup.spacing = 1;
            
            // 设置内容大小适配器
            var backgroundFitter = item.gameObject.GetComponent<ContentSizeFitter>();
            if (backgroundFitter == null)
            {
                backgroundFitter = item.gameObject.AddComponent<ContentSizeFitter>();
            }
            backgroundFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // 初始化高亮组件（已在 Awake 中创建）
            if (item._highlight != null)
            {
                item._highlight.raycastTarget = false;
            }
            
            // 初始化强调色组件（已在 Awake 中创建）
            if (item._accent != null)
            {
                item._accent.raycastTarget = false;
                item._accent.color = Color.yellow;
                var highlightFitter = item._accent.gameObject.GetComponent<LayoutElement>();
                if (highlightFitter == null)
                {
                    highlightFitter = item._accent.gameObject.AddComponent<LayoutElement>();
                }
                highlightFitter.ignoreLayout = true;
                
                // 设置强调色位置
                var accentTransform = item._accent.gameObject.transform as RectTransform;
                accentTransform.anchorMin = new Vector2(0, 0.5f);
                accentTransform.anchorMax = new Vector2(0, 0.5f);
                accentTransform.sizeDelta = new Vector2(1, 10);
                accentTransform.pivot = new Vector2(0, 0.5f);
            }
            
            // 初始化文本组件（已在 Awake 中创建）
            if (item.Text != null)
            {
                var textFitter = item.Text.gameObject.GetComponent<ContentSizeFitter>();
                if (textFitter == null)
                {
                    textFitter = item.Text.gameObject.AddComponent<ContentSizeFitter>();
                }
                textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            
            // 设置默认状态 - 延迟到 Awake 之后
            // item.SubTextEnabled = false;
            // item.HighlightEnabled = false;
            // item.AccentEnabled = false;
        }
        
        protected override void OnSpawned(EnhancedTextMeshProUGUIWithBackground item)
        {
            base.OnSpawned(item);
            
            // 确保材质正确
            if (item._highlight != null && item._highlight.material != BeatSaberUtils.UINoGlowMaterial)
            {
                item._highlight.material = BeatSaberUtils.UINoGlowMaterial;
            }
            if (item._accent != null && item._accent.material != BeatSaberUtils.UINoGlowMaterial)
            {
                item._accent.material = BeatSaberUtils.UINoGlowMaterial;
            }
            
            // 设置默认状态
            item.SubTextEnabled = false;
            item.HighlightEnabled = false;
            item.AccentEnabled = false;
        }
        
        protected override void Reinitialize(EnhancedTextMeshProUGUIWithBackground msg)
        {
            base.Reinitialize(msg);
            
            if (msg.Text != null)
            {
                msg.Text.autoSizeTextContainer = false;
            }
            
            if (msg.SubText != null)
            {
                msg.SubText.enableWordWrapping = true;
                msg.SubText.autoSizeTextContainer = false;
            }
            
            var rectTransform = msg.transform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.pivot = new Vector2(0.5f, 0);
            }
        }
        
        protected override void OnDespawned(EnhancedTextMeshProUGUIWithBackground msg)
        {
            if (msg == null || msg.gameObject == null)
            {
                return;
            }
            
            try
            {
                base.OnDespawned(msg);
                
                // 移除事件监听器，防止内存泄漏
                msg.OnLatePreRenderRebuildComplete = null;
                
                // 重置状态 - 添加异常处理
                try
                {
                    msg.HighlightEnabled = false;
                    msg.AccentEnabled = false;
                    msg.SubTextEnabled = false;
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[OnDespawned] Failed to reset message states: {ex.Message}");
                }
                
                // 清理文本内容
                if (msg.Text != null)
                {
                    try
                    {
                        // 先清除聊天消息引用
                        if (msg.Text is EnhancedTextMeshProUGUI enhancedText)
                        {
                            enhancedText.ChatMessage = null;
                        }
                        
                        // 再清空文本
                        msg.Text.text = "";
                        
                        // 最后更新渲染
                        if (msg.Text.gameObject != null && msg.Text.enabled)
                        {
                            msg.Text.SetAllDirty();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[OnDespawned] Failed to clean main text: {ex.Message}");
                    }
                }
                
                if (msg.SubText != null)
                {
                    try
                    {
                        // 先清除聊天消息引用
                        if (msg.SubText is EnhancedTextMeshProUGUI enhancedSubText)
                        {
                            enhancedSubText.ChatMessage = null;
                        }
                        
                        // 再清空文本
                        msg.SubText.text = "";
                        
                        // 最后更新渲染
                        if (msg.SubText.gameObject != null && msg.SubText.enabled)
                        {
                            msg.SubText.SetAllDirty();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"[OnDespawned] Failed to clean sub text: {ex.Message}");
                    }
                }
                
                // 重置 Transform
                if (msg.transform != null)
                {
                    msg.transform.SetParent(null, false);
                    msg.transform.localPosition = Vector3.zero;
                    msg.transform.localScale = Vector3.one;
                }
                
                // 禁用 GameObject，防止渲染
                msg.gameObject.SetActive(false);
            }
            catch (Exception ex)
            {
                Logger.Error($"[OnDespawned] Unexpected error during cleanup: {ex}");
            }
        }
    }
    }
}