using Game.Core.Models;

namespace Game.Core.Domain;

/// <summary>
/// Shared combat status (token) rules: debuff set, EOT decay, magnitude helpers.
/// Player-facing name is "Status"; engine storage remains <see cref="TokenType"/>.
/// </summary>
public static class CombatStatusRules
{
    public const double StrengthOutgoingDamageBonusPerStack = 0.25;
    public const double WeakenOutgoingDamagePenaltyPerStack = 0.50;
    public const double DefenseIncomingDamageReductionPerStack = 0.25;
    public const double VulnerabilityIncomingDamageBonusPerStack = 0.50;
    public const double LuckyShotCritChanceBonusPerStack = 0.04;
    public const double DexterityAccuracyBonusPerStack = 0.10;
    public const double ClumsyAccuracyPenaltyPerStack = 0.20;
    public const double ExpositionAccuracyBonusPerStack = 0.20;
    public const double MarkCritChanceBonusPerStack = 0.10;
    public const double MarkCritDamageBonusPerStack = 0.50;
    public const int ControlledInstabilityReflectDamagePerStack = 2;
    public const int DestabilizationDamagePerStack = 3;
    public const double BleedingMaxHpDamageFractionPerStack = 0.05;
    public const double CorrosionDebuffAmplifyPerStack = 0.10;
    public const int CorrosionEndOfTurnDamage = 5;
    public const double ConfusionRetargetChance = 0.33;
    public const double CriticalStrikeBaseDamageMultiplier = 2.0;

    private static readonly HashSet<TokenType> DebuffTokenTypes =
    [
        TokenType.Weaken,
        TokenType.Vulnerability,
        TokenType.Clumsy,
        TokenType.Exposition,
        TokenType.Corrosion,
        TokenType.Mark,
        TokenType.Confusion,
        TokenType.Bleeding,
        TokenType.Blind,
        TokenType.Stun,
    ];

    private static readonly HashSet<TokenType> EndOfTurnDecayTokenTypes =
    [
        TokenType.Strength,
        TokenType.Defense,
        TokenType.Weaken,
        TokenType.Vulnerability,
        TokenType.LuckyShot,
        TokenType.Dexterity,
        TokenType.Exposition,
        TokenType.Mark,
        TokenType.Clumsy,
        TokenType.Confusion,
        TokenType.Regeneration,
        TokenType.Bleeding,
    ];

    public static bool IsDebuffToken(TokenType tokenType) => DebuffTokenTypes.Contains(tokenType);

    public static IReadOnlyCollection<TokenType> AllDebuffTokenTypes => DebuffTokenTypes;

    public static IReadOnlyCollection<TokenType> EndOfTurnDecayTokens => EndOfTurnDecayTokenTypes;

    public static int CountDistinctDebuffTypes(TokenComponent tokens)
    {
        if (tokens == null)
        {
            return 0;
        }

        var distinctDebuffCount = 0;
        foreach (var debuffTokenType in DebuffTokenTypes)
        {
            if (tokens.GetStacks(debuffTokenType) > 0)
            {
                distinctDebuffCount++;
            }
        }

        return distinctDebuffCount;
    }

    public static double CorrosionAmplificationMultiplier(TokenComponent tokens)
    {
        var corrosionStacks = tokens?.GetStacks(TokenType.Corrosion) ?? 0;
        if (corrosionStacks <= 0)
        {
            return 1.0;
        }

        return 1.0 + (CorrosionDebuffAmplifyPerStack * corrosionStacks);
    }

    public static double OutgoingDamageMultiplierFromTokens(TokenComponent tokens)
    {
        if (tokens == null)
        {
            return 1.0;
        }

        var corrosionAmplify = CorrosionAmplificationMultiplier(tokens);
        var strengthStacks = tokens.GetStacks(TokenType.Strength);
        var weakenStacks = tokens.GetStacks(TokenType.Weaken);
        var strengthBonus = StrengthOutgoingDamageBonusPerStack * strengthStacks;
        var weakenPenalty = WeakenOutgoingDamagePenaltyPerStack * weakenStacks * corrosionAmplify;
        return Math.Max(0.0, 1.0 + strengthBonus - weakenPenalty);
    }

    public static double IncomingDamageMultiplierFromTokens(TokenComponent tokens)
    {
        if (tokens == null)
        {
            return 1.0;
        }

        var corrosionAmplify = CorrosionAmplificationMultiplier(tokens);
        var defenseStacks = tokens.GetStacks(TokenType.Defense);
        var vulnerabilityStacks = tokens.GetStacks(TokenType.Vulnerability);
        var defenseReduction = DefenseIncomingDamageReductionPerStack * defenseStacks;
        var vulnerabilityBonus = VulnerabilityIncomingDamageBonusPerStack * vulnerabilityStacks * corrosionAmplify;
        return Math.Max(0.0, 1.0 - defenseReduction + vulnerabilityBonus);
    }

    public static double CritChanceBonusFromAttackerTokens(TokenComponent attackerTokens)
    {
        if (attackerTokens == null)
        {
            return 0;
        }

        return LuckyShotCritChanceBonusPerStack * attackerTokens.GetStacks(TokenType.LuckyShot);
    }

    public static double CritChanceBonusFromDefenderTokens(TokenComponent defenderTokens)
    {
        if (defenderTokens == null)
        {
            return 0;
        }

        var markStacks = defenderTokens.GetStacks(TokenType.Mark);
        var corrosionAmplify = CorrosionAmplificationMultiplier(defenderTokens);
        return MarkCritChanceBonusPerStack * markStacks * corrosionAmplify;
    }

    public static double CritDamageMultiplierFromDefenderMark(TokenComponent defenderTokens)
    {
        if (defenderTokens == null)
        {
            return CriticalStrikeBaseDamageMultiplier;
        }

        var markStacks = defenderTokens.GetStacks(TokenType.Mark);
        if (markStacks <= 0)
        {
            return CriticalStrikeBaseDamageMultiplier;
        }

        var corrosionAmplify = CorrosionAmplificationMultiplier(defenderTokens);
        return CriticalStrikeBaseDamageMultiplier *
               (1.0 + (MarkCritDamageBonusPerStack * markStacks * corrosionAmplify));
    }

    public static double AccuracyModifierFromActorTokens(TokenComponent actorTokens)
    {
        if (actorTokens == null)
        {
            return 0;
        }

        var dexterityStacks = actorTokens.GetStacks(TokenType.Dexterity);
        var clumsyStacks = actorTokens.GetStacks(TokenType.Clumsy);
        var corrosionAmplify = CorrosionAmplificationMultiplier(actorTokens);
        return (DexterityAccuracyBonusPerStack * dexterityStacks) -
               (ClumsyAccuracyPenaltyPerStack * clumsyStacks * corrosionAmplify);
    }

    public static double AccuracyBonusFromTargetExposition(TokenComponent targetTokens)
    {
        if (targetTokens == null)
        {
            return 0;
        }

        var expositionStacks = targetTokens.GetStacks(TokenType.Exposition);
        var corrosionAmplify = CorrosionAmplificationMultiplier(targetTokens);
        return ExpositionAccuracyBonusPerStack * expositionStacks * corrosionAmplify;
    }
}
