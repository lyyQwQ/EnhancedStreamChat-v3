using System;
using UnityEngine;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 聊天配置接口
    /// </summary>
    public interface IChatConfiguration
    {
        /// <summary>
        /// 配置变更事件
        /// </summary>
        event Action OnConfigChanged;

        // Main Settings
        /// <summary>
        /// 是否预缓存动画表情
        /// </summary>
        bool PreCacheAnimatedEmotes { get; }

        // UI Settings
        /// <summary>
        /// 系统字体名称
        /// </summary>
        string SystemFontName { get; }

        /// <summary>
        /// 背景颜色
        /// </summary>
        Color BackgroundColor { get; }

        /// <summary>
        /// 文字颜色
        /// </summary>
        Color TextColor { get; }

        /// <summary>
        /// 强调色
        /// </summary>
        Color AccentColor { get; }

        /// <summary>
        /// 高亮色
        /// </summary>
        Color HighlightColor { get; }

        /// <summary>
        /// Ping颜色
        /// </summary>
        Color PingColor { get; }

        // Layout Settings
        /// <summary>
        /// 聊天框宽度
        /// </summary>
        int ChatWidth { get; }

        /// <summary>
        /// 聊天框高度
        /// </summary>
        int ChatHeight { get; }

        /// <summary>
        /// 字体大小
        /// </summary>
        float FontSize { get; }

        /// <summary>
        /// 是否允许移动
        /// </summary>
        bool AllowMovement { get; }

        /// <summary>
        /// 是否同步方向
        /// </summary>
        bool SyncOrientation { get; }

        /// <summary>
        /// 是否反转聊天顺序
        /// </summary>
        bool ReverseChatOrder { get; }

        // Menu Layout
        /// <summary>
        /// 菜单中的聊天位置
        /// </summary>
        Vector3 Menu_ChatPosition { get; }

        /// <summary>
        /// 菜单中的聊天旋转
        /// </summary>
        Vector3 Menu_ChatRotation { get; }

        /// <summary>
        /// 菜单中的聊天层级
        /// </summary>
        int Menu_ChatLayer { get; }

        // Song Layout
        /// <summary>
        /// 游戏中的聊天位置
        /// </summary>
        Vector3 Song_ChatPosition { get; }

        /// <summary>
        /// 游戏中的聊天旋转
        /// </summary>
        Vector3 Song_ChatRotation { get; }

        /// <summary>
        /// 游戏中的聊天层级
        /// </summary>
        int Song_ChatLayer { get; }

        /// <summary>
        /// 触发配置变更事件
        /// </summary>
        void NotifyConfigChanged();
    }
}