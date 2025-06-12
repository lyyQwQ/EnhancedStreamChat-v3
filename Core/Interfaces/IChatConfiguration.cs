using System;
using System.ComponentModel;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Core.Interfaces
{
    /// <summary>
    /// 聊天配置接口，支持动态配置更新
    /// </summary>
    public interface IChatConfiguration : INotifyPropertyChanged
    {
        // 显示设置
        bool Enabled { get; set; }
        int MaxMessages { get; set; }
        float MessageLifetime { get; set; }
        bool ReverseChatOrder { get; set; }
        
        // 渲染设置
        float FontSize { get; set; }
        float LineSpacing { get; set; }
        string FontName { get; set; }
        int ChatWidth { get; set; }
        int ChatHeight { get; set; }
        
        // 颜色设置
        Color BackgroundColor { get; set; }
        Color TextColor { get; set; }
        Color AccentColor { get; set; }
        Color HighlightColor { get; set; }
        
        // 位置设置
        ChatPosition MenuPosition { get; set; }
        ChatPosition GamePosition { get; set; }
        bool SyncPositions { get; set; }
        
        // 性能设置
        bool EnableAnimatedEmotes { get; set; }
        int MaxImagesPerMessage { get; set; }
        bool PreloadEmotes { get; set; }
        
        // 保存/加载
        Task SaveAsync();
        Task LoadAsync();
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        void ResetToDefaults();
    }
    
    /// <summary>
    /// 聊天位置配置
    /// </summary>
    public struct ChatPosition : IEquatable<ChatPosition>
    {
        public Vector3 Position { get; set; }
        public Vector3 Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public int Layer { get; set; }
        
        public ChatPosition(Vector3 position, Vector3 rotation, Vector3 scale, int layer = 5)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Layer = layer;
        }
        
        public bool Equals(ChatPosition other)
        {
            return Position.Equals(other.Position) && 
                   Rotation.Equals(other.Rotation) && 
                   Scale.Equals(other.Scale) && 
                   Layer == other.Layer;
        }
        
        public override bool Equals(object obj)
        {
            return obj is ChatPosition other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Position.GetHashCode();
                hashCode = (hashCode * 397) ^ Rotation.GetHashCode();
                hashCode = (hashCode * 397) ^ Scale.GetHashCode();
                hashCode = (hashCode * 397) ^ Layer;
                return hashCode;
            }
        }
        
        public static bool operator ==(ChatPosition left, ChatPosition right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(ChatPosition left, ChatPosition right)
        {
            return !left.Equals(right);
        }
    }
}