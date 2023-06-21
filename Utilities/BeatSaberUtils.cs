using BeatSaberMarkupLanguage;
using System.Linq;
using TMPro;
using UnityEngine;

namespace EnhancedStreamChat.Utilities
{
    public static class BeatSaberUtils
    {
        private static Material _noGlow;
        public static Material UINoGlowMaterial => _noGlow ??= Resources.FindObjectsOfTypeAll<Material>().Where(m => m.name == "UINoGlow").FirstOrDefault();

        private static Shader _tmpNoGlowFontShader;
        public static Shader TMPNoGlowFontShader => _tmpNoGlowFontShader ??= BeatSaberUI.MainTextFont == null ? null : BeatSaberUI.MainTextFont.material.shader;

        // DaNike to the rescue 
        public static bool TryGetTMPFontByFamily(string family, out TMP_FontAsset font)
        {
            if (FontManager.TryGetTMPFontByFamily(family, out font)) {
                font.material.shader = TMPNoGlowFontShader;
                return true;
            }

            return false;
        }

        public enum TextLayerVisibility : int
        {
            ThirdPerson = 3,
            Floor = 4, // Called "Water" ingame
            FirstPerson = 6,
            UI = 5,
            Notes = 8,
            Debris = 9,
            Avatar = 10,
            Walls = 11,
            Sabers = 12,
            CutParticles = 16,
            CustomNotes = 24,
            WallTextures = 25,
            PlayerPlattform = 28
        }

        public static TextLayerVisibility textLayerVisibilityReverser(int id)
        {
            switch (id)
            {
                case 3: return TextLayerVisibility.ThirdPerson;
                case 4: return TextLayerVisibility.Floor;
                case 5: return TextLayerVisibility.UI;
                case 6: return TextLayerVisibility.FirstPerson;
                case 8: return TextLayerVisibility.Notes;
                case 9: return TextLayerVisibility.Debris;
                case 10: return TextLayerVisibility.Avatar;
                case 11: return TextLayerVisibility.WallTextures;
                case 12: return TextLayerVisibility.Sabers;
                case 16: return TextLayerVisibility.CutParticles;
                case 24: return TextLayerVisibility.CustomNotes;
                case 25: return TextLayerVisibility.Walls;
                case 28: return TextLayerVisibility.PlayerPlattform;
                default: return TextLayerVisibility.UI;
            }
        }

        public static string textLayerVisibilitylocalizer(int id)
        {
            switch (id)
            {
                case 3: return "第三人称";
                case 4: return "地面";
                case 5: return "UI";
                case 6: return "第一人称";
                case 8: return "方块";
                case 9: return "碎片";
                case 10: return "人物形象";
                case 11: return "墙壁纹理";
                case 12: return "光剑";
                case 16: return "粒子";
                case 24: return "自定义方块";
                case 25: return "墙壁";
                case 28: return "玩家平台";
                default: return "UI";
            }
        }
    }
}