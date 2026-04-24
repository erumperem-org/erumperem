using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>Cor "aleatória" mas estável por <paramref name="skillId"/> (mesma skill = mesma cor em todas as sessões).</summary>
    public static class SkillUiColorPalette
    {
        public static Color GetColorForSkillId(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return new Color(0.55f, 0.55f, 0.6f, 1f);
            }

            var hash = Sha256ToUInt(skillId);
            var hue = (hash % 10000) / 10000f;
            var rgb = Color.HSVToRGB(hue, 0.55f, 0.95f);
            rgb.a = 1f;
            return rgb;
        }

        private static uint Sha256ToUInt(string text)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToUInt32(bytes, 0);
            }
        }
    }
}
