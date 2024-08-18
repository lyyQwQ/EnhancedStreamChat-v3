using ChatCore.Interfaces;
using ChatCore.Models;
using ChatCore.Models.Bilibili;
using ChatCore.Models.Twitch;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static UnityEngine.RectTransform;

namespace EnhancedStreamChat.Chat
{
    public class ChatMessageBuilder
    {
        public static ObjectMemoryPool<ConcurrentStack<EnhancedImageInfo>> ImageStackPool { get; }

        static ChatMessageBuilder()
        {
            ImageStackPool = new ObjectMemoryPool<ConcurrentStack<EnhancedImageInfo>>(5, null, null, null, f => f.Clear());
        }


        /// <summary>
        /// This function *blocks* the calling thread, and caches all the images required to display the message, then registers them with the provided font.
        /// </summary>
        /// <param name="msg">The chat message to get images from</param>
        /// <param name="font">The font to register these images to</param>
        public static bool PrepareImages(IChatMessage msg, EnhancedFontInfo font)
        {
            Logger.Debug($"Preparing images for message: {msg.Message}");
            var tasks = new List<Task<EnhancedImageInfo>>();
            var pendingEmoteDownloads = new HashSet<string>();

            foreach (var emote in msg.Emotes) {
                if (string.IsNullOrEmpty(emote.Id) || pendingEmoteDownloads.Contains(emote.Id)) {
                    Logger.Warn($"Emote {emote.Name} was missing from the emote dict! The request to {emote.Uri} may have timed out?");
                    continue;
                }
                if (!font.CharacterLookupTable.ContainsKey(emote.Id)) {
                    Logger.Info($"characterLookupTable not contains emote {emote.Id}, characterLookupTable: {font.CharacterLookupTable}");
                    pendingEmoteDownloads.Add(emote.Id);
                    var tcs = new TaskCompletionSource<EnhancedImageInfo>();
                    switch (emote.Type) {
                        case EmoteType.SingleImage:
                            Logger.Debug("[ChatMessageBuilder] | [PrepareImages] | [SingleImage] | Emote: ID: " + emote.Id + " Uri: " + emote.Uri + " IsAnimated: " + emote.IsAnimated);
                            var IsAnimated = emote.IsAnimated;
                            switch (Path.GetExtension(emote.Uri)) {
                                case ".jpg":
                                case ".png":
                                case ".bmp":
                                    IsAnimated = false;
                                    break;
                                case ".gif":
                                    IsAnimated = true;
                                    break;
                            }
                            // SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.TryCacheSingleImage(emote.Id, emote.Uri, IsAnimated, (info) =>
                            Logger.Debug("[ChatMessageBuilder] | [PrepareImages] | [SingleImage] | start cache image Emote: ID:" + emote.Id + " Uri: " + emote.Uri);
                            CoroutineRunner.Instance.StartCoroutine(ChatImageProvider.instance.TryCacheSingleImage(emote.Id, emote.Uri, IsAnimated, (info) =>
                            {
                                Logger.Debug($"try cache image Emote: ID: {emote.Id}, Uri: {emote.Uri}, IsAnimated: {IsAnimated}, info: {info}");
                                if (info != null) {
                                    if (!font.TryRegisterImageInfo(info, out var character)) {
                                        Logger.Warn($"Failed to register emote \"{emote.Id}\" in font {font.Font.name}.");
                                    }
                                    Logger.Debug($"register emote \"{emote.Id}\" in font {font.Font.name}, character: {character}, character: {char.ConvertFromUtf32((int)character)}, character int: {(int)character}, info: {info}");
                                }
                                tcs.SetResult(info);
                            }, forcedHeight: (int)Math.Ceiling(ChatConfig.instance.FontSize * 15)));
                            break;
                        case EmoteType.SpriteSheet:
                            Logger.Debug("[ChatMessageBuilder] | [PrepareImages] | [SpriteSheet] | start cache SpriteSheet Emote: ID: " + emote.Id + " Uri: " + emote.Uri);
                            // SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.TryCacheSpriteSheetImage(emote.Id, emote.Uri, emote.UVs, (info) =>
                            CoroutineRunner.Instance.StartCoroutine(ChatImageProvider.instance.TryCacheSpriteSheetImage(emote.Id, emote.Uri, emote.UVs, (info) =>
                            {
                                Logger.Debug($"try cache SpriteSheet Emote: ID: {emote.Id}, Uri: {emote.Uri}, UVs: {emote.UVs}");
                                if (info != null) {
                                    if (!font.TryRegisterImageInfo(info, out var character)) {
                                        Logger.Warn($"Failed to register emote \"{emote.Id}\" in font {font.Font.name}.");
                                    }
                                    Logger.Debug($"register emote \"{emote.Id}\" in font {font.Font.name}, character: {character}, character: {char.ConvertFromUtf32((int)character)}, character int: {(int)character}, info: {info}");
                                }
                                tcs.SetResult(info);
                            }, forcedHeight: 110));
                            break;
                        default:
                            tcs.SetResult(null);
                            Logger.Warn($"Unknown emote type {emote.Type} for emote {emote.Name}!");
                            break;
                    }
                    tasks.Add(tcs.Task);
                }
            }

            Logger.Info($"Preparing badges for message: {msg.Message}");
            foreach (var badge in msg.Sender.Badges) {
                if (string.IsNullOrEmpty(badge.Id) || pendingEmoteDownloads.Contains(badge.Id)) {
                    Logger.Warn($"Badge {badge.Name} was missing from the badge dict! The request to {badge.Uri} may have timed out?");
                    continue;
                }

                Logger.Debug("Badges: ID: " + badge.Id + " NAME: " + badge.Name + " URL: " + badge.Uri);
                if (!font.CharacterLookupTable.ContainsKey(badge.Id)) {
                    Logger.Debug($"characterLookupTable not contains badge {badge.Id}, characterLookupTable: {font.CharacterLookupTable}");
                    pendingEmoteDownloads.Add(badge.Id);
                    var tcs = new TaskCompletionSource<EnhancedImageInfo>();
                    // SharedCoroutineStarter.instance.StartCoroutine(ChatImageProvider.instance.TryCacheSingleImage(badge.Id, badge.Uri, false, (info) =>
                    CoroutineRunner.Instance.StartCoroutine(ChatImageProvider.instance.TryCacheSingleImage(badge.Id, badge.Uri, false, (info) =>
                    {
                        Logger.Debug($"try cache image Badge: ID: {badge.Id}, Uri: {badge.Uri}, info: {info}");
                        if (info != null) {
                            if (!font.TryRegisterImageInfo(info, out var character)) {
                                Logger.Warn($"Failed to register badge \"{badge.Id}\" in font {font.Font.name}.");
                            }
                            Logger.Debug($"register badge \"{badge.Id}\" in font {font.Font.name}, character: {character}, character: {char.ConvertFromUtf32((int)character)}, character int: {(int)character}, info: {info}");
                        }
                        tcs.SetResult(info);
                    }, forcedHeight: (int)Math.Ceiling(ChatConfig.instance.FontSize * 15)));
                    tasks.Add(tcs.Task);
                }
            }

            // Wait on all the resources to be ready
            return Task.WaitAll(tasks.ToArray(), 15000);
        }

