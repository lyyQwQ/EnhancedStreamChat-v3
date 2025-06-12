using System.Collections.Generic;

namespace EnhancedStreamChat.Core.Events
{
    /// <summary>
    /// 配置变更事件
    /// </summary>
    public class ConfigurationChangedEvent
    {
        /// <summary>
        /// 变更的配置属性名称
        /// </summary>
        public string PropertyName { get; }
        
        /// <summary>
        /// 旧值
        /// </summary>
        public object OldValue { get; }
        
        /// <summary>
        /// 新值
        /// </summary>
        public object NewValue { get; }
        
        /// <summary>
        /// 是否需要重新渲染UI
        /// </summary>
        public bool RequiresUIRefresh { get; }
        
        /// <summary>
        /// 是否需要重新加载资源
        /// </summary>
        public bool RequiresResourceReload { get; }
        
        /// <summary>
        /// 创建配置变更事件
        /// </summary>
        public ConfigurationChangedEvent(
            string propertyName, 
            object oldValue, 
            object newValue,
            bool requiresUIRefresh = false,
            bool requiresResourceReload = false)
        {
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
            RequiresUIRefresh = requiresUIRefresh;
            RequiresResourceReload = requiresResourceReload;
        }
        
        /// <summary>
        /// 需要UI刷新的配置属性列表
        /// </summary>
        public static readonly HashSet<string> UIRefreshProperties = new HashSet<string>
        {
            nameof(Interfaces.IChatConfiguration.FontSize),
            nameof(Interfaces.IChatConfiguration.LineSpacing),
            nameof(Interfaces.IChatConfiguration.ChatWidth),
            nameof(Interfaces.IChatConfiguration.ChatHeight),
            nameof(Interfaces.IChatConfiguration.BackgroundColor),
            nameof(Interfaces.IChatConfiguration.TextColor),
            nameof(Interfaces.IChatConfiguration.ReverseChatOrder)
        };
        
        /// <summary>
        /// 需要资源重载的配置属性列表
        /// </summary>
        public static readonly HashSet<string> ResourceReloadProperties = new HashSet<string>
        {
            nameof(Interfaces.IChatConfiguration.FontName),
            nameof(Interfaces.IChatConfiguration.EnableAnimatedEmotes),
            nameof(Interfaces.IChatConfiguration.PreloadEmotes)
        };
        
        /// <summary>
        /// 创建带有自动检测的配置变更事件
        /// </summary>
        public static ConfigurationChangedEvent CreateWithAutoDetection(
            string propertyName,
            object oldValue,
            object newValue)
        {
            return new ConfigurationChangedEvent(
                propertyName,
                oldValue,
                newValue,
                UIRefreshProperties.Contains(propertyName),
                ResourceReloadProperties.Contains(propertyName)
            );
        }
    }
}