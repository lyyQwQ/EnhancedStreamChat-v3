namespace EnhancedStreamChat.Core.Models
{
    /// <summary>
    /// 用户徽章数据模型
    /// </summary>
    public class ChatBadge
    {
        /// <summary>
        /// 徽章ID
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// 徽章名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 徽章图片URL
        /// </summary>
        public string ImageUrl { get; set; }
        
        /// <summary>
        /// 徽章描述
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// 徽章优先级（用于排序）
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// 徽章颜色（如果适用）
        /// </summary>
        public UnityEngine.Color? Color { get; set; }
        
        /// <summary>
        /// 徽章类型（订阅者、版主、VIP等）
        /// </summary>
        public BadgeType Type { get; set; }
    }
    
    /// <summary>
    /// 徽章类型枚举
    /// </summary>
    public enum BadgeType
    {
        /// <summary>
        /// 广播者/主播
        /// </summary>
        Broadcaster,
        
        /// <summary>
        /// 版主/管理员
        /// </summary>
        Moderator,
        
        /// <summary>
        /// VIP
        /// </summary>
        VIP,
        
        /// <summary>
        /// 订阅者
        /// </summary>
        Subscriber,
        
        /// <summary>
        /// 验证标记
        /// </summary>
        Verified,
        
        /// <summary>
        /// Twitch Prime
        /// </summary>
        Prime,
        
        /// <summary>
        /// Bilibili 舰长
        /// </summary>
        BilibiliCaptain,
        
        /// <summary>
        /// Bilibili 提督
        /// </summary>
        BilibiliAdmiral,
        
        /// <summary>
        /// Bilibili 总督
        /// </summary>
        BilibiliGovernor,
        
        /// <summary>
        /// 自定义徽章
        /// </summary>
        Custom
    }
}