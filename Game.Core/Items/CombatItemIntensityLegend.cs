using Game.Core.Domain;

namespace Game.Core.Items;

/// <summary>
/// Locked Phase I intensity mapping from REWORK_SKILLS_PLAN.md.
/// Relative: + = 5pp, ++ = 10pp, +++ = 15pp, ++++ = 20pp (negatives mirrored).
/// Absolute HP: +10 / ++20 / +++35 / ++++50.
/// Absolute Defense: +3 / ++6 / +++10 pp.
/// Absolute Crit: +1 / ++2 / +++4 pp.
/// </summary>
public static class CombatItemIntensityLegend
{
    public const double RelativePercentagePointsPerRank = 0.05;
    public const int AbsoluteHealthPlus = 10;
    public const int AbsoluteHealthPlusPlus = 20;
    public const int AbsoluteHealthPlusPlusPlus = 35;
    public const int AbsoluteHealthPlusPlusPlusPlus = 50;
    public const double AbsoluteDefensePlus = 0.03;
    public const double AbsoluteDefensePlusPlus = 0.06;
    public const double AbsoluteDefensePlusPlusPlus = 0.10;
    public const double AbsoluteCritPlus = 0.01;
    public const double AbsoluteCritPlusPlus = 0.02;
    public const double AbsoluteCritPlusPlusPlus = 0.04;

    public static int ResolveAbsoluteHealthDelta(int intensityRank)
    {
        var magnitude = Math.Abs(intensityRank) switch
        {
            1 => AbsoluteHealthPlus,
            2 => AbsoluteHealthPlusPlus,
            3 => AbsoluteHealthPlusPlusPlus,
            4 => AbsoluteHealthPlusPlusPlusPlus,
            _ => 0,
        };

        return magnitude * Math.Sign(intensityRank);
    }

    public static double ResolveAbsoluteDefenseChanceDelta(int intensityRank)
    {
        var magnitude = Math.Abs(intensityRank) switch
        {
            1 => AbsoluteDefensePlus,
            2 => AbsoluteDefensePlusPlus,
            3 => AbsoluteDefensePlusPlusPlus,
            _ => 0,
        };

        return magnitude * Math.Sign(intensityRank);
    }

    public static double ResolveAbsoluteCritChanceDelta(int intensityRank)
    {
        var magnitude = Math.Abs(intensityRank) switch
        {
            1 => AbsoluteCritPlus,
            2 => AbsoluteCritPlusPlus,
            3 => AbsoluteCritPlusPlusPlus,
            _ => 0,
        };

        return magnitude * Math.Sign(intensityRank);
    }

    public static double ResolveRelativeFraction(int intensityRank) =>
        intensityRank * RelativePercentagePointsPerRank;

    public static CombatItemRarity ResolveIndividualStatusRarity(int sizeRank) =>
        sizeRank switch
        {
            2 => CombatItemRarity.Rare,
            3 => CombatItemRarity.Epic,
            _ => CombatItemRarity.Common,
        };

    public static CombatItemRarity ResolveThematicRarity(int sizeRank) =>
        sizeRank switch
        {
            2 => CombatItemRarity.Epic,
            3 => CombatItemRarity.Legendary,
            _ => CombatItemRarity.Rare,
        };
}
