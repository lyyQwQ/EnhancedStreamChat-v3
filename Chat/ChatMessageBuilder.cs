using CatCore.Models.Shared;
using CatCore.Models.Twitch.Media;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Interfaces;
using EnhancedStreamChat.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace EnhancedStreamChat.Chat
{
    public class ChatMessageBuilder
    {
        private readonly ChatImageProvider _chatImageProvider;
        private static readonly ConcurrentDictionary<string, Color> s_senderColor = new ConcurrentDictionary<string, Color>();
        private const string CompactPrefixSeparator = "<space=-0.14em>";
        private const string CompactPrefixIconTighten = "<space=-0.10em>";
        private readonly System.Random _random = new System.Random(Environment.TickCount);

        private static string TruncateForTask14Debug(string text, int max = 180)
        {
            if (string.IsNullOrEmpty(text) || max <= 0) {
                return string.Empty;
            }
            if (text.Length <= max) {
                return text;
            }
            return text.Substring(0, max) + "...(truncated)";
        }

        public ChatMessageBuilder(ChatImageProvider chatImageProvider)
        {
            this._chatImageProvider = chatImageProvider;
        }

        /// <summary>
        /// This function *blocks* the calling thread, and caches all the images required to display the message, then registers them with the provided font.
        /// </summary>
        /// <param name="msg">The chat message to get images from</param>
        /// <param name="font">The font to register these images to</param>

        public async Task<bool> PrepareImages(IESCChatMessage msg, EnhancedFontInfo font)
        {
            var tasks = new List<Task<EnhancedImageInfo>>();
            var pendingEmoteDownloads = new HashSet<string>();
            var metadataImages = this.GetMetadataImages(msg);
            var metadataRegisteredCount = 0;

            foreach (var emote in msg.Emotes) {
                if (string.IsNullOrEmpty(emote.Id) || pendingEmoteDownloads.Contains(emote.Id)) {
                    continue;
                }
                if (!font.CharacterLookupTable.ContainsKey(emote.Id)) {
                    await MainThreadInvoker.Invoke(() =>
                    {
                        _ = pendingEmoteDownloads.Add(emote.Id);
                        var tcs = new TaskCompletionSource<EnhancedImageInfo>();
                        _ = SharedCoroutineStarter.Instance.StartCoroutine(this._chatImageProvider.TryCacheSingleImage(emote.Id, emote.Url, emote.Animated ? ChatImageProvider.ESCAnimationType.GIF : ChatImageProvider.ESCAnimationType.MAYBE_GIF, (info) =>
                        {
                            if (info == null || !font.TryRegisterImageInfo(info, out var character)) {
                                Logger.Warn($"Failed to register emote \"{emote.Id}\" in font {font.Font.name}.");
                            }
                            tcs.SetResult(info);
                        }, forcedHeight: 110));
                        tasks.Add(tcs.Task);
                    });
                }
            }

            foreach (var metadataImage in metadataImages) {
                if (string.IsNullOrEmpty(metadataImage.Id) || string.IsNullOrEmpty(metadataImage.Url) || pendingEmoteDownloads.Contains(metadataImage.Id)) {
                    continue;
                }
                if (!font.CharacterLookupTable.ContainsKey(metadataImage.Id)) {
                    await MainThreadInvoker.Invoke(() =>
                    {
                        _ = pendingEmoteDownloads.Add(metadataImage.Id);
                        var tcs = new TaskCompletionSource<EnhancedImageInfo>();
                        _ = SharedCoroutineStarter.Instance.StartCoroutine(this._chatImageProvider.TryCacheSingleImage(metadataImage.Id, metadataImage.Url,
                            metadataImage.Animated ? ChatImageProvider.ESCAnimationType.GIF : ChatImageProvider.ESCAnimationType.MAYBE_GIF, (info) =>
                            {
                                if (info != null && font.TryRegisterImageInfo(info, out var _)) {
                                    metadataRegisteredCount++;
                                }
                                else {
                                    Logger.Warn($"Failed to register metadata image \"{metadataImage.Id}\" in font {font.Font.name}.");
                                }
                                tcs.SetResult(info);
                            }, forcedHeight: metadataImage.ForcedHeight));
                        tasks.Add(tcs.Task);
                    });
                }
            }

            if (msg.Sender is IChatUserWithBadges userWithBadges) {
                foreach (var badge in userWithBadges.Badges) {
                    if (string.IsNullOrEmpty(badge.Id) || pendingEmoteDownloads.Contains(badge.Id)) {
                        continue;
                    }
                    if (!font.CharacterLookupTable.ContainsKey(badge.Id)) {
                        await MainThreadInvoker.Invoke(() =>
                        {
                            _ = pendingEmoteDownloads.Add(badge.Id);
                            var tcs = new TaskCompletionSource<EnhancedImageInfo>();
                            _ = SharedCoroutineStarter.Instance.StartCoroutine(this._chatImageProvider.TryCacheSingleImage(badge.Id, badge.Uri, ChatImageProvider.ESCAnimationType.NONE, (info) =>
                            {
                                if (info != null) {
                                    if (!font.TryRegisterImageInfo(info, out var character)) {
                                        Logger.Warn($"Failed to register badge \"{badge.Id}\" in font {font.Font.name}.");
                                    }
                                }
                                tcs.SetResult(info);
                            }, forcedHeight: 110));
                            tasks.Add(tcs.Task);
                        });
                    }
                }
            }
            // Wait on all the resources to be ready
            var result = await Task.WhenAll(tasks);
            if (metadataImages.Count > 0) {
                Logger.Debug($"BILI_METADATA_IMAGE_PREPARE msg={msg.Id} requested={metadataImages.Count} registered={metadataRegisteredCount}");
            }
            return result.All(x => x != null);
        }

        public async Task<string> BuildMessage(IESCChatMessage msg, EnhancedFontInfo font, BuildMessageTarget buildMessage, bool shouldPrepareImages = true)
        {
            try {
                if (shouldPrepareImages && !await this.PrepareImages(msg, font)) {
                    Logger.Warn($"Failed to prepare some/all images for msg \"{msg.Message}\"!");
                    //return msg.Message;
                }
                var badges = new Stack<EnhancedImageInfo>();
                var prefixImageIds = new List<string>();
                var avatarPrefixImageIds = new List<string>();
                if (msg.Sender is IChatUserWithBadges userWithBadges) {
                    foreach (var badge in userWithBadges.Badges) {
                        prefixImageIds.Add(badge.Id);
                    }
                }
                foreach (var metadataImage in this.GetMetadataImages(msg)) {
                    // Keep emotes out of prefix icons; avatar stays as the left-most prefix image.
                    if (string.Equals(metadataImage.Kind, "emote", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    if (string.Equals(metadataImage.Kind, "avatar", StringComparison.OrdinalIgnoreCase)) {
                        avatarPrefixImageIds.Add(metadataImage.Id);
                        continue;
                    }
                    prefixImageIds.Add(metadataImage.Id);
                }
                if (avatarPrefixImageIds.Count > 0) {
                    prefixImageIds.InsertRange(0, avatarPrefixImageIds);
                }
                foreach (var imageId in prefixImageIds) {
                    if (!this._chatImageProvider.CachedImageInfo.TryGetValue(imageId, out var badgeInfo)) {
                        Logger.Warn($"Failed to find cached image info for badge \"{imageId}\"!");
                        continue;
                    }
                    badges.Push(badgeInfo);
                }
                var isBilibiliMessage = msg.Metadata.TryGetValue("platform", out var platform) && platform == "bilibili";
                var sb = buildMessage == BuildMessageTarget.Main ? new StringBuilder(msg.Message) : new StringBuilder(msg.SubMessage); // Replace all instances of < with a zero-width non-breaking character
                var shouldTask14Log = isBilibiliMessage && buildMessage == BuildMessageTarget.Main;
                foreach (var emote in msg.Emotes) {
                    if (emote is TwitchEmote twitchEmote && 0 < twitchEmote.Bits) {
                        continue;
                    }
                    if (!this._chatImageProvider.CachedImageInfo.TryGetValue(emote.Id, out var replace)) {
                        Logger.Warn($"Emote {emote.Name} was missing from the emote dict! The request to {emote.Url} may have timed out?");
                        continue;
                    }
                    //Logger.Info($"replase id {replace.ImageId}");
                    //Logger.Info($"Emote: {emote.Name}, StartIndex: {emote.StartIndex}, EndIndex: {emote.EndIndex}, Len: {sb.Length}");
                    if (!font.TryGetCharacter(replace.ImageId, out var character)) {
                        Logger.Warn($"Emote {emote.Name} was missing from the character dict! Font hay have run out of usable characters.");
                        continue;
                    }
                    //Logger.Info($"target char {character}");
                    try {
                        if (isBilibiliMessage) {
                            _ = sb.Replace(emote.Name, char.ConvertFromUtf32((int)character));
                            continue;
                        }
                        // Replace emotes by index, in reverse order (msg.Emotes is sorted by emote.StartIndex in descending order)
                        if (Regex.IsMatch(emote.Id, "^Emoji_")) {
                            var charIndexText = Regex.Replace(emote.Id, "^Emoji_", "");
                            var emojiChars = charIndexText.Split('-').Select(x => char.ConvertFromUtf32(Convert.ToInt32($"0x{x}", 16)));
                            var emojiBuilder = new StringBuilder();
                            foreach (var emojiChar in emojiChars) {
                                _ = emojiBuilder.Append(emojiChar);
                            }
                            _ = sb.Replace(emojiBuilder.ToString(), char.ConvertFromUtf32((int)character));
                        }
                        else {
                            _ = sb.Replace(emote.Name, char.ConvertFromUtf32((int)character));
                        }
                    }
                    catch (Exception ex) {
                        Logger.Error($"An unknown error occurred while trying to swap emote {emote.Name} into string of length {sb.Length} at location ({emote.StartIndex}, {emote.EndIndex})\r\n{ex}");
                    }
                }
                _ = sb.Replace("<", "<\u2060");
                foreach (var emote in msg.Emotes) {
                    if (emote is not TwitchEmote twitchEmote || twitchEmote.Bits == 0) {
                        continue;
                    }
                    if (!this._chatImageProvider.CachedImageInfo.TryGetValue(emote.Id, out var replace)) {
                        Logger.Warn($"Emote {emote.Name} was missing from the emote dict! The request to {emote.Url} may have timed out?");
                        continue;
                    }
                    //Logger.Info($"replase id {replace.ImageId}");
                    //Logger.Info($"Emote: {emote.Name}, StartIndex: {emote.StartIndex}, EndIndex: {emote.EndIndex}, Len: {sb.Length}");
                    if (!font.TryGetCharacter(replace.ImageId, out var character)) {
                        Logger.Warn($"Emote {emote.Name} was missing from the character dict! Font hay have run out of usable characters.");
                        continue;
                    }
                    //Logger.Info($"target char {character}");
                    try {
                        // Replace emotes by index, in reverse order (msg.Emotes is sorted by emote.StartIndex in descending order)
                        _ = sb.Replace(emote.Name, $"{char.ConvertFromUtf32((int)character)}\u00A0<color={twitchEmote.Color}><size=77%><b>{twitchEmote.Bits}\u00A0</b></size></color>");
                    }
                    catch (Exception ex) {
                        Logger.Error($"An unknown error occurred while trying to swap emote {emote.Name} into string of length {sb.Length} at location ({emote.StartIndex}, {emote.EndIndex})\r\n{ex}");
                    }
                }
                if (buildMessage == BuildMessageTarget.Main && msg.IsSystemMessage) {
                    // System messages get a grayish color to differenciate them from normal messages in chat, and do not receive a username/badge prefix
                    _ = sb.Insert(0, $"<color=#bbbbbbff>");
                    _ = sb.Append("</color>");
                }
                else {
                    var displayNameForTask14 = msg.Sender?.DisplayName ?? string.Empty;
                    var containsMedalLink = displayNameForTask14.Contains("<link=medal_", StringComparison.Ordinal);
                    if (shouldTask14Log) {
                        #if BADGE_DEBUG
                        Logger.Info($"[TASK14DBG][ESC-BUILD] BeforeNameInsert msgId={msg.Id} displayName=\"{TruncateForTask14Debug(displayNameForTask14)}\" containsMedalLink={containsMedalLink}");
                        #endif
                        if (!containsMedalLink) {
                            #if BADGE_DEBUG
                            Logger.Warn($"[TASK14DBG][ESC-BUILD] msgId={msg.Id} 上游未提供 medal link in DisplayName");
                            #endif
                        }
                    }
                    var nameColorCode = msg.Sender?.Color;
                    if (ColorUtility.TryParseHtmlString(nameColorCode?.Substring(0, 7), out var nameColor)) {
                        if (nameColor == Color.white && !s_senderColor.TryGetValue(msg.Sender?.UserName, out nameColor)) {
                            nameColor = new Color((float)this._random.Next(0, 100000) / 100000, (float)this._random.Next(0, 100000) / 100000, (float)this._random.Next(0, 100000) / 100000);
                            _ = s_senderColor.TryAdd(msg.Sender?.UserName, nameColor);
                        }
                        Color.RGBToHSV(nameColor, out var h, out var s, out var v);
                        if (v < 0.85f) {
                            v = 0.85f;
                            nameColor = Color.HSVToRGB(h, s, v);
                        }
                        nameColorCode = ColorUtility.ToHtmlStringRGBA(nameColor);
                        nameColorCode = nameColorCode.Insert(0, "#");
                    }
                    if (msg.IsActionMessage) {
                        // Message becomes the color of their name if it's an action message
                        _ = sb.Insert(0, $"<color={nameColorCode}><b>{msg.Sender?.DisplayName}</b> ");
                        _ = sb.Append("</color>");
                    }
                    else {
                        // Insert username w/ color
                        _ = sb.Insert(0, $"<color={nameColorCode}><b>{msg.Sender?.DisplayName}</b></color>: ");
                    }
                    var parsedBadge = new HashSet<string>();
                    var insertedPrefixIcon = false;
                    while (badges.Count > 0) {
                        // Insert all prefix images (Twitch badges + metadata images)
                        var badge = badges.Pop();
                        if (badge == null || parsedBadge.Contains(badge.ImageId)) {
                            continue;
                        }
                        _ = parsedBadge.Add(badge.ImageId);
                        if (font.TryGetCharacter(badge.ImageId, out var character)) {
                            // Keep icon group compact; only the first inserted icon keeps a small separator before username.
                            _ = sb.Insert(0, insertedPrefixIcon
                                ? $"{char.ConvertFromUtf32((int)character)}{CompactPrefixIconTighten}"
                                : $"{char.ConvertFromUtf32((int)character)}{CompactPrefixSeparator}");
                            insertedPrefixIcon = true;
                        }
                        else {
                            Logger.Warn("Undefind badge");
                        }
                    }
                    if (shouldTask14Log) {
                        var builtText = sb.ToString();
                        var containsMedalLinkAfterInsert = builtText.Contains("<link=medal_", StringComparison.Ordinal);
                        #if BADGE_DEBUG
                        Logger.Info($"[TASK14DBG][ESC-BUILD] AfterNameInsert msgId={msg.Id} containsMedalLinkAfterInsert={containsMedalLinkAfterInsert} sbPrefix=\"{TruncateForTask14Debug(builtText, 220)}\"");
                        #endif
                    }
                }
                return sb.ToString();
            }
            catch (Exception ex) {
                Logger.Error($"An exception occurred in ChatMessageBuilder while parsing msg with {msg.Emotes.Count} emotes. Msg: \"{msg.Message}\". {ex}");
            }
            return msg.Message;
        }

        private List<MetadataImageResource> GetMetadataImages(IESCChatMessage msg)
        {
            var result = new List<MetadataImageResource>();
            if (msg?.Metadata == null) {
                return result;
            }

            // Format A (legacy / current CatCore):
            //   bili.richimage.count
            //   bili.richimage.{i}.(id|url|animated|height|kind)
            if (msg.Metadata.TryGetValue("bili.richimage.count", out var countText)
                && int.TryParse(countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                && count > 0) {
                for (var i = 0; i < count; i++) {
                    if (!msg.Metadata.TryGetValue($"bili.richimage.{i}.id", out var id)
                        || !msg.Metadata.TryGetValue($"bili.richimage.{i}.url", out var url)
                        || string.IsNullOrWhiteSpace(id)
                        || string.IsNullOrWhiteSpace(url)) {
                        continue;
                    }

                    var kind = msg.Metadata.TryGetValue($"bili.richimage.{i}.kind", out var kindText) ? kindText : "metadata";
                    var animated = msg.Metadata.TryGetValue($"bili.richimage.{i}.animated", out var animatedText) && animatedText == "1";
                    var forcedHeight = GetDefaultForcedHeight(kind);
                    if (msg.Metadata.TryGetValue($"bili.richimage.{i}.height", out var heightText)
                        && int.TryParse(heightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeight)
                        && parsedHeight > 0) {
                        forcedHeight = parsedHeight;
                    }
                    forcedHeight = ApplyBroadcasterMinHeight(id, forcedHeight);

                    result.Add(new MetadataImageResource(id, url, animated, forcedHeight, kind));
                }

                return result;
            }

            // Format B (kind-grouped):
            //   bili.richimage.{i}.{kind}.(id|url|animated|height)
            // where kind may be avatar|badge|tag|emote
            const int maxRichImageIndex = 32;
            var foundAny = false;
            for (var i = 0; i < maxRichImageIndex; i++) {
                var addedThisIndex = false;
                foreach (var kind in s_richImageKinds) {
                    var prefix = $"bili.richimage.{i}.{kind}.";
                    if (!msg.Metadata.TryGetValue(prefix + "id", out var id)
                        || !msg.Metadata.TryGetValue(prefix + "url", out var url)
                        || string.IsNullOrWhiteSpace(id)
                        || string.IsNullOrWhiteSpace(url)) {
                        continue;
                    }

                    var animated = msg.Metadata.TryGetValue(prefix + "animated", out var animatedText)
                        && (animatedText == "1" || string.Equals(animatedText, "true", StringComparison.OrdinalIgnoreCase));

                    var forcedHeight = GetDefaultForcedHeight(kind);
                    if (msg.Metadata.TryGetValue(prefix + "height", out var heightText)
                        && int.TryParse(heightText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHeight)
                        && parsedHeight > 0) {
                        forcedHeight = parsedHeight;
                    }
                    forcedHeight = ApplyBroadcasterMinHeight(id, forcedHeight);

                    result.Add(new MetadataImageResource(id, url, animated, forcedHeight, kind));
                    foundAny = true;
                    addedThisIndex = true;
                }

                // If we've started finding grouped metadata, stop scanning after the first empty index.
                if (foundAny && !addedThisIndex) {
                    break;
                }
            }

            return result;
        }

        private static readonly string[] s_richImageKinds = new[] { "avatar", "badge", "tag", "emote" };

        private static int GetDefaultForcedHeight(string kind)
        {
            if (string.Equals(kind, "avatar", StringComparison.OrdinalIgnoreCase)) {
                return 110;
            }
            if (string.Equals(kind, "tag", StringComparison.OrdinalIgnoreCase)) {
                return 88;
            }
            return 94;
        }

        private static int ApplyBroadcasterMinHeight(string id, int forcedHeight)
        {
            const int broadcasterMinHeight = 108;
            if (!string.IsNullOrEmpty(id)
                && id.IndexOf("_broadcaster_", StringComparison.OrdinalIgnoreCase) >= 0
                && forcedHeight < broadcasterMinHeight) {
                return broadcasterMinHeight;
            }

            return forcedHeight;
        }

        private readonly struct MetadataImageResource
        {
            public string Id { get; }
            public string Url { get; }
            public bool Animated { get; }
            public int ForcedHeight { get; }
            public string Kind { get; }

            public MetadataImageResource(string id, string url, bool animated, int forcedHeight, string kind)
            {
                Id = id;
                Url = url;
                Animated = animated;
                ForcedHeight = forcedHeight;
                Kind = kind;
            }
        }
    }
}
