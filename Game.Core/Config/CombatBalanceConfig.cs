using Game.Core.Domain;

namespace Game.Core.Config;

public sealed class CorruptionTierModifiers
{
    public required int Tier { get; init; }
    public required double PlayerDamageDealtMultiplier { get; init; }
    public required double PlayerDamageTakenMultiplier { get; init; }
    public required double PlayerCritBonus { get; init; }
    public required double EnemyCritBonusAgainstPlayer { get; init; }
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
            ],
        };
    }
}

public static class CorruptionTierCalculator
{
    public static int GetTier(double corruptionValue)
    {
        if (corruptionValue >= 99) return 3;
        if (corruptionValue >= 66) return 2;
        if (corruptionValue >= 33) return 1;
        return 0;
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
