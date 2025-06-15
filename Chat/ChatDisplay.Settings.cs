using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components.Settings;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using EnhancedStreamChat.Utilities;
using HMUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
#if DEBUG
using System.Threading.Tasks;
using EnhancedStreamChat.Tests;
using Zenject;
#endif

namespace EnhancedStreamChat.Chat
{
    public partial class ChatDisplay : BSMLAutomaticViewController
    {
#if DEBUG
        private GameplayTest _gameplayTest;
        private bool _isRunningTest = false;

        [Inject]
        public void InjectTestDependencies(GameplayTest gameplayTest)
        {
            _gameplayTest = gameplayTest;
        }
#endif

        private bool SetProperty<T>(ref T oldValue, T newValue, [CallerMemberName] string name = null)
        {
#if DEBUG
            Logger.Info($"Change value:{oldValue}, {newValue}");
#endif
            if (EqualityComparer<T>.Default.Equals(oldValue, newValue)) {
                return false;
            }
            oldValue = newValue;
            MainThreadInvoker.Invoke(() => this.OnPropertyChanged(new PropertyChangedEventArgs(name)));
            return true;
        }

        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            this.NotifyPropertyChanged(e.PropertyName);
#if DEBUG
            Logger.Info($"property changed:{e.PropertyName}");
#endif
            if (e.PropertyName == nameof(this.AccentColor)) {
                this._chatConfig.AccentColor = this.AccentColor;
            }
            else if (e.PropertyName == nameof(this.TextColor)) {
                this._chatConfig.TextColor = this.TextColor;
            }
            else if (e.PropertyName == nameof(this.BackgroundColor)) {
                this._chatConfig.BackgroundColor = this.BackgroundColor;
            }
            else if (e.PropertyName == nameof(this.AllowMovement)) {
                this._chatConfig.AllowMovement = this.AllowMovement;
            }
            else if (e.PropertyName == nameof(this.ChatHeight)) {
                this._chatConfig.ChatHeight = this.ChatHeight;
            }
            else if (e.PropertyName == nameof(this.ChatWidth)) {
                this._chatConfig.ChatWidth = this.ChatWidth;
            }
            else if (e.PropertyName == nameof(this.ChatPosition)) {
                if (this._chatConfig.SyncOrientation) {
                    this._chatConfig.Menu_ChatPosition = this.ChatPosition;
                    this._chatConfig.Song_ChatPosition = this.ChatPosition;
                }
                else {
                    if (this._isInGame) {
                        this._chatConfig.Song_ChatPosition = this.ChatPosition;
                    }
                    else {
                        this._chatConfig.Menu_ChatPosition = this.ChatPosition;
                    }
                }
            }
            else if (e.PropertyName == nameof(this.ChatRotation)) {
                if (this._chatConfig.SyncOrientation) {
                    this._chatConfig.Menu_ChatRotation = this.ChatRotation;
                    this._chatConfig.Song_ChatRotation = this.ChatRotation;
                }
                else {
                    if (this._isInGame) {
                        this._chatConfig.Song_ChatRotation = this.ChatRotation;
                    }
                    else {
                        this._chatConfig.Menu_ChatRotation = this.ChatRotation;
                    }
                }
            }
            else if (e.PropertyName == nameof(this.FontSize)) {
                this._chatConfig.FontSize = this.FontSize;
            }
            else if (e.PropertyName == nameof(this.HighlightColor)) {
                this._chatConfig.HighlightColor = this.HighlightColor;
            }
            else if (e.PropertyName == nameof(this.PingColor)) {
                this._chatConfig.PingColor = this.PingColor;
            }
            else if (e.PropertyName == nameof(this.ReverseChatOrder)) {
                this._chatConfig.ReverseChatOrder = this.ReverseChatOrder;
            }
            else if (e.PropertyName == nameof(this.SyncOrientation)) {
                this._chatConfig.SyncOrientation = this.SyncOrientation;
                if (this._chatConfig.SyncOrientation) {
                    if (this._isInGame) {
                        this._chatConfig.Menu_ChatPosition = this._chatConfig.Song_ChatPosition;
                        this._chatConfig.Menu_ChatRotation = this._chatConfig.Song_ChatRotation;
                    }
                    else {
                        this._chatConfig.Song_ChatPosition = this._chatConfig.Menu_ChatPosition;
                        this._chatConfig.Song_ChatRotation = this._chatConfig.Menu_ChatRotation;
                    }
                }
            }
        }

