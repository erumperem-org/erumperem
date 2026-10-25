using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Presentation;

/// <summary>
/// Generates the skill summary line for the UI in English.
/// </summary>
public static class SkillPlayerDescriptionBuilder
{
    public static string TranslateToEnglish(string name) =>
        CombatDisplayTerms.TranslateExactName(name);

    public sealed class SkillDescriptionContext
    {
        public Combatant? Actor { get; init; }
        public BattleState? BattleState { get; init; }
        public Combatant? PreviewTarget { get; init; }
        public IReadOnlyDictionary<string, SkillDefinition>? SkillsById { get; init; }
    }

    public static string BuildSummaryLine(SkillDefinition skill, SkillDescriptionContext? context = null)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        var detailParts = new List<string>();
        AppendIfNotEmpty(detailParts, DescribeTarget(skill));
        AppendIfNotEmpty(detailParts, DescribeDirectDamage(skill));
        AppendIfNotEmpty(detailParts, DescribeDebuffScaling(skill));
        AppendIfNotEmpty(detailParts, DescribeBonusDamagePerOwnToken(skill));
        AppendIfNotEmpty(detailParts, DescribeAccuracyPenaltyPerLivingEnemy(skill));
        AppendIfNotEmpty(detailParts, DescribeHitChance(skill, context));

        if (ShouldShowCriticalChance(skill, context))
        {
            AppendIfNotEmpty(detailParts, DescribeCriticalChance(skill, context));
        }

        AppendIfNotEmpty(detailParts, DescribeEffects(skill.EffectsOnHit, skill, prefix: null));
        AppendIfNotEmpty(detailParts, DescribeFollowUpSkills(skill, context));
        AppendIfNotEmpty(detailParts, DescribeExtraTurn(skill));
        AppendIfNotEmpty(detailParts, DescribeCorruptionCost(skill));

        foreach (var passivePart in DescribePassiveModifiersForSkill(skill, context))
        {
            AppendIfNotEmpty(detailParts, passivePart);
        }

