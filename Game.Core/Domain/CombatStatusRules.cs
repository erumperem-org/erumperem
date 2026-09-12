using Game.Core.Models;

namespace Game.Core.Domain;

/// <summary>
/// Shared combat status (token) rules: debuff set, EOT decay, magnitude helpers.
/// Player-facing name is "Status"; engine storage remains <see cref="TokenType"/>.
/// Perma* variants use <see cref="PermanentStatusEfficiencyMultiplier"/> and never decay at end of turn.
/// Stealth accuracy penalty is flat (-40%) while any Stealth stack is present (spec L785).
/// </summary>
public static class CombatStatusRules
{
    public const double StrengthDamageCausedBonusPerStack = 0.25;
    public const double WeakenDamageCausedPenaltyPerStack = 0.50;
    public const double DefenseIncomingDamageReductionPerStack = 0.25;
    public const double VulnerabilityIncomingDamageBonusPerStack = 0.50;
    public const double LuckyShotCritChanceBonusPerStack = 0.04;
    public const double DexterityAccuracyBonusPerStack = 0.10;
    public const double ClumsyAccuracyPenaltyPerStack = 0.20;
    public const double ExpositionAccuracyBonusPerStack = 0.20;
    /// <summary>Flat accuracy penalty while the target has any Stealth stacks.</summary>
    public const double StealthTargetAccuracyPenalty = 0.40;
    public const double MarkCritChanceBonusPerStack = 0.10;
    public const double MarkCritDamageBonusPerStack = 0.50;
    public const int ControlledInstabilityReflectDamagePerStack = 2;
    public const int DestabilizationDamagePerStack = 3;
    public const double BleedingMaxHpDamageFractionPerStack = 0.05;
    public const double CorrosionDebuffAmplifyPerStack = 0.10;
    public const int CorrosionEndOfTurnDamage = 5;
    public const double ConfusionRetargetChance = 0.33;
    public const double DizzyRetargetChance = 0.50;
    public const double CriticalStrikeBaseDamageMultiplier = 2.0;
    public const double MinimumHitChanceFraction = 0.05;
    public const double MaximumHitChanceFraction = 1.0;
    public const double PermanentStatusEfficiencyMultiplier = 0.25;

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
        TokenType.Hypnosis,
        TokenType.Dizzy,
        TokenType.Burn,
        TokenType.PermaWeaken,
        TokenType.PermaVulnerability,
        TokenType.PermaClumsy,
        TokenType.PermaExposition,
    ];

    private static readonly HashSet<TokenType> BuffTokenTypes =
    [
        TokenType.Strength,
        TokenType.Defense,
        TokenType.LuckyShot,
        TokenType.Dexterity,
        TokenType.Regeneration,
        TokenType.Stealth,
        TokenType.ControlledInstability,
        TokenType.Block,
        TokenType.BlockPlus,
        TokenType.Dodge,
        TokenType.BonusAction,
        TokenType.PermaStrength,
        TokenType.PermaDefense,
        TokenType.PermaDexterity,
        TokenType.PermaStealth,
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
        TokenType.Stealth,
        TokenType.Corrosion,
        TokenType.Burn,
        TokenType.Hypnosis,
        TokenType.Dizzy,
    ];

    private static readonly HashSet<TokenType> PermanentTokenTypes =
    [
        TokenType.PermaStrength,
        TokenType.PermaDefense,
        TokenType.PermaWeaken,
        TokenType.PermaVulnerability,
        TokenType.PermaDexterity,
        TokenType.PermaClumsy,
        TokenType.PermaExposition,
        TokenType.PermaStealth,
    ];

    public static bool IsDebuffToken(TokenType tokenType) => DebuffTokenTypes.Contains(tokenType);

    public static bool IsBuffToken(TokenType tokenType) => BuffTokenTypes.Contains(tokenType);

    public static bool IsPermanentStatus(TokenType tokenType) => PermanentTokenTypes.Contains(tokenType);

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

    public static double DamageCausedMultiplierFromTokens(TokenComponent tokens) =>
        DamageCausedMultiplierFromTokens(tokens, tokenEfficiencyLookup: null);

    public static double DamageCausedMultiplierFromTokens(
        TokenComponent tokens,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        if (tokens == null)
        {
            return 1.0;
        }

        var corrosionAmplify = CorrosionAmplificationMultiplier(tokens);
        var strengthStacks = EffectiveMagnitudeStacks(
            tokens,
            TokenType.Strength,
            TokenType.PermaStrength,
            tokenEfficiencyLookup);
        var weakenStacks = EffectiveMagnitudeStacks(
            tokens,
            TokenType.Weaken,
            TokenType.PermaWeaken,
            tokenEfficiencyLookup);
        var strengthBonus = StrengthDamageCausedBonusPerStack * strengthStacks;
        var weakenPenalty = WeakenDamageCausedPenaltyPerStack * weakenStacks * corrosionAmplify;
        return Math.Max(0.0, 1.0 + strengthBonus - weakenPenalty);
    }

    public static double ApplyBaseDefenseChance(double incomingDamage, double defenseChance)
    {
        var clampedDefenseChance = Math.Clamp(defenseChance, 0.0, 1.0);
        return incomingDamage * (1.0 - clampedDefenseChance);
    }

    public static double IncomingDamageMultiplierFromTokens(TokenComponent tokens) =>
        IncomingDamageMultiplierFromTokens(tokens, tokenEfficiencyLookup: null);

    public static double IncomingDamageMultiplierFromTokens(
        TokenComponent tokens,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        if (tokens == null)
        {
            return 1.0;
        }

        var corrosionAmplify = CorrosionAmplificationMultiplier(tokens);
        var defenseStacks = EffectiveMagnitudeStacks(
            tokens,
            TokenType.Defense,
            TokenType.PermaDefense,
            tokenEfficiencyLookup);
        var vulnerabilityStacks = EffectiveMagnitudeStacks(
            tokens,
            TokenType.Vulnerability,
            TokenType.PermaVulnerability,
            tokenEfficiencyLookup);
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

    public static double CritChanceBonusFromDefenderTokens(TokenComponent defenderTokens) =>
        CritChanceBonusFromDefenderTokens(defenderTokens, tokenEfficiencyLookup: null);

    public static double CritChanceBonusFromDefenderTokens(
        TokenComponent defenderTokens,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        if (defenderTokens == null)
        {
            return 0;
        }

        var markStacks = defenderTokens.GetStacks(TokenType.Mark) *
                         LookupEfficiency(tokenEfficiencyLookup, TokenType.Mark);
        var corrosionAmplify = CorrosionAmplificationMultiplier(defenderTokens);
        return MarkCritChanceBonusPerStack * markStacks * corrosionAmplify;
    }

    public static double CritDamageMultiplierFromDefenderMark(TokenComponent defenderTokens) =>
        CritDamageMultiplierFromDefenderMark(defenderTokens, tokenEfficiencyLookup: null);

    public static double CritDamageMultiplierFromDefenderMark(
        TokenComponent defenderTokens,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        if (defenderTokens == null)
        {
            return CriticalStrikeBaseDamageMultiplier;
        }

        var markStacks = defenderTokens.GetStacks(TokenType.Mark) *
                         LookupEfficiency(tokenEfficiencyLookup, TokenType.Mark);
        if (markStacks <= 0)
        {
            return CriticalStrikeBaseDamageMultiplier;
        }

        var corrosionAmplify = CorrosionAmplificationMultiplier(defenderTokens);
        return CriticalStrikeBaseDamageMultiplier *
               (1.0 + (MarkCritDamageBonusPerStack * markStacks * corrosionAmplify));
    }

    public static double AccuracyModifierFromActorTokens(TokenComponent actorTokens) =>
        AccuracyModifierFromActorTokens(actorTokens, tokenEfficiencyLookup: null);

    public static double AccuracyModifierFromActorTokens(
        TokenComponent actorTokens,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        if (actorTokens == null)
        {
            return 0;
        }

        var dexterityStacks = EffectiveMagnitudeStacks(
            actorTokens,
            TokenType.Dexterity,
            TokenType.PermaDexterity,
            tokenEfficiencyLookup);
        var clumsyStacks = EffectiveMagnitudeStacks(
            actorTokens,
            TokenType.Clumsy,
            TokenType.PermaClumsy,
            tokenEfficiencyLookup);
        var corrosionAmplify = CorrosionAmplificationMultiplier(actorTokens);
        return (DexterityAccuracyBonusPerStack * dexterityStacks) -
               (ClumsyAccuracyPenaltyPerStack * clumsyStacks * corrosionAmplify);
    }

    public static double AccuracyBonusFromTargetExposition(TokenComponent targetTokens) =>
        AccuracyBonusFromTargetExposition(targetTokens, tokenEfficiencyLookup: null);

    public static double AccuracyBonusFromTargetExposition(
        TokenComponent targetTokens,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        if (targetTokens == null)
        {
            return 0;
        }

        var expositionStacks = EffectiveMagnitudeStacks(
            targetTokens,
            TokenType.Exposition,
            TokenType.PermaExposition,
            tokenEfficiencyLookup);
        var corrosionAmplify = CorrosionAmplificationMultiplier(targetTokens);
        return ExpositionAccuracyBonusPerStack * expositionStacks * corrosionAmplify;
    }

    /// <summary>
    /// Accuracy penalty applied to skills that target a combatant with Stealth (flat while present).
    /// PermaStealth is per-stack at 25% of the flat Stealth penalty.
    /// </summary>
    public static double AccuracyPenaltyFromTargetStealth(TokenComponent targetTokens)
    {
        if (targetTokens == null)
        {
            return 0;
        }

        var penalty = 0.0;
        if (targetTokens.GetStacks(TokenType.Stealth) > 0)
        {
            penalty += StealthTargetAccuracyPenalty;
        }

        var permaStealthStacks = targetTokens.GetStacks(TokenType.PermaStealth);
        if (permaStealthStacks > 0)
        {
            penalty += StealthTargetAccuracyPenalty * PermanentStatusEfficiencyMultiplier * permaStealthStacks;
        }

        return penalty;
    }

    public static TokenType? MapDotTypeToStatusToken(DotType dotType) =>
        dotType switch
        {
            DotType.Burn => TokenType.Burn,
            DotType.Bleed => TokenType.Bleeding,
            _ => null,
        };

    private static double EffectiveMagnitudeStacks(
        TokenComponent tokens,
        TokenType temporaryTokenType,
        TokenType permanentTokenType,
        Func<TokenType, double>? tokenEfficiencyLookup)
    {
        var temporaryStacks = tokens.GetStacks(temporaryTokenType) *
                              LookupEfficiency(tokenEfficiencyLookup, temporaryTokenType);
        var permanentStacks = tokens.GetStacks(permanentTokenType) *
                              PermanentStatusEfficiencyMultiplier *
                              LookupEfficiency(tokenEfficiencyLookup, permanentTokenType);
        return temporaryStacks + permanentStacks;
    }

    private static double LookupEfficiency(Func<TokenType, double>? tokenEfficiencyLookup, TokenType tokenType) =>
        tokenEfficiencyLookup?.Invoke(tokenType) ?? 1.0;
}
