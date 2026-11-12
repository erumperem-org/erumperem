using Game.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Config;

public sealed class CorruptionTierModifiers
{
    public required int Tier { get; init; }
    public required double PlayerDamageDealtMultiplier { get; init; }
    public required double PlayerDamageTakenMultiplier { get; init; }
    public required double PlayerCritBonus { get; init; }
    public required double EnemyCritBonusAgainstPlayer { get; init; }

    /// <summary>Extra multiplier applied to critical hit damage when an enemy critically strikes a player.</summary>
    public double EnemyCritDamageMultiplierAgainstPlayer { get; init; } = 1.0;

    /// <summary>Added to enemy hit chance.</summary>
    public double EnemyAccuracyBonus { get; init; }

    /// <summary>Global accuracy bonus (+10% per tier) applied to all skills for players and enemies.</summary>
    public double GlobalAccuracyBonus { get; init; }

    /// <summary>Global critical bonus (+5% per tier) applied to all skills for players and enemies.</summary>
    public double GlobalCritBonus { get; init; }

    /// <summary>When true, enemies have 100% guaranteed hits.</summary>
    public bool EnemiesGuaranteedHit { get; init; }

    /// <summary>When true, enemies always land critical hits.</summary>
    public bool EnemiesAlwaysCrit { get; init; }
}

public sealed class CombatBalanceConfig
{
    public required IReadOnlyList<CorruptionTierModifiers> CorruptionTiers { get; init; }
    public double ElementAdvantageMultiplier { get; init; } = 1.5;
    public double ElementDisadvantageMultiplier { get; init; } = 0.5;

    public const double DefaultExtraTurnChanceDecayPerConsecutiveUse = 0.20;

    /// <summary>
    /// Subtracted from extra-turn chance for each consecutive extra-turn skill the actor already used
    /// (Pistol Draw, Aim for the Head, Juggling, …). First use is unmodified.
    /// </summary>
    public double ExtraTurnChanceDecayPerConsecutiveUse { get; init; } = DefaultExtraTurnChanceDecayPerConsecutiveUse;

    public CorruptionTierModifiers GetTierModifiers(int corruptionTier)
    {
        return CorruptionTiers.First(tierModifiers => tierModifiers.Tier == corruptionTier);
    }

    public static CombatBalanceConfig CreateDefault()
    {
        return new CombatBalanceConfig
        {
            CorruptionTiers =
            [
                new CorruptionTierModifiers
                {
                    Tier = 0,
                    PlayerDamageDealtMultiplier = 1.00,
                    PlayerDamageTakenMultiplier = 1.00,
                    PlayerCritBonus = 0.00,
                    EnemyCritBonusAgainstPlayer = 0.00,
                    EnemyAccuracyBonus = 0.00,
                    GlobalAccuracyBonus = 0.00,
                    GlobalCritBonus = 0.00,
                    EnemiesGuaranteedHit = false,
                    EnemiesAlwaysCrit = false,
                },
                new CorruptionTierModifiers
                {
                    Tier = 1,
                    PlayerDamageDealtMultiplier = 1.00,
                    PlayerDamageTakenMultiplier = 1.00,
                    PlayerCritBonus = 0.05,
                    EnemyCritBonusAgainstPlayer = 0.05,
                    EnemyAccuracyBonus = 0.10,
                    GlobalAccuracyBonus = 0.10,
                    GlobalCritBonus = 0.05,
                    EnemiesGuaranteedHit = false,
                    EnemiesAlwaysCrit = false,
                },
                new CorruptionTierModifiers
                {
                    Tier = 2,
                    PlayerDamageDealtMultiplier = 1.00,
                    PlayerDamageTakenMultiplier = 1.00,
                    PlayerCritBonus = 0.10,
                    EnemyCritBonusAgainstPlayer = 0.10,
                    EnemyAccuracyBonus = 0.20,
                    GlobalAccuracyBonus = 0.20,
                    GlobalCritBonus = 0.10,
                    EnemiesGuaranteedHit = false,
                    EnemiesAlwaysCrit = false,
                },
                new CorruptionTierModifiers
                {
                    Tier = 3,
                    PlayerDamageDealtMultiplier = 1.00,
                    PlayerDamageTakenMultiplier = 1.00,
                    PlayerCritBonus = 0.15,
                    EnemyCritBonusAgainstPlayer = 0.15,
                    EnemyAccuracyBonus = 0.30,
                    GlobalAccuracyBonus = 0.30,
                    GlobalCritBonus = 0.15,
                    EnemiesGuaranteedHit = false,
                    EnemiesAlwaysCrit = false,
                },
                new CorruptionTierModifiers
                {
                    Tier = 4,
                    PlayerDamageDealtMultiplier = 1.00,
                    PlayerDamageTakenMultiplier = 1.45,
                    PlayerCritBonus = 0.20,
                    EnemyCritBonusAgainstPlayer = 0.20,
                    EnemyCritDamageMultiplierAgainstPlayer = 1.35,
                    EnemyAccuracyBonus = 0.40,
                    GlobalAccuracyBonus = 0.40,
                    GlobalCritBonus = 0.20,
                    EnemiesGuaranteedHit = true,
                    EnemiesAlwaysCrit = true,
                },
            ],
        };
    }
}

public static class CorruptionTierCalculator
{
    public static int GetTier(double corruptionValue)
    {
        var clampedFloor = Math.Max(CorruptionRules.MinCorruptionValue, corruptionValue);
        if (clampedFloor <= CorruptionRules.Tier0UpperInclusive)
        {
            return 0;
        }

        if (clampedFloor <= CorruptionRules.Tier1UpperInclusive)
        {
            return 1;
        }

        if (clampedFloor <= CorruptionRules.Tier2UpperInclusive)
        {
            return 2;
        }

        if (clampedFloor <= CorruptionRules.Tier3UpperInclusive)
        {
            return 3;
        }

        return 4;
    }
}

public enum ElementMatchupKind
{
    Neutral = 0,
    Advantage = 1,
    Disadvantage = 2,
}

public static class ElementTriangle
{
    public static bool HasAdvantage(ElementType attacker, ElementType defender)
    {
        return (attacker, defender) switch
        {
            (ElementType.Fire, ElementType.Metal) => true,
            (ElementType.Metal, ElementType.Anomaly) => true,
            (ElementType.Anomaly, ElementType.Fire) => true,
            _ => false,
        };
    }

    public static bool HasDisadvantage(ElementType attacker, ElementType defender) =>
        HasAdvantage(defender, attacker);

    public static ElementMatchupKind GetMatchup(ElementType attacker, ElementType defender)
    {
        if (HasAdvantage(attacker, defender))
        {
            return ElementMatchupKind.Advantage;
        }

        if (HasDisadvantage(attacker, defender))
        {
            return ElementMatchupKind.Disadvantage;
        }

        return ElementMatchupKind.Neutral;
    }
}