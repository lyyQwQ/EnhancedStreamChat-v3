using BeatSaberMarkupLanguage;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public static class BeatSaberUtils
    {
        private static Material s_noGlow;
        public static Material UINoGlowMaterial => s_noGlow ??= Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").FirstOrDefault();

        private static Shader s_tmpNoGlowFontShader;
#if BS_1423
        public static Shader TMPNoGlowFontShader => s_tmpNoGlowFontShader ??= BeatSaberUI.MainUIFontMaterial != null
            ? BeatSaberUI.MainUIFontMaterial.shader
            : BeatSaberUI.MainFlatUIFontMaterial != null
                ? BeatSaberUI.MainFlatUIFontMaterial.shader
                : null;
#else
        public static Shader TMPNoGlowFontShader => s_tmpNoGlowFontShader ??= !BeatSaberUI.MainTextFont ? null : GetTMPFontMaterial(BeatSaberUI.MainTextFont)?.shader;
#endif

        public static Material GetTMPFontMaterial(TMP_FontAsset font)
        {
            if (!font) {
                return null;
            }

            try {
                var material = font.material;
                return material;
            }
            catch {
                return null;
            }
        }

        public static void ApplyShaderToTMPFont(TMP_FontAsset font, Shader shader)
        {
            var material = GetTMPFontMaterial(font);
            if (material == null || shader == null || material.shader == shader) {
                return;
            }

            material.shader = shader;
        }

        public static Material EnsureTMPFontMaterial(TMP_FontAsset font)
        {
            var material = GetTMPFontMaterial(font);
            if (material != null) {
                return material;
            }

#if BS_1423
            var referenceMaterial = BeatSaberUI.MainUIFontMaterial ?? BeatSaberUI.MainFlatUIFontMaterial;
#else
            var referenceMaterial = GetTMPFontMaterial(BeatSaberUI.MainTextFont);
#endif
            var mainTexture = font == null
                ? null
                : font.atlasTexture ?? font.atlasTextures?.FirstOrDefault(texture => texture != null);
            if (font == null || referenceMaterial == null || mainTexture == null) {
                return null;
            }

            try {
                material = new Material(referenceMaterial)
                {
                    name = font.name + " Material"
                };
                material.SetTexture("_MainTex", mainTexture);
                font.material = material;
                return material;
            }
            catch {
                return null;
            }
        }

        // DaNike to the rescue
        public static bool TryGetTMPFontByFamily(string family, out TMP_FontAsset font)
        {
            if (FontManager.TryGetTMPFontByFamily(family, out font)) {
                ApplyShaderToTMPFont(font, TMPNoGlowFontShader);
                return true;
            }

            return false;
        }
    }
}