        var translatedName = TranslateToEnglish(skill.Name);
        return $"{translatedName}: {string.Join(" | ", detailParts)}.";
    }

    private static void AppendIfNotEmpty(List<string> parts, string phrase)
    {
        if (!string.IsNullOrEmpty(phrase))
        {
            parts.Add(phrase);
        }
    }

    private static string DescribeTarget(SkillDefinition skill) =>
        skill.TargetKind switch
        {
            SkillTargetKind.OneEnemy => "1 target",
            SkillTargetKind.UpToThreeEnemies => "up to 3 enemies",
            SkillTargetKind.AllEnemies => "all enemies",
            SkillTargetKind.OneAlly => "1 ally",
            SkillTargetKind.SelfOrAlly => "self or ally",
            SkillTargetKind.SelfAndAlly => "self and ally",
            SkillTargetKind.Self => "self",
            _ => "1 target",
        };

    private static bool HasDirectDamage(SkillDefinition skill) =>
        skill.BaseDamage.Min > 0 || skill.BaseDamage.Max > 0;

    private static string DescribeDirectDamage(SkillDefinition skill)
    {
        if (!HasDirectDamage(skill) && !skill.ComputeFromDebuffTypesOnTarget)
        {
            return "no direct damage";
        }

        if (skill.ComputeFromDebuffTypesOnTarget && !HasDirectDamage(skill))
        {
            return string.Empty;
        }

        var minimumDamage = skill.BaseDamage.Min;
        var maximumDamage = skill.BaseDamage.Max;
        var hitCount = Math.Max(1, skill.HitCount);
        var damagePhrase = minimumDamage == maximumDamage
            ? $"{minimumDamage} damage"
            : $"{minimumDamage}–{maximumDamage} damage";

        if (hitCount > 1)
        {
            return $"{hitCount} hits of {damagePhrase}";
        }

        return damagePhrase;
    }

    private static string DescribeFollowUpSkills(SkillDefinition skill, SkillDescriptionContext? context)
    {
        if (skill.FollowUpSkillIds == null || skill.FollowUpSkillIds.Count == 0)
        {
            return string.Empty;
        }

        var followUpNames = new List<string>();
        foreach (var followUpSkillId in skill.FollowUpSkillIds)
        {
            SkillDefinition? followUpSkill = null;
            if (context?.BattleState != null &&
                context.BattleState.SkillsById.TryGetValue(followUpSkillId, out followUpSkill))
            {
            }
            else if (context?.SkillsById != null)
            {
                context.SkillsById.TryGetValue(followUpSkillId, out followUpSkill);
            }

            followUpNames.Add(followUpSkill != null
                ? TranslateToEnglish(followUpSkill.Name)
                : followUpSkillId);
        }

        if (followUpNames.Count == 0)
        {
            return string.Empty;
        }

        return "follow-up: " + string.Join(", then ", followUpNames);
    }

    private static string DescribeExtraTurn(SkillDefinition skill)
    {
        if (skill.GrantsBonusActionsToAllies)
        {
            return "extra turn for self and ally";
        }

        if (skill.ChanceToNotEndTurn > 0)
        {
            return $"{FormatPercentFromFraction(skill.ChanceToNotEndTurn)} chance of extra turn";
        }

        return string.Empty;
    }

    private static string DescribeAccuracyPenaltyPerLivingEnemy(SkillDefinition skill)
    {
        if (skill.AccuracyPenaltyPerLivingEnemy <= 0)
        {
            return string.Empty;
        }

        return $"-{FormatPercentFromFraction(skill.AccuracyPenaltyPerLivingEnemy)} hit chance per living enemy";
    }

    private static string DescribeBonusDamagePerOwnToken(SkillDefinition skill)
    {
        if (!skill.BonusDamagePerOwnToken.HasValue)
        {
            return string.Empty;
        }

        var tokenName = TokenDisplayName(skill.BonusDamagePerOwnToken.Value);
        var damagePerStack = Math.Max(1, skill.BonusDamagePerOwnTokenStacks);
        return $"+{damagePerStack} damage per {tokenName} stack you have";
    }

    private static string DescribeDebuffScaling(SkillDefinition skill)
    {
        if (!skill.ComputeFromDebuffTypesOnTarget)
        {
            return string.Empty;
        }

        var scalingParts = new List<string>();
        if (skill.DamagePerDistinctDebuffType > 0)
        {
            scalingParts.Add($"+{skill.DamagePerDistinctDebuffType} damage");
        }

        if (skill.CritChancePerDistinctDebuffType > 0)
        {
            scalingParts.Add($"+{FormatPercentFromFraction(skill.CritChancePerDistinctDebuffType)} crit");
        }

        if (skill.AccuracyPerDistinctDebuffType > 0)
        {
            scalingParts.Add($"+{FormatPercentFromFraction(skill.AccuracyPerDistinctDebuffType)} hit chance");
        }

        if (scalingParts.Count == 0)
        {
            return "stronger for each distinct status on the enemy";
        }

        return string.Join(", ", scalingParts) + " per distinct status on the enemy";
    }

    private static string DescribeHitChance(SkillDefinition skill, SkillDescriptionContext? context)
    {
        var actorAccuracy = context?.Actor?.Stats.Accuracy ?? 1.0;
        var combinedHitChance = skill.Accuracy * actorAccuracy;
        if (combinedHitChance >= 0.9995)
        {
            return string.Empty;
        }

        return $"{FormatPercentFromFraction(combinedHitChance)} hit chance" +
               (Math.Max(1, skill.HitCount) > 1 ? " per hit" : string.Empty);
    }

    private static bool ShouldShowCriticalChance(SkillDefinition skill, SkillDescriptionContext? context)
    {
        if (!HasDirectDamage(skill))
        {
            return false;
        }

        return ComputeEffectiveCriticalChanceFraction(skill, context) > 0.0005;
    }

    private static string DescribeCriticalChance(SkillDefinition skill, SkillDescriptionContext? context) =>
        $"{FormatPercentFromFraction(ComputeEffectiveCriticalChanceFraction(skill, context))} crit";

    private static double ComputeEffectiveCriticalChanceFraction(SkillDefinition skill, SkillDescriptionContext? context)
    {
        var criticalChance = skill.BaseCritChance + (context?.Actor?.Stats.CritChance ?? 0.0);
        if (context?.BattleState != null && context.Actor != null)
        {
            var tierModifiers = context.BattleState.BalanceConfig.GetTierModifiers(context.BattleState.CorruptionTier);
            if (context.Actor.Identity.Faction == Faction.Player)
            {
                criticalChance += tierModifiers.PlayerCritBonus;
            }
        }

        return Math.Clamp(criticalChance, 0.0, 1.0);
    }

    private static string DescribeCorruptionCost(SkillDefinition skill)
    {
        var corruptionCost = skill.CorruptionCost;
        if (Math.Abs(corruptionCost) < 1e-12)
        {
            return "no corruption";
        }

        if (corruptionCost > 0)
        {
            return $"+{FormatPlainNumber(corruptionCost)} corruption";
        }

        return $"{FormatPlainNumber(corruptionCost)} corruption";
    }

    private static string DescribeEffects(
        IReadOnlyList<EffectSpec> effects,
        SkillDefinition skill,
        string? prefix)
    {
        if (effects == null || effects.Count == 0)
        {
            return string.Empty;
        }

        var effectPhrases = new List<string>();
        foreach (var effect in effects)
        {
            var phrase = DescribeSingleEffect(effect, skill);
            if (!string.IsNullOrEmpty(phrase))
            {
                effectPhrases.Add(phrase);
            }
        }

        if (effectPhrases.Count == 0)
        {
            return string.Empty;
        }

        var joinedEffects = string.Join(", ", effectPhrases);
        return string.IsNullOrEmpty(prefix) ? joinedEffects : $"{prefix}: {joinedEffects}";
    }

    private static string DescribeSingleEffect(
        EffectSpec effect,
        SkillDefinition skill)
    {
        var chancePrefix = effect.Chance < 0.9995
            ? $"{FormatPercentFromFraction(effect.Chance)} "
            : string.Empty;

        return effect.Type switch
        {
            EffectType.ApplyToken when effect.Token.HasValue =>
                $"{chancePrefix}{FormatTokenGrantPhrase(effect, skill)}",
            EffectType.ApplyDot when effect.Dot.HasValue =>
                DescribeApplyDotEffect(chancePrefix, effect),
            EffectType.HealHpPercent =>
                $"{chancePrefix}heals {FormatPlainNumber(Math.Max(0, effect.Potency))}% HP",
            EffectType.HealHp =>
                $"{chancePrefix}heals {Math.Max(0, effect.Potency)} HP",
            EffectType.ApplyRandomDot =>
                $"{chancePrefix}applies a random DoT",
            EffectType.RemoveAllDebuffTokens =>
                $"{chancePrefix}removes all debuffs",
            EffectType.ConsumeAllTokenStacksDealDamagePerStack when effect.Token.HasValue =>
                $"{chancePrefix}consumes all {TokenDisplayName(effect.Token.Value)}: {Math.Max(1, effect.Potency)} damage per stack",
            EffectType.ConsumeAllTokenStacksHealPerStack when effect.Token.HasValue =>
                $"{chancePrefix}consumes all {TokenDisplayName(effect.Token.Value)}: heals {Math.Max(1, effect.Potency)} HP per stack",
            EffectType.SelfDamageFlat =>
                $"{chancePrefix}{Math.Max(0, effect.Potency)} self-damage",
            EffectType.TriggerDestabilizationOnTargets =>
                $"{chancePrefix}triggers Destabilization on targets",
            EffectType.ApplyBonusAction =>
                $"{chancePrefix}+{FormatTokenStackCount(Math.Max(1, effect.Stacks))} {TokenDisplayName(TokenType.BonusAction)}",
            EffectType.Push =>
                $"{chancePrefix}pushes {Math.Max(1, Math.Abs(effect.Steps))} position(s)",
            EffectType.Pull =>
                $"{chancePrefix}pulls {Math.Max(1, Math.Abs(effect.Steps))} position(s)",
            EffectType.ApplyStun =>
                $"{chancePrefix}{FormatTokenStackCount(Math.Max(1, effect.Stacks))} {TokenDisplayName(TokenType.Stun)}",
            _ => string.Empty,
        };
    }

    private static string DescribeApplyDotEffect(string chancePrefix, EffectSpec effect)
    {
        var dotName = DotDisplayName(effect.Dot!.Value);
        var potency = Math.Max(0, effect.Potency);
        var durationTurns = Math.Max(1, effect.Duration);
        if (potency > 0 && durationTurns > 0)
        {
            return
                $"{chancePrefix}applies {dotName} ({potency} damage/turn, {FormatTurnCount(durationTurns)})";
        }

        return $"{chancePrefix}applies {dotName}";
    }

    private static string FormatTokenGrantPhrase(EffectSpec effect, SkillDefinition skill)
    {
        var stacks = Math.Max(1, effect.Stacks);
        var tokenName = TokenDisplayName(effect.Token!.Value);
        var stackPhrase = FormatTokenStackCount(stacks);
        var scopePrefix = DescribeEffectScopePrefix(effect.EffectScope, skill);
        if (skill.TargetKind == SkillTargetKind.Self && scopePrefix == "on self")
        {
            scopePrefix = string.Empty;
        }

        return string.IsNullOrEmpty(scopePrefix)
            ? $"+{stackPhrase} {tokenName}"
            : $"{scopePrefix}: +{stackPhrase} {tokenName}";
    }

    private static string DescribeEffectScopePrefix(EffectScope effectScope, SkillDefinition skill)
    {
        if (effectScope == EffectScope.AllAllies)
        {
            return "all allies";
        }

        if (effectScope == EffectScope.AllEnemies)
        {
            return "all enemies";
        }

        if (effectScope == EffectScope.Self)
        {
            return "on self";
        }

        if (effectScope == EffectScope.Default)
        {
            return skill.TargetKind switch
            {
                SkillTargetKind.Self => "on self",
                SkillTargetKind.OneAlly => "on ally",
                SkillTargetKind.SelfOrAlly => "on self or ally",
                SkillTargetKind.SelfAndAlly => "on self and ally",
                _ => string.Empty,
            };
        }

        return string.Empty;
    }

    private static IEnumerable<string> DescribePassiveModifiersForSkill(
        SkillDefinition skill,
        SkillDescriptionContext? context)
    {
        if (context?.Actor == null || context.BattleState == null)
        {
            yield break;
        }

        foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(context.Actor, context.BattleState))
        {
            var passivePhrase = DescribePassiveModifierForSkill(passiveDefinition, skill, context);
            if (!string.IsNullOrEmpty(passivePhrase))
            {
                yield return passivePhrase;
            }
        }
    }

    private static string DescribePassiveModifierForSkill(
        PassiveDefinition passiveDefinition,
        SkillDefinition skill,
        SkillDescriptionContext context)
    {
        return passiveDefinition.EffectKind switch
        {
            PassiveEffectKind.ExtraTokenOnSelfSkill
                when skill.TargetKind == SkillTargetKind.Self &&
                     string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.TokenType.HasValue =>
                $"passive: +{FormatTokenStackCount(Math.Max(1, passiveDefinition.IntValue))} " +
                $"{TokenDisplayName(passiveDefinition.TokenType.Value)}",

            PassiveEffectKind.ExtraHealPercentOnSelfSkill
                when skill.TargetKind == SkillTargetKind.Self &&
                     string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive > 0 =>
                $"passive: healing blocked outside village (+{FormatPlainNumber(passiveDefinition.Additive)}% HP)",

            PassiveEffectKind.DamageCausedVsSkillId
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {CombatDisplayTerms.FormatDamageCausedBonus(passiveDefinition.Additive)}",

            PassiveEffectKind.DamageCausedVsSkillIfTargetHasDot
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.DotType.HasValue &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {CombatDisplayTerms.FormatDamageCausedBonus(passiveDefinition.Additive)} if target has " +
                $"{DotDisplayName(passiveDefinition.DotType.Value)}",

            PassiveEffectKind.DamageCausedAfterPrerequisiteSkill
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {CombatDisplayTerms.FormatDamageCausedBonus(passiveDefinition.Additive)} after prep skill",

            PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.DotType.HasValue =>
                $"passive: applies extra {DotDisplayName(passiveDefinition.DotType.Value)} if target already has " +
                $"{DotDisplayName(passiveDefinition.DotType.Value)}",

            PassiveEffectKind.DotDurationBonus
                when passiveDefinition.DotType.HasValue &&
                     SkillAppliesDotType(skill, passiveDefinition.DotType.Value) &&
                     passiveDefinition.IntValue > 0 =>
                passiveDefinition.IntValue2 > 0
                    ? $"passive: {DotDisplayName(passiveDefinition.DotType.Value)} lasts +{FormatTurnCount(passiveDefinition.IntValue)} " +
                      $"(max. {FormatTurnCount(passiveDefinition.IntValue2)})"
                    : $"passive: {DotDisplayName(passiveDefinition.DotType.Value)} lasts +{FormatTurnCount(passiveDefinition.IntValue)}",

            PassiveEffectKind.DamageCausedPenaltyWhenToken
                when passiveDefinition.TokenType.HasValue &&
                     context.Actor!.Tokens.GetStacks(passiveDefinition.TokenType.Value) > 0 &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {CombatDisplayTerms.FormatDamageCausedBonus(passiveDefinition.Additive)} while you have " +
                $"{TokenDisplayName(passiveDefinition.TokenType.Value)}",

            PassiveEffectKind.DamageCausedVsDotOnTarget
                when HasDirectDamage(skill) &&
                     passiveDefinition.DotType.HasValue &&
                     context.PreviewTarget != null &&
                     PassiveRuleApplier.CountDotStacks(context.PreviewTarget, passiveDefinition.DotType.Value) > 0 =>
                DescribeDamageCausedVsDotOnTargetPassive(passiveDefinition),

            _ => string.Empty,
        };
    }

    private static string DescribeDamageCausedVsDotOnTargetPassive(PassiveDefinition passiveDefinition)
    {
        if (!passiveDefinition.DotType.HasValue)
        {
            return string.Empty;
        }

        var dotName = DotDisplayName(passiveDefinition.DotType.Value);
        if (passiveDefinition.AdditivePerStack > 0 && passiveDefinition.Cap > 0)
        {
            return
                $"passive: deal +{FormatPercentFromFraction(passiveDefinition.AdditivePerStack)} more damage per " +
                $"{dotName} stack on target (max. +{FormatPercentFromFraction(passiveDefinition.Cap)})";
        }

        if (passiveDefinition.Additive != 0)
        {
            return $"passive: {CombatDisplayTerms.FormatDamageCausedBonus(passiveDefinition.Additive)} against targets with {dotName}";
        }

        return string.Empty;
    }

    private static bool SkillAppliesDotType(SkillDefinition skill, DotType dotType)
    {
        foreach (var effect in skill.EffectsOnHit)
        {
            if (effect.Type == EffectType.ApplyDot && effect.Dot == dotType)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatTokenStackCount(int stacks) =>
        stacks == 1 ? "1" : stacks.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatTurnCount(int turns) => CombatDisplayTerms.FormatTurnCount(turns);

    private static string FormatPercentFromFraction(double fraction) =>
        CombatDisplayTerms.FormatPercentFromFraction(fraction);

    private static string FormatPlainNumber(double value) =>
        CombatDisplayTerms.FormatPlainNumber(value);

    public static string FormatStatusDisplayName(TokenType tokenType) =>
        CombatDisplayTerms.FormatTokenDisplayName(tokenType);

    private static string TokenDisplayName(TokenType tokenType) =>
        CombatDisplayTerms.FormatTokenDisplayName(tokenType);

    private static string DotDisplayName(DotType dotType) =>
        CombatDisplayTerms.FormatDotDisplayName(dotType);
}