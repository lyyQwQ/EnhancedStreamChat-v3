using System.Drawing;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public class GraphicUtils
    {
        public static Texture2D? LoadTextureRaw(byte[]? file)
        {
            if (file == null || file.Length == 0) {
                return null;
            }

            var tex2D = new Texture2D(2, 2);
            return tex2D.LoadImage(file) ? tex2D : null;
        }

        public static Texture2D? LoadTextureFromFile(string filePath) => File.Exists(filePath) ? LoadTextureRaw(File.ReadAllBytes(filePath)) : null;

        public static Texture2D? LoadTextureFromResources(string resourcePath) => LoadTextureRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath));

        public static Sprite? LoadSpriteRaw(byte[] image, float pixelsPerUnit = 100.0f) => LoadSpriteFromTexture(LoadTextureRaw(image), pixelsPerUnit);

        public static Sprite? LoadSpriteFromTexture(Texture2D? spriteTexture, float pixelsPerUnit = 100.0f) => spriteTexture != null ? Sprite.Create(spriteTexture, new Rect(0, 0, spriteTexture.width, spriteTexture.height), new Vector2(0, 0), pixelsPerUnit) : null;

        public static Sprite? LoadSpriteFromFile(string filePath, float pixelsPerUnit = 100.0f) => LoadSpriteFromTexture(LoadTextureFromFile(filePath), pixelsPerUnit);

        public static Sprite? LoadSpriteFromResources(string resourcePath, float pixelsPerUnit = 100.0f) => LoadSpriteRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath), pixelsPerUnit);

        public static byte[]? GetResource(Assembly asm, string resourceName)
        {
            var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) {
                return null;
            }

            var data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
            return data;
        }

        public static Image ByteArrayToImage(byte[] byteArrayIn) => Image.FromStream(new MemoryStream(byteArrayIn));
    }
}