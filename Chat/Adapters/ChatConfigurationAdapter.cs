using System;
using UnityEngine;
using EnhancedStreamChat.Core.Interfaces;

namespace EnhancedStreamChat.Chat.Adapters
{
    /// <summary>
    /// ChatConfig 的适配器，将 StreamCore 配置系统适配到 IChatConfiguration 接口
    /// </summary>
    public class ChatConfigurationAdapter : IChatConfiguration
    {
        private readonly ChatConfig _config;
        private bool _isInitialized;

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event Action OnConfigChanged;

        public ChatConfigurationAdapter()
        {
            _config = ChatConfig.instance;
            if (_config == null)
            {
                Logger.Error("[ChatConfigurationAdapter] ChatConfig.instance is null!");
                throw new InvalidOperationException("ChatConfig.instance must be initialized before creating adapter");
            }
            
            // 订阅 StreamCore 的配置变更事件
            SubscribeToConfigChanges();
            _isInitialized = true;
            
            Logger.Info("[ChatConfigurationAdapter] Initialized successfully");
        }

        private void SubscribeToConfigChanges()
        {
            // 订阅 StreamCore 配置系统的变更事件
            // ChatConfig 继承自 ConfigBase<ChatConfig>，OnConfigChanged 事件在基类中定义
            _config.OnConfigChanged += OnStreamCoreConfigChanged;
        }

        private void OnStreamCoreConfigChanged(ChatConfig config)
        {
            Logger.Debug("[ChatConfigurationAdapter] StreamCore config changed, forwarding notification");
            NotifyConfigChanged();
        }

        // 实现接口属性 - Main Settings
        public bool PreCacheAnimatedEmotes => _config.PreCacheAnimatedEmotes;

        // UI Settings
        public string SystemFontName => _config.SystemFontName;
        public Color BackgroundColor => _config.BackgroundColor;
        public Color TextColor => _config.TextColor;
        public Color AccentColor => _config.AccentColor;
        public Color HighlightColor => _config.HighlightColor;
        public Color PingColor => _config.PingColor;

        // Layout Settings
        public int ChatWidth => _config.ChatWidth;
        public int ChatHeight => _config.ChatHeight;
        public float FontSize => _config.FontSize;
        public bool AllowMovement => _config.AllowMovement;
        public bool SyncOrientation => _config.SyncOrientation;
        public bool ReverseChatOrder => _config.ReverseChatOrder;

        // Menu Layout
        public Vector3 Menu_ChatPosition => _config.Menu_ChatPosition;
        public Vector3 Menu_ChatRotation => _config.Menu_ChatRotation;
        public int Menu_ChatLayer => _config.Menu_ChatLayer;

        // Song Layout
        public Vector3 Song_ChatPosition => _config.Song_ChatPosition;
        public Vector3 Song_ChatRotation => _config.Song_ChatRotation;
        public int Song_ChatLayer => _config.Song_ChatLayer;

        /// <summary>
        /// 手动触发配置变更事件
        /// </summary>
        public void NotifyConfigChanged()
        {
            if (_isInitialized)
            {
                Logger.Debug("[ChatConfigurationAdapter] Notifying config changed");
                OnConfigChanged?.Invoke();
            }
        }

        /// <summary>
        /// 保存配置（StreamCore 会自动保存，这里提供手动保存接口）
        /// </summary>
        public void Save()
        {
            try
            {
                _config.Save();
                Logger.Debug("[ChatConfigurationAdapter] Configuration saved");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatConfigurationAdapter] Failed to save configuration: {ex}");
            }
        }

        /// <summary>
        /// 手动触发配置重载通知
        /// 注意：StreamCore 的配置系统会自动监听文件变化并重载，
        /// 这个方法主要用于手动触发配置变更通知
        /// </summary>
        public void TriggerReload()
        {
            try
            {
                NotifyConfigChanged();
                Logger.Debug("[ChatConfigurationAdapter] Configuration reload notification triggered");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ChatConfigurationAdapter] Failed to trigger reload notification: {ex}");
            }
        }
    }
}