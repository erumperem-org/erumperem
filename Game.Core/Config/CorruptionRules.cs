namespace Game.Core.Config;

/// <summary>
/// Thresholds for world corruption (percentage-style magnitude). Tier boundaries are inclusive at the lower bound.
/// </summary>
public static class CorruptionRules
{
    public const double MinCorruptionValue = 0;

    public const double Tier0UpperInclusive = 32;
    public const double Tier1UpperInclusive = 65;
    public const double Tier2UpperInclusive = 98;
    public const double Tier3UpperInclusive = 198;

    /// <summary>When <c>corruptionCost</c> is omitted from skill JSON.</summary>
    public const double DefaultSkillCorruptionCost = 1;

    /// <summary>
    /// Design baseline for enemy critical strike multiplier vs players before tier-specific modifiers (matches legacy 150%).
    /// </summary>
    public const double BaseCriticalStrikeDamageMultiplier = 1.5;
}
