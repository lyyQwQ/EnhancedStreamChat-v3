using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;
using UnityEngine;
using UnityEngine.UI;

namespace EnhancedStreamChat.Chat
{
    public partial class ChatDisplay : BSMLAutomaticViewController
    {

        [UIAction("#post-parse")]
        private void PostParse()
        {
            // bg
            this._backgroundColorSetting.editButton.onClick.AddListener(this.HideSettings);
            this._backgroundColorSetting.modalColorPicker.cancelEvent += this.ShowSettings;
            this._backgroundColorSetting.CurrentColor = this._chatConfig.BackgroundColor;
            // accent
            this._accentColorSetting.editButton.onClick.AddListener(this.HideSettings);
            this._accentColorSetting.modalColorPicker.cancelEvent += this.ShowSettings;
            this._accentColorSetting.CurrentColor = this._chatConfig.AccentColor;
            // highlight
            this._highlightColorSetting.editButton.onClick.AddListener(this.HideSettings);
            this._highlightColorSetting.modalColorPicker.cancelEvent += this.ShowSettings;
            this._highlightColorSetting.CurrentColor = this._chatConfig.HighlightColor;
            // ping
            this._pingColorSetting.editButton.onClick.AddListener(this.HideSettings);
            this._pingColorSetting.modalColorPicker.cancelEvent += this.ShowSettings;
            this._pingColorSetting.CurrentColor = this._chatConfig.PingColor;
            // text
            this._textColorSetting.editButton.onClick.AddListener(this.HideSettings);
            this._textColorSetting.modalColorPicker.cancelEvent += this.ShowSettings;
            this._textColorSetting.CurrentColor = this._chatConfig.TextColor;

            // Move interactables in front of the screen
            this.settingsModalGameObject.transform.localPosition = new Vector3(this.settingsModalGameObject.transform.localPosition.x, this.settingsModalGameObject.transform.localPosition.y, -2f);
            this.settingsIconGameObject.transform.localPosition = new Vector3(this.settingsIconGameObject.transform.localPosition.x, this.settingsIconGameObject.transform.localPosition.y, -2f);
        }

        [UIParams]
        internal BSMLParserParams parserParams;

        [UIObject("settings-icon")]
        internal GameObject settingsIconGameObject;

        [UIObject("settings-modal")]
        internal GameObject settingsModalGameObject;

        [UIComponent("background-color-setting")]
        private readonly ColorSetting _backgroundColorSetting;

        [UIComponent("accent-color-setting")]
        private readonly ColorSetting _accentColorSetting;

        [UIComponent("highlight-color-setting")]
        private readonly ColorSetting _highlightColorSetting;

        [UIComponent("ping-color-setting")]
        private readonly ColorSetting _pingColorSetting;

        [UIComponent("text-color-setting")]
        private readonly ColorSetting _textColorSetting;

        private Color _accentColor;
        [UIValue("accent-color")]
        public Color AccentColor
        {
            get => this._chatConfig.AccentColor;
            set
            {
                this._chatConfig.AccentColor = value;
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("highlight-color")]
        public Color HighlightColor
        {
            get => this._chatConfig.HighlightColor;
            set
            {
                this._chatConfig.HighlightColor = value;
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("ping-color")]
        public Color PingColor
        {
            get => this._chatConfig.PingColor;
            set
            {
                this._chatConfig.PingColor = value;
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("background-color")]
        public Color BackgroundColor
        {
            get => this._chatConfig.BackgroundColor;
            set
            {
                this._chatConfig.BackgroundColor = value;
                this._chatScreen.GetComponentInChildren<ImageView>().material.color = value;
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("text-color")]
        public Color TextColor
        {
            get => this._chatConfig.TextColor;
            set
            {
                this._chatConfig.TextColor = value;
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("font-size")]
        public float FontSize
        {
            get => this._chatConfig.FontSize;
            set
            {
                this._chatConfig.FontSize = value;
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        private int _settingsWidth = 110;
        [UIValue("settings-width")]
        public int SettingsWidth
        {
            get => this._settingsWidth;
            set
            {
                this._settingsWidth = value;
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("chat-width")]
        public int ChatWidth
        {
            get => this._chatConfig.ChatWidth;
            set
            {
                this._chatConfig.ChatWidth = value;
                this._chatScreen.ScreenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatContainer.GetComponent<RectMask2D>().rectTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("chat-height")]
        public int ChatHeight
        {
            get => this._chatConfig.ChatHeight;
            set
            {
                this._chatConfig.ChatHeight = value;
                this._chatScreen.ScreenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatContainer.GetComponent<RectMask2D>().rectTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("chat-position")]
        public Vector3 ChatPosition
        {
            get => this._isInGame ? this._chatConfig.Song_ChatPosition : this._chatConfig.Menu_ChatPosition;
            set
            {
                if (this._isInGame || this.SyncOrientation) {
                    this._chatConfig.Song_ChatPosition = value;
                }

                if (!this._isInGame || this.SyncOrientation) {
                    this._chatConfig.Menu_ChatPosition = value;
                }

                this._chatScreen.ScreenPosition = value;
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("chat-rotation")]
        public Vector3 ChatRotation
        {
            get => this._isInGame ? this._chatConfig.Song_ChatRotation : this._chatConfig.Menu_ChatRotation;
            set
            {
                if (this._isInGame || this.SyncOrientation) {
                    this._chatConfig.Song_ChatRotation = value;
                }

                if (!this._isInGame || this.SyncOrientation) {
                    this._chatConfig.Menu_ChatRotation = value;
                }

                this._chatScreen.ScreenRotation = Quaternion.Euler(value);
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("allow-movement")]
        public bool AllowMovement
        {
            get => this._chatConfig.AllowMovement;
            set
            {
                this._chatConfig.AllowMovement = value;
                this._chatScreen.ShowHandle = value;
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("sync-orientation")]
        public bool SyncOrientation
        {
            get => this._chatConfig.SyncOrientation;
            set
            {
                this._chatConfig.SyncOrientation = value;

                if (value) {
                    this.ChatPosition = this.ChatPosition;
                    this.ChatRotation = this.ChatRotation;
                }

                this.NotifyPropertyChanged();
            }
        }

        [UIValue("reverse-chat-order")]
        public bool ReverseChatOrder
        {
            get => this._chatConfig.ReverseChatOrder;
            set
            {
                this._chatConfig.ReverseChatOrder = value;
                this.UpdateMessages();
                this.NotifyPropertyChanged();
            }
        }

        [UIValue("mod-version")]
        public string ModVersion => Plugin.Version;

        [UIAction("launch-web-app")]
        private void LaunchWebApp() => ChatManager.instance._sc.LaunchWebApp();

        [UIAction("launch-kofi")]
        private void LaunchKofi() => Application.OpenURL("https://ko-fi.com/brian91292");

        [UIAction("launch-github")]
        private void LaunchGitHub() => Application.OpenURL("https://github.com/Auros/EnhancedStreamChat-v3");

        [UIAction("on-settings-clicked")]
        private void OnSettingsClick() => Logger.log.Info("Settings clicked!");

        [UIAction("#hide-settings")]
        private void OnHideSettings()
        {
            Logger.log.Info("Saving settings!");
            this._chatConfig.Save();
        }

        private void HideSettings() => this.parserParams.EmitEvent("hide-settings");

        private void ShowSettings() => this.parserParams.EmitEvent("show-settings");
    }
}
