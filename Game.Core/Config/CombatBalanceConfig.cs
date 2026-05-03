using Game.Core.Domain;

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
}

public sealed class CombatBalanceConfig
{
    public required IReadOnlyList<CorruptionTierModifiers> CorruptionTiers { get; init; }
    public double ElementAdvantageMultiplier { get; init; } = 1.5;
    public double ElementDisadvantageMultiplier { get; init; } = 0.5;
    public double BlindMissChance { get; init; } = 0.5;
    public double DodgeNegateChance { get; init; } = 0.5;
    public double BlockDamageMultiplier { get; init; } = 0.5;
    public double BlockPlusDamageMultiplier { get; init; } = 0.25;

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
                },
                new CorruptionTierModifiers
                {
                    Tier = 1,
                    PlayerDamageDealtMultiplier = 1.08,
                    PlayerDamageTakenMultiplier = 1.08,
                    PlayerCritBonus = 0.03,
                    EnemyCritBonusAgainstPlayer = 0.03,
                },
                new CorruptionTierModifiers
                {
                    Tier = 2,
                    PlayerDamageDealtMultiplier = 1.16,
                    PlayerDamageTakenMultiplier = 1.16,
                    PlayerCritBonus = 0.06,
                    EnemyCritBonusAgainstPlayer = 0.06,
                },
                new CorruptionTierModifiers
                {
                    Tier = 3,
                    PlayerDamageDealtMultiplier = 1.25,
                    PlayerDamageTakenMultiplier = 1.25,
                    PlayerCritBonus = 0.10,
                    EnemyCritBonusAgainstPlayer = 0.10,
                },
                new CorruptionTierModifiers
                {
                    Tier = 4,
                    PlayerDamageDealtMultiplier = 1.25,
                    PlayerDamageTakenMultiplier = 1.45,
                    PlayerCritBonus = 0.10,
                    EnemyCritBonusAgainstPlayer = 0.18,
                    EnemyCritDamageMultiplierAgainstPlayer = 1.35,
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
}