        public static Task<string> BuildMessage(IChatMessage msg, EnhancedFontInfo font) => Task.Run(() =>
        {
            try {
                Logger.Debug($"Building message: {msg.Message}");

                if (!PrepareImages(msg, font)) {
                    Logger.Warn($"Failed to prepare some/all images for msg \"{msg.Message}\"!");
                    //return msg.Message;
                }
                Logger.Debug($"Images prepared for message: {msg.Message}");

                var badges = ImageStackPool.Alloc();
                try {
                    foreach (var badge in msg.Sender.Badges)
                    {
                        if (badge != null && !string.IsNullOrEmpty(badge.Id))
                        {
                            Logger.Debug("Badges: ID: " + badge.Id + " NAME: " + badge.Name + " URL: " + badge.Uri);
                            if (!ChatImageProvider.instance.CachedImageInfo.TryGetValue(badge.Id, out var badgeInfo))
                            {
                                Logger.Warn($"Failed to find cached image info for badge \"{badge.Id}\"!");
                                continue;
                            }
                            badges.Push(badgeInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"An exception occurred in ChatMessageBuilder while parsing badges. Msg: \"{msg.Message}\". {ex.ToString()}");
                }

                var sb = new StringBuilder(msg.Message); // Replace all instances of < with a zero-width non-breaking character
                Logger.Debug($"Message: {msg.Message}");
                // Escape all html tags in the message
                sb.Replace("<", "<\u2060");
                Logger.Debug($"Message sb: {sb.ToString()}");
                
                try{
                    Logger.Debug("Phase emotes: " + sb.ToString() + ", got " + msg.Emotes.Length + " Emote(s).");
                    foreach (var emote in msg.Emotes)
                    {
                        if (!ChatImageProvider.instance.CachedImageInfo.TryGetValue(emote.Id, out var replace))
                        {
                            Logger.Warn($"Emote {emote.Name} was missing from the emote dict! The request to {emote.Uri} may have timed out?");
                            continue;
                        }
                        Logger.Debug($"Emote: {emote.Name}, StartIndex: {emote.StartIndex}, EndIndex: {emote.EndIndex}, Len: {sb.Length}");
                        if (!font.TryGetCharacter(replace.ImageId, out var character))
                        {
                            Logger.Warn($"Emote {emote.Name} was missing from the character dict! Font hay have run out of usable characters.");
                            continue;
                        }

                        /* Logger.Debug("Try to show emotes: " + emote.Name.ToString());*/
                        try
                        {
                             //Replace emotes by index, in reverse order (msg.Emotes is sorted by emote.StartIndex in descending order)
                            if (msg is BilibiliChatMessage)
                            {
                                Logger.Debug("Emote: ID: " + emote.Id + " NAME: " + emote.Name + " URL: " + emote.Uri);
                                // todo 图片还有问题，暂时注释掉
                                // sb.Replace(emote.Name, emote switch
                                // {
                                //     BilibiliChatEmote b when true => char.ConvertFromUtf32((int)character),
                                //     _ => char.ConvertFromUtf32((int)character)
                                // },
                                // emote.StartIndex, emote.EndIndex - emote.StartIndex);
                                Logger.Debug("Replace " + emote.Name.ToString() + " ==> " + sb.ToString());
                                // 尝试在font的characterLookupTable中找到对应的character 输出找没找到
                                if (ESCFontManager.instance.MainFont.characterLookupTable.TryGetValue(character, out var tmpCharacter))
                                {
                                    Logger.Debug($"Font characterLookupTable contains character: {tmpCharacter}, tmpCharacter: {tmpCharacter.unicode}");
                                }
                                else
                                {
                                    Logger.Warn("Font characterLookupTable not contains character: " + char.ConvertFromUtf32((int)character));
                                }
                            }
                            else
                            {
                                sb.Replace(emote.Name, emote switch
                                {
                                    TwitchEmote t when t.Bits > 0 => $"{char.ConvertFromUtf32((int)character)}\u00A0<color={t.Color}><size=77%><b>{t.Bits}\u00A0</b></size></color>",
                                    _ => char.ConvertFromUtf32((int)character)
                                },
                                emote.StartIndex, emote.EndIndex - emote.StartIndex + 1);
                                Logger.Debug("Replace " + emote.Name.ToString() + " ==> " + sb.ToString());
                                // 尝试在font的characterLookupTable中找到对应的character 输出找没找到
                                if (ESCFontManager.instance.MainFont.characterLookupTable.TryGetValue(character, out var tmpCharacter))
                                {
                                    Logger.Debug($"Font characterLookupTable contains character: {tmpCharacter.ToString()}, tmpCharacter: {tmpCharacter.unicode}");
                                }
                                else
                                {
                                    Logger.Warn("Font characterLookupTable not contains character: " + char.ConvertFromUtf32((int)character));
                                }

                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"An unknown error occurred while trying to swap emote {emote.Name} into string of length {sb.Length} at location ({emote.StartIndex}, {emote.EndIndex})\r\n{ex}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"An exception occurred in ChatMessageBuilder while parsing badges. Msg: \"{msg.Message}\". {ex.ToString()}");
                }

                if (msg.IsSystemMessage) {
                    // System messages get a grayish color to differenciate them from normal messages in chat, and do not receive a username/badge prefix
                    sb.Insert(0, $"<color=#bbbbbbbb>");
                    sb.Append("</color>");
                }
                else {
                    var nameColorCode = msg.Sender.Color;
                    Logger.Debug(nameColorCode);
                    if (ColorUtility.TryParseHtmlString(msg.Sender.Color.Substring(0, 7), out var nameColor)) {
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
                        sb.Insert(0, $"<color={nameColorCode}><b>{msg.Sender.DisplayName}</b> ");
                        sb.Append("</color>");
                        Logger.Debug("Action message: " + sb.ToString());
                    }
                    else {
                        // Insert username w/ color
                        sb.Insert(0, $"<color={nameColorCode}><b>{msg.Sender.DisplayName}</b></color>: ");
                        Logger.Debug("Normal message: " + sb.ToString());
                    }

                    try {
                        Logger.Debug("Badges: " + msg.Sender.Badges.Length + " Badge(s).");
                        for (var i = 0; i < msg.Sender.Badges.Length; i++)
                        {
                            // Insert user badges at the beginning of the string in reverse order
                            // if (badges.TryPop(out var badge) && font.TryGetCharacter(badge.ImageId, out var character)) {
                            if (badges.TryPop(out var badge))
                            {
                                if (font.TryGetCharacter(badge.ImageId, out var character))
                                {
                                    // sb.Insert(0, $"{char.ConvertFromUtf32((int)character)} "); //todo 图片显示有问题，暂时注释掉
                                    Logger.Debug("Badge: " + sb.ToString());
                                }
                                /*if (msg is BilibiliChatMessage)
                                {
                                }
                                else if (font.TryGetCharacter(badge.ImageId, out var character)) {
                                    sb.Insert(0, $"{char.ConvertFromUtf32((int)character)} ");
                                }*/
                            }
                        }
                        Logger.Debug("Badges: " + sb.ToString());
                    } catch (Exception ex)
                    {
                        Logger.Error($"An exception occurred in ChatMessageBuilder while replace emotes. Msg: \"{msg.Message}\". {ex.ToString()}");
                    }
                    ImageStackPool.Free(badges);
                }
                // 在开头插一个a，防止textMeshPro的textinfo计算错误
                // sb.Insert(0, "aaaaaa ");

                Logger.Debug("Final message: " + sb.ToString());
                return sb.ToString();
            }
            catch (Exception ex) {
                Logger.Error($"An exception occurred in ChatMessageBuilder while parsing msg with {msg.Emotes.Length} emotes. Msg: \"{msg.Message}\". {ex.ToString()}");
            }
            return msg.Message;
        });
    }
}
