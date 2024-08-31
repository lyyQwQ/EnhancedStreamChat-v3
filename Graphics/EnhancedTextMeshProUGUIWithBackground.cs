using EnhancedStreamChat.Utilities;
using HMUI;
using System;
using EnhancedStreamChat.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedTextMeshProUGUIWithBackground : MonoBehaviour, ILatePreRenderRebuildReciver
    {
        public EnhancedTextMeshProUGUI Text { get; internal set; }

        public EnhancedTextMeshProUGUI SubText { get; internal set; }

        public DateTime ReceivedDate { get; internal set; }

        public event Action OnLatePreRenderRebuildComplete;

        private ImageView _highlight;
        private ImageView _accent;
        private VerticalLayoutGroup _verticalLayoutGroup;
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
            get => this.SubText.enabled;
            set
            {
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
            this._highlight = this.gameObject.AddComponent<ImageView>();
            this._highlight.raycastTarget = false;
            this._highlight.material = BeatSaberUtils.UINoGlowMaterial;
            this.Text = new GameObject().AddComponent<EnhancedTextMeshProUGUI>();
            DontDestroyOnLoad(this.Text.gameObject);
            this.Text.OnLatePreRenderRebuildComplete += this.Text_OnLatePreRenderRebuildComplete;

            this.SubText = new GameObject().AddComponent<EnhancedTextMeshProUGUI>();
            DontDestroyOnLoad(this.SubText.gameObject);
            this.SubText.OnLatePreRenderRebuildComplete += this.Text_OnLatePreRenderRebuildComplete;

            this._accent = new GameObject().AddComponent<ImageView>();
            this._accent.raycastTarget = false;
            DontDestroyOnLoad(this._accent.gameObject);
            this._accent.material = BeatSaberUtils.UINoGlowMaterial;
            this._accent.color = Color.yellow;

            this._verticalLayoutGroup = this.gameObject.AddComponent<VerticalLayoutGroup>();
            this._verticalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            this._verticalLayoutGroup.spacing = 1;

            var highlightFitter = this._accent.gameObject.AddComponent<LayoutElement>();
            highlightFitter.ignoreLayout = true;
            var textFitter = this.Text.gameObject.AddComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var backgroundFitter = this.gameObject.AddComponent<ContentSizeFitter>();
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
        private void OnDestroy()
        {
            this.Text.OnLatePreRenderRebuildComplete -= this.Text_OnLatePreRenderRebuildComplete;
            this.SubText.OnLatePreRenderRebuildComplete -= this.Text_OnLatePreRenderRebuildComplete;
        }

        private void Text_OnLatePreRenderRebuildComplete()
        {
            // Logger.Debug("Text_OnLatePreRenderRebuildComplete");
            (this._accent.gameObject.transform as RectTransform).sizeDelta =
                new Vector2(1, (this.transform as RectTransform).sizeDelta.y);
            OnLatePreRenderRebuildComplete?.Invoke();
            // Logger.Debug("Text_OnLatePreRenderRebuildComplete: Invoke");
        }
    }
}