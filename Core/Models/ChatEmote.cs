namespace EnhancedStreamChat.Core.Models
{
    /// <summary>
    /// 聊天表情数据模型
    /// </summary>
    public class ChatEmote
    {
        /// <summary>
        /// 表情ID
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// 表情名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 表情图片URL
        /// </summary>
        public string ImageUrl { get; set; }
        
        /// <summary>
        /// 是否为动画表情
        /// </summary>
        public bool IsAnimated { get; set; }
        
        /// <summary>
        /// 在消息中的起始位置
        /// </summary>
        public int StartIndex { get; set; }
        
        /// <summary>
        /// 在消息中的结束位置
        /// </summary>
        public int EndIndex { get; set; }
        
        /// <summary>
        /// 表情来源（Twitch、BTTV、FFZ、7TV、Bilibili等）
        /// </summary>
        public string Source { get; set; }
        
        /// <summary>
        /// 表情尺寸倍数（1x、2x、3x等）
        /// </summary>
        public int Scale { get; set; } = 1;
        
        /// <summary>
        /// 是否为全局表情
        /// </summary>
        public bool IsGlobal { get; set; }
        
        /// <summary>
        /// 获取指定尺寸的图片URL
        /// </summary>
        public string GetScaledUrl(int scale)
        {
            if (string.IsNullOrEmpty(ImageUrl))
                return ImageUrl;
                
            // 根据不同平台的URL格式进行替换
            if (ImageUrl.Contains("/1x") || ImageUrl.Contains("/2x") || ImageUrl.Contains("/3x"))
            {
                return ImageUrl.Replace("/1x", $"/{scale}x")
                              .Replace("/2x", $"/{scale}x")
                              .Replace("/3x", $"/{scale}x");
            }
            
            return ImageUrl;
        }
    }
}