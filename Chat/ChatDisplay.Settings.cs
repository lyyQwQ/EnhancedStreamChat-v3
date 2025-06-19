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
                this._chatConfigInstance.AccentColor = this.AccentColor;
            }
            else if (e.PropertyName == nameof(this.TextColor)) {
                this._chatConfigInstance.TextColor = this.TextColor;
            }
            else if (e.PropertyName == nameof(this.BackgroundColor)) {
                this._chatConfigInstance.BackgroundColor = this.BackgroundColor;
            }
            else if (e.PropertyName == nameof(this.AllowMovement)) {
                this._chatConfigInstance.AllowMovement = this.AllowMovement;
            }
            else if (e.PropertyName == nameof(this.ChatHeight)) {
                this._chatConfigInstance.ChatHeight = this.ChatHeight;
            }
            else if (e.PropertyName == nameof(this.ChatWidth)) {
                this._chatConfigInstance.ChatWidth = this.ChatWidth;
            }
            else if (e.PropertyName == nameof(this.ChatPosition)) {
                if (this._chatConfigInstance.SyncOrientation) {
                    this._chatConfigInstance.Menu_ChatPosition = this.ChatPosition;
                    this._chatConfigInstance.Song_ChatPosition = this.ChatPosition;
                }
                else {
                    if (this._isInGame) {
                        this._chatConfigInstance.Song_ChatPosition = this.ChatPosition;
                    }
                    else {
                        this._chatConfigInstance.Menu_ChatPosition = this.ChatPosition;
                    }
                }
            }
            else if (e.PropertyName == nameof(this.ChatRotation)) {
                if (this._chatConfigInstance.SyncOrientation) {
                    this._chatConfigInstance.Menu_ChatRotation = this.ChatRotation;
                    this._chatConfigInstance.Song_ChatRotation = this.ChatRotation;
                }
                else {
                    if (this._isInGame) {
                        this._chatConfigInstance.Song_ChatRotation = this.ChatRotation;
                    }
                    else {
                        this._chatConfigInstance.Menu_ChatRotation = this.ChatRotation;
                    }
                }
            }
            else if (e.PropertyName == nameof(this.FontSize)) {
                this._chatConfigInstance.FontSize = this.FontSize;
            }
            else if (e.PropertyName == nameof(this.HighlightColor)) {
                this._chatConfigInstance.HighlightColor = this.HighlightColor;
            }
            else if (e.PropertyName == nameof(this.PingColor)) {
                this._chatConfigInstance.PingColor = this.PingColor;
            }
            else if (e.PropertyName == nameof(this.ReverseChatOrder)) {
                this._chatConfigInstance.ReverseChatOrder = this.ReverseChatOrder;
            }
            else if (e.PropertyName == nameof(this.SyncOrientation)) {
                this._chatConfigInstance.SyncOrientation = this.SyncOrientation;
                if (this._chatConfigInstance.SyncOrientation) {
                    if (this._isInGame) {
                        this._chatConfigInstance.Menu_ChatPosition = this._chatConfigInstance.Song_ChatPosition;
                        this._chatConfigInstance.Menu_ChatRotation = this._chatConfigInstance.Song_ChatRotation;
                    }
                    else {
                        this._chatConfigInstance.Song_ChatPosition = this._chatConfigInstance.Menu_ChatPosition;
                        this._chatConfigInstance.Song_ChatRotation = this._chatConfigInstance.Menu_ChatRotation;
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
            this._backgroundColorSetting.CurrentColor = this._chatConfigInstance.BackgroundColor;
            // accent
            this._accentColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._accentColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._accentColorSetting.CurrentColor = this._chatConfigInstance.AccentColor;
            // highlight
            this._highlightColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._highlightColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._highlightColorSetting.CurrentColor = this._chatConfigInstance.HighlightColor;
            // ping
            this._pingColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._pingColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._pingColorSetting.CurrentColor = this._chatConfigInstance.PingColor;
            // text
            this._textColorSetting.EditButton.onClick.AddListener(this.HideSettings);
            this._textColorSetting.ModalColorPicker.CancelEvent += this.ShowSettings;
            this._textColorSetting.CurrentColor = this._chatConfigInstance.TextColor;
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
            get => this._isInGame ? BeatSaberUtils.textLayerVisibilityReverser(this._chatConfigInstance.Song_ChatLayer) : BeatSaberUtils.textLayerVisibilityReverser(this._chatConfigInstance.Menu_ChatLayer);
            set
            {
                if (this._isInGame || this.SyncOrientation)
                {
                    this._chatConfigInstance.Song_ChatLayer = (int)value;
                }

                if (!this._isInGame || this.SyncOrientation)
                {
                    this._chatConfigInstance.Menu_ChatLayer = (int)value;
                }
            }
        }

        private static readonly List<object> textLayerVisibilities = Enum.GetValues(typeof(BeatSaberUtils.TextLayerVisibility)).Cast<object>().ToList();

        [UIValue("accent-color")]
        public Color AccentColor
        {
            get => this._chatConfigInstance.AccentColor;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.AccentColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("highlight-color")]
        public Color HighlightColor
        {
            get => this._chatConfigInstance.HighlightColor;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.HighlightColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("ping-color")]
        public Color PingColor
        {
            get => this._chatConfigInstance.PingColor;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.PingColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("background-color")]
        public Color BackgroundColor
        {
            get => this._chatConfigInstance.BackgroundColor;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.BackgroundColor, value);
                this._chatScreen.GetComponentsInChildren<ImageView>().FirstOrDefault(x => x.name == "Background").color = value;
            }
        }

        [UIValue("text-color")]
        public Color TextColor
        {
            get => this._chatConfigInstance.TextColor;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.TextColor, value);
                this.UpdateMessages();
            }
        }

        [UIValue("font-size")]
        public float FontSize
        {
            get => this._chatConfigInstance.FontSize;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.FontSize, value);
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
            get => this._chatConfigInstance.ChatWidth;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.ChatWidth, value);
                this._chatScreen.ScreenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatContainer.GetComponent<RectMask2D>().rectTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
                this.UpdateMessages();
            }
        }

        [UIValue("chat-height")]
        public int ChatHeight
        {
            get => this._chatConfigInstance.ChatHeight;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.ChatHeight, value);
                this._chatScreen.ScreenSize = new Vector2(this.ChatWidth, this.ChatHeight);
                this._chatContainer.GetComponent<RectMask2D>().rectTransform.sizeDelta = new Vector2(this.ChatWidth, this.ChatHeight);
                this.UpdateMessages();
            }
        }

        [UIValue("chat-position")]
        public Vector3 ChatPosition
        {
            get => this._isInGame ? this._chatConfigInstance.Song_ChatPosition : this._chatConfigInstance.Menu_ChatPosition;
            set
            {
                this._chatScreen.ScreenPosition = value;
                if (this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfigInstance.Song_ChatPosition, value);
                }

                if (!this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfigInstance.Menu_ChatPosition, value);
                }
            }
        }

        [UIValue("chat-rotation")]
        public Vector3 ChatRotation
        {
            get => this._isInGame ? this._chatConfigInstance.Song_ChatRotation : this._chatConfigInstance.Menu_ChatRotation;
            set
            {
                this._chatScreen.ScreenRotation = Quaternion.Euler(value);
                if (this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfigInstance.Song_ChatRotation, value);
                }

                if (!this._isInGame || this.SyncOrientation) {
                    this.SetProperty(ref this._chatConfigInstance.Menu_ChatRotation, value);
                }
            }
        }

        [UIValue("allow-movement")]
        public bool AllowMovement
        {
            get => this._chatConfigInstance.AllowMovement;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.AllowMovement, value);
                this._chatScreen.ShowHandle = value;
            }
        }

        [UIValue("sync-orientation")]
        public bool SyncOrientation
        {
            get => this._chatConfigInstance.SyncOrientation;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.SyncOrientation, value);
                if (value) {
                    this.ChatPosition = this.ChatPosition;
                    this.ChatRotation = this.ChatRotation;
                }
            }
        }

        [UIValue("reverse-chat-order")]
        public bool ReverseChatOrder
        {
            get => this._chatConfigInstance.ReverseChatOrder;
            set
            {
                this.SetProperty(ref this._chatConfigInstance.ReverseChatOrder, value);
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

        [UIAction("reset-chat-position")]
        private void ResetChatPosition()
        {
            // 重置位置和旋转到默认值
            Vector3 defaultPosition = new Vector3(0, 3.75f, 2.5f);
            Vector3 defaultRotation = new Vector3(325, 0, 0);
            
            if (this.SyncOrientation)
            {
                // 如果同步方向开启，同时重置菜单和游戏中的位置
                this.ChatPosition = defaultPosition;
                this.ChatRotation = defaultRotation;
            }
            else
            {
                // 根据当前是在游戏中还是菜单中，重置对应的位置
                if (this._isInGame)
                {
                    this._chatConfigInstance.Song_ChatPosition = defaultPosition;
                    this._chatConfigInstance.Song_ChatRotation = defaultRotation;
                    this.ChatPosition = defaultPosition;
                    this.ChatRotation = defaultRotation;
                }
                else
                {
                    this._chatConfigInstance.Menu_ChatPosition = defaultPosition;
                    this._chatConfigInstance.Menu_ChatRotation = defaultRotation;
                    this.ChatPosition = defaultPosition;
                    this.ChatRotation = defaultRotation;
                }
            }
            
            // 立即保存配置
            this._chatConfigInstance.Save();
            Logger.Info("Chat position and rotation reset to default values");
        }

        [UIAction("#hide-settings")]
        private void OnHideSettings()
        {
            Logger.Info("Saving settings!");
            this._chatConfigInstance.Save();
        }

        private void HideSettings() => this.parserParams.EmitEvent("hide-settings");

        private void ShowSettings() => this.parserParams.EmitEvent("show-settings");
        private void Load()
        {
            this.AccentColor = this._chatConfigInstance.AccentColor;
            this.TextColor = this._chatConfigInstance.TextColor;
            this.BackgroundColor = this._chatConfigInstance.BackgroundColor;
            this.AllowMovement = this._chatConfigInstance.AllowMovement;
            this.ChatHeight = this._chatConfigInstance.ChatHeight;
            this.ChatWidth = this._chatConfigInstance.ChatWidth;
            this.FontSize = this._chatConfigInstance.FontSize;
            this.HighlightColor = this._chatConfigInstance.HighlightColor;
            this.PingColor = this._chatConfigInstance.PingColor;
            this.ReverseChatOrder = this._chatConfigInstance.ReverseChatOrder;
            this.SyncOrientation = this._chatConfigInstance.SyncOrientation;
            if (this._isInGame) {
                this.ChatPosition = this._chatConfigInstance.Song_ChatPosition;
                this.ChatRotation = this._chatConfigInstance.Song_ChatRotation;
            }
            else {
                this.ChatPosition = this._chatConfigInstance.Menu_ChatPosition;
                this.ChatRotation = this._chatConfigInstance.Menu_ChatRotation;
            }
        }
    }


}
