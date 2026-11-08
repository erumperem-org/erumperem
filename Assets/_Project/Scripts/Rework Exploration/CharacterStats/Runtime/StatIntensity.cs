using UnityEngine;

namespace Core.CharacterStats
{
    /// <summary>
    /// Qualitative intensity used by the item design doc (itens_jogo.md):
    /// "+" light, "++" medium, "+++" strong, "++++" extreme, and the same
    /// ladder for negatives. The enum VALUE is the signed level, so the
    /// symbol is always derivable from it (see <see cref="StatIntensityExtensions"/>).
    /// Numbers behind each level live in <see cref="IntensityLibrary"/> —
    /// never in items — so rebalancing is a single-asset change.
    /// </summary>
    public enum StatIntensity
    {
        /// <summary>No change. Also the default of a freshly authored entry.</summary>
        None = 0,

        Minus4 = -4, // ----
        Minus3 = -3, // ---
        Minus2 = -2, // --
        Minus1 = -1, // -

        Plus1 = 1,   // +
        Plus2 = 2,   // ++
        Plus3 = 3,   // +++
        Plus4 = 4    // ++++
    }

    public static class StatIntensityExtensions
    {
        /// <summary>Plus2 → "++", Minus3 → "---", None → "".</summary>
        public static string ToSymbol(this StatIntensity intensity)
        {
            int level = (int)intensity;
            if (level == 0) return string.Empty;
            return new string(level > 0 ? '+' : '-', Mathf.Abs(level));
        }

        /// <summary>"+++" → Plus3, "--" → Minus2. Anything invalid (mixed signs, > 4 chars, other chars) → None.</summary>
        public static StatIntensity FromSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol) || symbol.Length > 4) return StatIntensity.None;

            char sign = symbol[0];
            if (sign != '+' && sign != '-') return StatIntensity.None;

            foreach (char c in symbol)
                if (c != sign) return StatIntensity.None;

            int level = symbol.Length * (sign == '+' ? 1 : -1);
            return (StatIntensity)level;
        }
    }
}
