using Game.Core.Domain;

namespace Game.Core.Items;

/// <summary>Rarity only tints the icon background. Values are 0–1 sRGB.</summary>
public static class CombatItemRarityColors
{
    public static (double Red, double Green, double Blue) ResolveBackgroundRgb(CombatItemRarity rarity) =>
        rarity switch
        {
            CombatItemRarity.Rare => (0.25, 0.45, 0.85),
            CombatItemRarity.Epic => (0.55, 0.25, 0.75),
            CombatItemRarity.Legendary => (0.95, 0.65, 0.15),
            _ => (0.55, 0.55, 0.55),
        };
}
