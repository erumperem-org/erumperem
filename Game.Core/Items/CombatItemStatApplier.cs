using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Items;

/// <summary>Applies catalog items to combatant <see cref="HealthComponent.MaxHp"/>, DefenseChance, and CritChance.</summary>
public static class CombatItemStatApplier
{
    public const int MinimumMaxHitPoints = 1;
    public const double MinimumDefenseChance = 0;
    public const double MaximumDefenseChance = 1;
    public const double MinimumCritChance = 0;

    public static CombatantBaseStatSnapshot CaptureBaseStats(Combatant combatant)
    {
        if (combatant == null)
        {
            throw new ArgumentNullException(nameof(combatant));
        }

        return new CombatantBaseStatSnapshot
        {
            MaxHp = combatant.Health.MaxHp,
            DefenseChance = combatant.Stats.DefenseChance,
            CritChance = combatant.Stats.CritChance,
            Speed = combatant.Stats.Speed,
            Accuracy = combatant.Stats.Accuracy,
        };
    }

    public static CombatantBaseStatSnapshot ApplyItem(
        CombatantBaseStatSnapshot baseline,
        CombatItemDefinition item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        var maxHp = baseline.MaxHp;
        var defenseChance = baseline.DefenseChance;
        var critChance = baseline.CritChance;

        foreach (var modifier in item.StatModifiers)
        {
            ApplyModifier(ref maxHp, ref defenseChance, ref critChance, modifier);
        }

        return new CombatantBaseStatSnapshot
        {
            MaxHp = Math.Max(MinimumMaxHitPoints, maxHp),
            DefenseChance = Clamp(defenseChance, MinimumDefenseChance, MaximumDefenseChance),
            CritChance = Math.Max(MinimumCritChance, critChance),
            Speed = baseline.Speed,
            Accuracy = baseline.Accuracy,
        };
    }

    public static CombatantBaseStatSnapshot ApplyItems(
        CombatantBaseStatSnapshot baseline,
        IEnumerable<CombatItemDefinition> items)
    {
        var current = baseline;
        foreach (var item in items)
        {
            current = ApplyItem(current, item);
        }

        return current;
    }

    public static void ApplyItemToCombatant(Combatant combatant, CombatItemDefinition item)
    {
        if (combatant == null)
        {
            throw new ArgumentNullException(nameof(combatant));
        }

        var baseline = CaptureBaseStats(combatant);
        var modified = ApplyItem(baseline, item);
        WriteStatsToCombatant(combatant, baseline.MaxHp, modified);
    }

    public static void WriteStatsToCombatant(
        Combatant combatant,
        int previousMaxHp,
        CombatantBaseStatSnapshot modified)
    {
        var maxHpDelta = modified.MaxHp - previousMaxHp;
        var currentHp = combatant.Health.CurrentHp + maxHpDelta;
        currentHp = Math.Clamp(currentHp, 0, modified.MaxHp);

        combatant.Health = new HealthComponent
        {
            MaxHp = modified.MaxHp,
            CurrentHp = currentHp,
            IsDead = currentHp <= 0 || combatant.Health.IsDead,
            IsDeathblowPending = combatant.Health.IsDeathblowPending,
        };

        combatant.Stats = new StatsComponent
        {
            Speed = modified.Speed,
            Accuracy = modified.Accuracy,
            CritChance = modified.CritChance,
            DefenseChance = modified.DefenseChance,
        };
    }

    private static void ApplyModifier(
        ref int maxHp,
        ref double defenseChance,
        ref double critChance,
        CombatItemStatModifier modifier)
    {
        switch (modifier.Stat)
        {
            case CombatItemStatKind.Health:
                maxHp = ApplyHealth(maxHp, modifier);
                break;
            case CombatItemStatKind.Defense:
                defenseChance = ApplyChanceStat(
                    defenseChance,
                    modifier,
                    CombatItemIntensityLegend.ResolveAbsoluteDefenseChanceDelta);
                break;
            case CombatItemStatKind.CriticalChance:
                critChance = ApplyChanceStat(
                    critChance,
                    modifier,
                    CombatItemIntensityLegend.ResolveAbsoluteCritChanceDelta);
                break;
        }
    }

    private static int ApplyHealth(int maxHp, CombatItemStatModifier modifier)
    {
        if (modifier.Scale == CombatItemModifierScale.Relative)
        {
            var relativeFraction = CombatItemIntensityLegend.ResolveRelativeFraction(modifier.IntensityRank);
            return (int)Math.Round(maxHp * (1.0 + relativeFraction), MidpointRounding.AwayFromZero);
        }

        return maxHp + CombatItemIntensityLegend.ResolveAbsoluteHealthDelta(modifier.IntensityRank);
    }

    private static double ApplyChanceStat(
        double currentValue,
        CombatItemStatModifier modifier,
        Func<int, double> resolveAbsoluteDelta)
    {
        if (modifier.Scale == CombatItemModifierScale.Relative)
        {
            return currentValue + CombatItemIntensityLegend.ResolveRelativeFraction(modifier.IntensityRank);
        }

        return currentValue + resolveAbsoluteDelta(modifier.IntensityRank);
    }

    private static double Clamp(double value, double minimum, double maximum) =>
        Math.Min(maximum, Math.Max(minimum, value));
}
