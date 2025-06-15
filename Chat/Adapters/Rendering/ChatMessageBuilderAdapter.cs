using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatCore.Interfaces;
using ChatCore.Models;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Chat.Adapters.Rendering
{
    /// <summary>
    /// Adapter that bridges the legacy ChatMessageBuilder with the new architecture
    /// </summary>
    public class ChatMessageBuilderAdapter : IInitializable, IDisposable
    {
        private readonly IImageProvider _imageProvider;
        private readonly IFontProvider _fontProvider;
        private readonly System.Random _random = new System.Random(Environment.TickCount);
        
        // Legacy ChatMessageBuilder instance (for backwards compatibility)
        private readonly ChatMessageBuilder _legacyBuilder;
        
        // Cache for user colors
        private readonly Dictionary<string, Color> _userColorCache = new Dictionary<string, Color>();
        
        [Inject]
        public ChatMessageBuilderAdapter(
            IImageProvider imageProvider,
            IFontProvider fontProvider)
        {
            _imageProvider = imageProvider ?? throw new ArgumentNullException(nameof(imageProvider));
            _fontProvider = fontProvider ?? throw new ArgumentNullException(nameof(fontProvider));
            _legacyBuilder = new ChatMessageBuilder();
        }
        
        public void Initialize()
        {
            Logger.Log.Info("[ChatMessageBuilderAdapter] Initialized");
        }
        
        public void Dispose()
        {
            _userColorCache.Clear();
            Logger.Log.Info("[ChatMessageBuilderAdapter] Disposed");
        }
        
        /// <summary>
        /// Prepares images for a message (loads and registers them with the font)
        /// </summary>
        public async Task<bool> PrepareImagesAsync(ChatMessage message, EnhancedFontInfo font)
        {
            if (message == null || font == null)
                return false;
                
            var tasks = new List<Task<bool>>();
            var processedIds = new HashSet<string>();
            
            // Process emotes
            foreach (var emote in message.Emotes)
            {
                if (string.IsNullOrEmpty(emote.Id) || processedIds.Contains(emote.Id))
                    continue;
                    
                processedIds.Add(emote.Id);
                
                if (!font.CharacterLookupTable.ContainsKey(emote.Id))
                {
                    tasks.Add(LoadAndRegisterImageAsync(emote.Id, emote.Uri, emote.IsAnimated, font, 110));
                }
            }
            
            // Process badges
            foreach (var badge in message.Badges)
            {
                if (string.IsNullOrEmpty(badge.Id) || processedIds.Contains(badge.Id))
                    continue;
                    
                processedIds.Add(badge.Id);
                
                if (!font.CharacterLookupTable.ContainsKey(badge.Id))
                {
                    tasks.Add(LoadAndRegisterImageAsync(badge.Id, badge.ImageUrl, false, font, 100));
                }
            }
            
            if (tasks.Count == 0)
                return true;
                
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        
        /// <summary>
        /// Builds a formatted message string with emotes and badges
        /// </summary>
        public async Task<string> BuildMessageAsync(ChatMessage message, EnhancedFontInfo font)
        {
            try
            {
                // Prepare images first
                if (!await PrepareImagesAsync(message, font))
                {
                    Logger.Log.Warn($"Failed to prepare some images for message: {message.Message}");
                }
                
                var sb = new StringBuilder();
                
                // Add badges and username for non-system messages
                if (!message.IsSystemMessage && message.Sender != null)
                {
                    // Add badges
                    foreach (var badge in message.Badges)
                    {
                        if (font.TryGetCharacter(badge.Id, out var character))
                        {
                            sb.Append(char.ConvertFromUtf32((int)character));
                            sb.Append(' ');
                        }
                    }
                    
                    // Add username with color
                    var userColor = GetUserColor(message.Sender);
                    var colorHex = ColorUtility.ToHtmlStringRGBA(userColor);
                    
                    if (message.IsActionMessage)
                    {
                        sb.Append($"<color=#{colorHex}><b>{EscapeRichText(message.Sender.DisplayName)}</b> ");
                    }
                    else
                    {
                        sb.Append($"<color=#{colorHex}><b>{EscapeRichText(message.Sender.DisplayName)}</b></color>: ");
                    }
                }
                
                // Process message content
                var messageText = new StringBuilder(message.Message);
                
                // Replace emotes with their registered characters
                foreach (var emote in message.Emotes.OrderByDescending(e => e.StartIndex))
                {
                    if (font.TryGetCharacter(emote.Id, out var character))
                    {
                        // Handle different emote types
                        // Note: ChatCore v2 doesn't have Bits property on emotes
                        // This would need to be handled differently based on platform-specific data
                                if (emote.Id.StartsWith("Emoji_"))
                        {
                            // Emoji handling
                            var emojiCode = emote.Id.Replace("Emoji_", "");
                            var emojiChars = emojiCode.Split('-').Select(x => char.ConvertFromUtf32(Convert.ToInt32($"0x{x}", 16)));
                            var emojiString = string.Concat(emojiChars);
                            messageText.Replace(emojiString, char.ConvertFromUtf32((int)character));
                        }
                        else
                        {
                            // Regular emote
                            messageText.Replace(emote.Name, char.ConvertFromUtf32((int)character));
                        }
                    }
                }
                
                // Escape < character to prevent tag parsing
                messageText.Replace("<", "<\u2060");
                
                sb.Append(messageText);
                
                // Close color tag for action messages
                if (!message.IsSystemMessage && message.IsActionMessage)
                {
                    sb.Append("</color>");
                }
                
                // Apply gray color for system messages
                if (message.IsSystemMessage)
                {
                    return $"<color=#bbbbbbff>{sb}</color>";
                }
                
                return sb.ToString();
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"Error building message: {ex}");
                return message.Message;
            }
        }
        
        /// <summary>
        /// Loads and registers an image with the font
        /// </summary>
        private async Task<bool> LoadAndRegisterImageAsync(string id, string url, bool isAnimated, EnhancedFontInfo font, int forcedHeight)
        {
            try
            {
                // Use ChatImageProvider to handle the actual loading
                var imageProvider = ChatImageProvider.instance;
                if (imageProvider == null)
                {
                    Logger.Log.Error("ChatImageProvider instance is null");
                    return false;
                }
                
                var tcs = new TaskCompletionSource<bool>();
                
                // Start coroutine to load image
                MainThreadInvoker.Invoke(() =>
                {
                    SharedCoroutineStarter.Instance.StartCoroutine(
                        imageProvider.TryCacheSingleImage(
                            id, 
                            url, 
                            isAnimated,
                            (info) =>
                            {
                                if (info != null)
                                {
                                    bool registered = font.TryRegisterImageInfo(info, out _);
                                    if (!registered)
                                    {
                                        Logger.Log.Warn($"Failed to register image {id} with font");
                                    }
                                    tcs.SetResult(registered);
                                }
                                else
                                {
                                    Logger.Log.Warn($"Failed to load image {id} from {url}");
                                    tcs.SetResult(false);
                                }
                            },
                            forcedHeight: forcedHeight
                        )
                    );
                });
                
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Logger.Log.Error($"Error loading image {id}: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets or generates a color for a user
        /// </summary>
        private Color GetUserColor(IChatUser user)
        {
            if (user == null)
                return Color.white;
                
            // Check cache first
            if (_userColorCache.TryGetValue(user.Id, out var cachedColor))
                return cachedColor;
                
            Color color;
            
            // Try to parse user's color
            if (!string.IsNullOrEmpty(user.Color) && ColorUtility.TryParseHtmlString(user.Color, out color))
            {
                // Ensure minimum brightness
                Color.RGBToHSV(color, out var h, out var s, out var v);
                if (v < 0.85f)
                {
                    v = 0.85f;
                    color = Color.HSVToRGB(h, s, v);
                }
            }
            else
            {
                // Generate random color based on username
                // Use System.Random instead of Unity's Random
                var hash = user.DisplayName?.GetHashCode() ?? user.Id.GetHashCode();
                var rnd = new System.Random(hash);
                color = new Color(
                    (float)rnd.Next(0, 100000) / 100000f,
                    (float)rnd.Next(0, 100000) / 100000f,
                    (float)rnd.Next(0, 100000) / 100000f
                );
                
                // Ensure minimum brightness
                Color.RGBToHSV(color, out var h, out var s, out var v);
                if (v < 0.85f)
                {
                    v = 0.85f;
                    color = Color.HSVToRGB(h, s, v);
                }
            }
            
            _userColorCache[user.Id] = color;
            return color;
        }
        
        /// <summary>
        /// Escapes rich text special characters
        /// </summary>
        private string EscapeRichText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            return text.Replace("<", "<\u2060");
        }
    }
}