        [UIAction("#post-parse")]
        protected void PostParse()
        {
            this.Load();
            // bg
            this._backgroundColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._backgroundColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._backgroundColorSetting.CurrentColor = this._chatConfig.BackgroundColor;
            // accent
            this._accentColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._accentColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._accentColorSetting.CurrentColor = this._chatConfig.AccentColor;
            // highlight
            this._highlightColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._highlightColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._highlightColorSetting.CurrentColor = this._chatConfig.HighlightColor;
            // ping
            this._pingColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._pingColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._pingColorSetting.CurrentColor = this._chatConfig.PingColor;
            // text
            this._textColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._textColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._textColorSetting.CurrentColor = this._chatConfig.TextColor;
            // layer
            this.gameObject.layer = (int)this.textLayerVisibility;

            // Move interactables in front of the screen
            this.settingsModalGameObject.transform.localPosition = new Vector3(this.settingsModalGameObject.transform.localPosition.x, this.settingsModalGameObject.transform.localPosition.y, -2f);
            this.settingsIconGameObject.transform.localPosition = new Vector3(this.settingsIconGameObject.transform.localPosition.x, this.settingsIconGameObject.transform.localPosition.y, -2f);

            this.settingsIconGameObject.layer = 5;
            this.settingsModalGameObject.layer = 5;
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

        [UIComponent("text-layer-settings")]
        private readonly DropDownListSetting _textLayerSetting;

        internal BeatSaberUtils.TextLayerVisibility textLayerVisibility
        {
            get => this._isInGame ? BeatSaberUtils.textLayerVisibilityReverser(this._chatConfig.Song_ChatLayer) : BeatSaberUtils.textLayerVisibilityReverser(this._chatConfig.Menu_ChatLayer);
            set
            {
                if (this._isInGame || this.SyncOrientation)
                {
                    this._chatConfig.Song_ChatLayer = (int)value;
                }

                if (!this._isInGame || this.SyncOrientation)
                {
                    this._chatConfig.Menu_ChatLayer = (int)value;
                }
            }
        }

        private static readonly List<object> textLayerVisibilities = Enum.GetValues(typeof(BeatSaberUtils.TextLayerVisibility)).Cast<object>().ToList();

        [UIValue("accent-color")]
        public Color AccentColor
        {
            get => this._chatConfig.AccentColor;
            set
            {
                this.SetProperty(ref this._chatConfig.AccentColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("highlight-color")]
        public Color HighlightColor
        {
            get => this._chatConfig.HighlightColor;
            set
            {
                this.SetProperty(ref this._chatConfig.HighlightColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("ping-color")]
        public Color PingColor
        {
            get => this._chatConfig.PingColor;
            set
            {
                this.SetProperty(ref this._chatConfig.PingColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("background-color")]
        public Color BackgroundColor
        {
            get => this._chatConfig.BackgroundColor;
            set
            {
                this.SetProperty(ref this._chatConfig.BackgroundColor, value);
                this._chatScreen.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Background").color = value;
            }
        }

        [UIValue("text-color")]
        public Color TextColor
        {
            get => this._chatConfig.TextColor;
            set
            {
                this.SetProperty(ref this._chatConfig.TextColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("font-size")]
        public float FontSize
        {
            get => this._chatConfig.FontSize;
            set
            {
                this.SetProperty(ref this._chatConfig.FontSize, value);
                this.UpdateMessages();
            }
        }

        private int _settingsWidth = 110;
        [UIValue("settings-width")]
        public int SettingsWidth
        {
            get => this._settingsWidth;
            set => this.SetProperty(ref this._settingsWidth, value);
        }

        [UIValue("chat-width")]
        public int ChatWidth
        {
            get => this._chatConfig.ChatWidth;
            set
            {
                this.SetProperty(ref this._chatConfig.ChatWidth, value);
                this._chatScreen.ScreenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatContainer.GetComponent<RectMask2D>().rectTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
                this.UpdateMessages();
            }
        }

        [UIValue("chat-height")]
        public int ChatHeight
        {
            get => this._chatConfig.ChatHeight;
            set
            {
                this.SetProperty(ref this._chatConfig.ChatHeight, value);
                this._chatScreen.ScreenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatContainer.GetComponent<RectMask2D>().rectTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
                this.UpdateMessages();
            }
        }

        [UIValue("chat-position")]
        public Vector3 ChatPosition
        {
            get => this._isInGame ? this._chatConfig.Song_ChatPosition : this._chatConfig.Menu_ChatPosition;
            set
            {
                this._chatScreen.ScreenPosition = value;
                if (this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfig.Song_ChatPosition, value);
                }

                if (!this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfig.Menu_ChatPosition, value);
                }
            }
        }

        [UIValue("chat-rotation")]
        public Vector3 ChatRotation
        {
            get => this._isInGame ? this._chatConfig.Song_ChatRotation : this._chatConfig.Menu_ChatRotation;
            set
            {
                this._chatScreen.ScreenRotation = Quaternion.Euler(value);
                if (this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfig.Song_ChatRotation, value);
                }

                if (!this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfig.Menu_ChatRotation, value);
                }
            }
        }

        [UIValue("allow-movement")]
        public bool AllowMovement
        {
            get => this._chatConfig.AllowMovement;
            set
            {
                this.SetProperty(ref this._chatConfig.AllowMovement, value);
                this._chatScreen.ShowHandle = value;
            }
        }

        [UIValue("sync-orientation")]
        public bool SyncOrientation
        {
            get => this._chatConfig.SyncOrientation;
            set
            {
                this.SetProperty(ref this._chatConfig.SyncOrientation, value);
                if (value) {
                    this.ChatPosition = this.ChatPosition;
                    this.ChatRotation = this.ChatRotation;
                }
            }
        }

        [UIValue("reverse-chat-order")]
        public bool ReverseChatOrder
        {
            get => this._chatConfig.ReverseChatOrder;
            set
            {
                this.SetProperty(ref this._chatConfig.ReverseChatOrder, value);
                this.UpdateMessages();
            }
        }

        [UIValue("mod-version")]
        public string ModVersion => Plugin.Version;

        [UIAction("launch-web-app")]
        private void LaunchWebApp() => ChatManager.instance._chatCoreInstance.LaunchWebApp();

        [UIAction("launch-kofi")]
        private void LaunchKofi() => Application.OpenURL("https://ko-fi.com/brian91292");

        [UIAction("launch-github")]
        private void LaunchGitHub() => Application.OpenURL("https://github.com/baoziii/EnhancedStreamChat-v3");

        [UIAction("on-settings-clicked")]
        private void OnSettingsClick() => Logger.Info("Settings clicked!");

#if DEBUG
        [UIAction("run-adapter-tests")]
        private async void RunAdapterTests()
        {
            if (_isRunningTest)
            {
                Logger.Log.Warn("[TEST] 测试已经在运行中，请等待完成");
                return;
            }

            _isRunningTest = true;
            Logger.Log.Info("[TEST] 开始运行适配器测试...");

            try
            {
                if (_gameplayTest != null)
                {
                    await _gameplayTest.RunAllTests();
                    Logger.Log.Info("[TEST] 适配器测试完成");
                }
                else
                {
                    Logger.Log.Error("[TEST] GameplayTest 未注入，无法运行测试");
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"[TEST] 运行测试时发生错误: {ex}");
            }
            finally
            {
                _isRunningTest = false;
            }
        }

        [UIValue("test-button-interactable")]
        public bool TestButtonInteractable => !_isRunningTest;

        [UIValue("test-button-text")]
        public string TestButtonText => _isRunningTest ? "测试运行中..." : "运行适配器测试";
#endif

        [UIAction("#hide-settings")]
        private void OnHideSettings()
        {
            Logger.Info("Saving settings!");
            this._chatConfig.Save();
        }

        private void HideSettings() => this.parserParams.EmitEvent("hide-settings");

        private void ShowSettings() => this.parserParams.EmitEvent("show-settings");
        private void Load()
        {
            this.AccentColor = this._chatConfig.AccentColor;
            this.TextColor = this._chatConfig.TextColor;
            this.BackgroundColor = this._chatConfig.BackgroundColor;
            this.AllowMovement = this._chatConfig.AllowMovement;
            this.ChatHeight = this._chatConfig.ChatHeight;
            this.ChatWidth = this._chatConfig.ChatWidth;
            this.FontSize = this._chatConfig.FontSize;
            this.HighlightColor = this._chatConfig.HighlightColor;
            this.PingColor = this._chatConfig.PingColor;
            this.ReverseChatOrder = this._chatConfig.ReverseChatOrder;
            this.SyncOrientation = this._chatConfig.SyncOrientation;
            if (this._isInGame) {
                this.ChatPosition = this._chatConfig.Song_ChatPosition;
                this.ChatRotation = this._chatConfig.Song_ChatRotation;
            }
            else {
                this.ChatPosition = this._chatConfig.Menu_ChatPosition;
                this.ChatRotation = this._chatConfig.Menu_ChatRotation;
            }
        }
    }


}
