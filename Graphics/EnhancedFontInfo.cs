using System.Collections.Concurrent;
using TMPro;
using UnityEngine.TextCore;

namespace EnhancedStreamChat.Graphics
{
    public class EnhancedFontInfo
    {
        public TMP_FontAsset Font { get; }
        public uint NextReplaceChar { get; private set; } = 0xe000;
        public ConcurrentDictionary<string, uint> CharacterLookupTable { get; } = new ConcurrentDictionary<string, uint>();
        public ConcurrentDictionary<uint, EnhancedImageInfo> ImageInfoLookupTable { get; } = new ConcurrentDictionary<uint, EnhancedImageInfo>();
        private readonly object _lock = new object();

        private const uint UNICODE_USER_FIRST_AREA_MINIMUM_VALUE = 0x0000E000;
        private const uint UNICODE_USER_FIRST_AREA_MAXIMUM_VALUE = 0x0000F8FF;

        private const uint UNICODE_USER_SECOND_AREA_MINIMUM_VALUE = 0x000F0000;
        private const uint UNICODE_USER_SECOND_AREA_MAXIMUM_VALUE = 0x000FFFFD;

        private const uint UNICODE_USER_THARD_AREA_MINIMUM_VALUE = 0x00100000;
        private const uint UNICODE_USER_THARD_AREA_MAXIMUM_VALUE = 0x0010FFFD;

        public EnhancedFontInfo(TMP_FontAsset font)
        {
            this.Font = font;
        }

        public uint GetNextReplaceChar()
        {
            var ret = this.NextReplaceChar++;
            // If we used up all the Private Use Area characters, move onto Supplementary Private Use Area-A
            if (this.NextReplaceChar < UNICODE_USER_FIRST_AREA_MINIMUM_VALUE) {
                Logger.Warn("Font is out of characters! Switching to overflow range.");
                this.NextReplaceChar = UNICODE_USER_FIRST_AREA_MINIMUM_VALUE;
            }
            if (UNICODE_USER_FIRST_AREA_MAXIMUM_VALUE < this.NextReplaceChar && this.NextReplaceChar < UNICODE_USER_SECOND_AREA_MINIMUM_VALUE) {
                Logger.Warn("Font is out of characters! Switching to overflow range.");
                this.NextReplaceChar = UNICODE_USER_SECOND_AREA_MINIMUM_VALUE;
            }
            if (UNICODE_USER_SECOND_AREA_MAXIMUM_VALUE < this.NextReplaceChar && this.NextReplaceChar < UNICODE_USER_THARD_AREA_MINIMUM_VALUE) {
                Logger.Warn("Font is out of characters! Switching to overflow range.");
                this.NextReplaceChar = UNICODE_USER_THARD_AREA_MINIMUM_VALUE;
            }
            if (UNICODE_USER_THARD_AREA_MAXIMUM_VALUE < this.NextReplaceChar) {
                Logger.Warn("Font is out of characters! Switching to overflow range.");
                this.NextReplaceChar = UNICODE_USER_FIRST_AREA_MINIMUM_VALUE;
            }
            return ret;
        }

        public bool TryGetCharacter(string id, out uint character) => this.CharacterLookupTable.TryGetValue(id, out character);

        public bool TryGetImageInfo(uint character, out EnhancedImageInfo imageInfo) => this.ImageInfoLookupTable.TryGetValue(character, out imageInfo);

        public bool TryRegisterImageInfo(EnhancedImageInfo imageInfo, out uint replaceCharacter)
        {
            Logger.Info($"Registering image info for {imageInfo.ImageId}, sprite: {imageInfo.Sprite}, width: {imageInfo.Width}, height: {imageInfo.Height}");
            if (!this.CharacterLookupTable.ContainsKey(imageInfo.ImageId)) {
                Logger.Debug("Character not found, registering...");
                uint next;
                do {
                    next = this.GetNextReplaceChar();
                    Logger.Debug($"Trying character {next:X}");
                }
                while (this.Font.characterLookupTable.ContainsKey(next));
                Logger.Debug($"Character {next:X} is available, registering...");
                this.Font.characterLookupTable.Add(next, new TMP_Character(next, new Glyph(next, new GlyphMetrics(0, 0, 0, 0, imageInfo.Width), new GlyphRect(0, 0, 0, 0))));
                this.CharacterLookupTable.TryAdd(imageInfo.ImageId, next);
                this.ImageInfoLookupTable.TryAdd(next, imageInfo);
                Logger.Debug($"Registered image info for {imageInfo.ImageId} at character {next:X}, font name: {this.Font.name}");
                replaceCharacter = next;
                Logger.Debug($"Returning character {replaceCharacter:X}");
                return true;
            }
            replaceCharacter = 0;
            Logger.Warn($"Character {imageInfo.ImageId} is already registered!");
            return false;
        }

        public bool TryUnregisterImageInfo(string id, out uint unregisteredCharacter)
        {
            lock (this._lock) {
                if (!this.CharacterLookupTable.TryGetValue(id, out var c)) {
                    unregisteredCharacter = 0;
                    return false;
                }
                if (this.Font.characterLookupTable.ContainsKey(c)) {
                    this.Font.characterLookupTable.Remove(c);
                }
                this.CharacterLookupTable.TryRemove(id, out unregisteredCharacter);
                return this.ImageInfoLookupTable.TryRemove(unregisteredCharacter, out var unregisteredImageInfo);
            }
        }
    }
}
