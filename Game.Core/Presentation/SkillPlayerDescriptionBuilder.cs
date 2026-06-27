using System.Globalization;
using System.Text;
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
    private static readonly CultureInfo EnglishCulture = CultureInfo.InvariantCulture;

    public sealed class SkillDescriptionContext
    {
        public Combatant? Actor { get; init; }
        public BattleState? BattleState { get; init; }
        public Combatant? PreviewTarget { get; init; }
    }

    public static string BuildSummaryLine(SkillDefinition skill, SkillDescriptionContext? context = null)
    {
        if (skill == null)
        {
            return string.Empty;
        }

        var detailParts = new List<string>
        {
            DescribeTarget(skill),
            DescribeDirectDamage(skill),
        };

        var hitChancePart = DescribeHitChance(skill, context);
        if (!string.IsNullOrEmpty(hitChancePart))
        {
            detailParts.Add(hitChancePart);
        }

        if (ShouldShowCriticalChance(skill, context))
        {
            detailParts.Add(DescribeCriticalChance(skill, context));
        }

        var effectsPart = DescribeEffects(skill.EffectsOnHit, skill, context, prefix: null);
        if (!string.IsNullOrEmpty(effectsPart))
        {
            detailParts.Add(effectsPart);
        }

        if (skill.ComboBonus.Count > 0)
        {
            var comboPart = DescribeEffects(skill.ComboBonus, skill, context, prefix: "with combo");
            if (!string.IsNullOrEmpty(comboPart))
            {
                detailParts.Add(comboPart);
            }
        }

        detailParts.Add(DescribeCorruptionCost(skill));

        var passiveParts = DescribePassiveModifiersForSkill(skill, context);
        detailParts.AddRange(passiveParts);

        return $"{skill.Name}: {string.Join(" | ", detailParts.Where(part => !string.IsNullOrEmpty(part)))}.";
    }

    private static string DescribeTarget(SkillDefinition skill) =>
        skill.TargetKind switch
        {
            SkillTargetKind.Enemy => "1 target",
            SkillTargetKind.Ally => "1 ally",
            SkillTargetKind.Self => "self",
            _ => "1 target",
        };

    private static bool HasDirectDamage(SkillDefinition skill) =>
        skill.BaseDamage.Min > 0 || skill.BaseDamage.Max > 0;

    private static string DescribeDirectDamage(SkillDefinition skill)
    {
        if (!HasDirectDamage(skill))
        {
            return "no direct damage";
        }

        var minimumDamage = skill.BaseDamage.Min;
        var maximumDamage = skill.BaseDamage.Max;
        if (minimumDamage == maximumDamage)
        {
            return $"{minimumDamage} damage";
        }

        return $"{minimumDamage}–{maximumDamage} damage";
    }

    private static string DescribeHitChance(SkillDefinition skill, SkillDescriptionContext? context)
    {
        var actorAccuracy = context?.Actor?.Stats.Accuracy ?? 1.0;
        var combinedHitChance = skill.Accuracy * actorAccuracy;
        if (combinedHitChance >= 0.9995)
        {
            return string.Empty;
        }

        return $"{FormatPercentFromFraction(combinedHitChance)} hit chance";
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
        SkillDescriptionContext? context,
        string? prefix)
    {
        if (effects == null || effects.Count == 0)
        {
            return string.Empty;
        }

        var effectPhrases = new List<string>();
        foreach (var effect in effects)
        {
            var phrase = DescribeSingleEffect(effect, skill, context);
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
        SkillDefinition skill,
        SkillDescriptionContext? context)
    {
        var chancePrefix = effect.Chance < 0.9995
            ? $"{FormatPercentFromFraction(effect.Chance)} "
            : string.Empty;

        return effect.Type switch
        {
            EffectType.ApplyToken when effect.Token.HasValue =>
                $"{chancePrefix}{FormatTokenGrantPhrase(effect, skill)}",
            EffectType.ApplyDot when effect.Dot.HasValue =>
                $"{chancePrefix}{FormatDotGrantPhrase(effect, skill, context)}",
            EffectType.HealHpPercent =>
                $"{chancePrefix}healing blocked outside village ({FormatPlainNumber(Math.Max(0, effect.Potency))}% HP)",
            EffectType.HealHp =>
                $"{chancePrefix}healing blocked outside the village ({Math.Max(0, effect.Potency)} HP)",
            EffectType.Push =>
                $"{chancePrefix}pushes {Math.Max(1, Math.Abs(effect.Steps))} position(s)",
            EffectType.Pull =>
                $"{chancePrefix}pulls {Math.Max(1, Math.Abs(effect.Steps))} position(s)",
            EffectType.ApplyStun =>
                $"{chancePrefix}{FormatTokenStackCount(Math.Max(1, effect.Stacks))} {TokenDisplayName(TokenType.Stun)}",
            _ => string.Empty,
        };
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

    private static string FormatDotGrantPhrase(
        EffectSpec effect,
        SkillDefinition skill,
        SkillDescriptionContext? context)
    {
        var dotName = DotDisplayName(effect.Dot!.Value);
        var potency = Math.Max(1, effect.Potency);
        var baseDuration = Math.Max(1, effect.Duration);
        var duration = baseDuration;
        if (context?.BattleState != null && context.Actor != null)
        {
            duration = PassiveRuleApplier.AdjustDotDuration(
                context.BattleState,
                context.Actor,
                effect.Dot.Value,
                baseDuration);
        }

        return $"{dotName} ({potency} damage for {FormatTurnCount(duration)})";
    }

    private static string DescribeEffectScopePrefix(string effectScope, SkillDefinition skill)
    {
        if (string.Equals(effectScope, "AllAllies", StringComparison.OrdinalIgnoreCase))
        {
            return "all allies";
        }

        if (string.Equals(effectScope, "Self", StringComparison.OrdinalIgnoreCase))
        {
            return "on self";
        }

        if (string.Equals(effectScope, "Default", StringComparison.OrdinalIgnoreCase))
        {
            return skill.TargetKind switch
            {
                SkillTargetKind.Self => "on self",
                SkillTargetKind.Ally => "on ally",
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

            PassiveEffectKind.OutgoingDamageVsSkillId
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {FormatSignedPercentBonus(passiveDefinition.Additive)} damage",

            PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.DotType.HasValue &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {FormatSignedPercentBonus(passiveDefinition.Additive)} damage if target has " +
                $"{DotDisplayName(passiveDefinition.DotType.Value)}",

            PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {FormatSignedPercentBonus(passiveDefinition.Additive)} damage after prep skill",

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

            PassiveEffectKind.OutgoingDamagePenaltyWhenToken
                when passiveDefinition.TokenType.HasValue &&
                     context.Actor!.Tokens.GetStacks(passiveDefinition.TokenType.Value) > 0 &&
                     passiveDefinition.Additive != 0 =>
                $"passive: {FormatSignedPercentBonus(passiveDefinition.Additive)} damage with " +
                $"{TokenDisplayName(passiveDefinition.TokenType.Value)}",

            PassiveEffectKind.OutgoingDamageVsDotOnTarget
                when HasDirectDamage(skill) &&
                     passiveDefinition.DotType.HasValue &&
                     context.PreviewTarget != null &&
                     PassiveRuleApplier.CountDotStacks(context.PreviewTarget, passiveDefinition.DotType.Value) > 0 =>
                DescribeOutgoingDamageVsDotOnTargetPassive(passiveDefinition),

            _ => string.Empty,
        };
    }

    private static string DescribeOutgoingDamageVsDotOnTargetPassive(PassiveDefinition passiveDefinition)
    {
        if (!passiveDefinition.DotType.HasValue)
        {
            return string.Empty;
        }

        var dotName = DotDisplayName(passiveDefinition.DotType.Value);
        if (passiveDefinition.AdditivePerStack > 0 && passiveDefinition.Cap > 0)
        {
            return
                $"passive: +{FormatPercentFromFraction(passiveDefinition.AdditivePerStack)} damage per " +
                $"{dotName} stack on target (max. +{FormatPercentFromFraction(passiveDefinition.Cap)})";
        }

        if (passiveDefinition.Additive != 0)
        {
            return $"passive: {FormatSignedPercentBonus(passiveDefinition.Additive)} damage against target with {dotName}";
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

        foreach (var effect in skill.ComboBonus)
        {
            if (effect.Type == EffectType.ApplyDot && effect.Dot == dotType)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatSignedPercentBonus(double additiveFraction)
    {
        if (additiveFraction >= 0)
        {
            return $"+{FormatPercentFromFraction(additiveFraction)}";
        }

        return FormatPercentFromFraction(additiveFraction);
    }

    private static string FormatTokenStackCount(int stacks) => stacks == 1 ? "1" : stacks.ToString(CultureInfo.InvariantCulture);

    private static string FormatTurnCount(int turns) =>
        turns == 1 ? "1 turn" : $"{turns} turns";

    private static string FormatPercentFromFraction(double fraction) =>
        (fraction * 100.0).ToString("0.##", EnglishCulture) + "%";

    private static string FormatPlainNumber(double value) =>
        value.ToString("0.##", EnglishCulture);

    private static string TokenDisplayName(TokenType tokenType) =>
        tokenType switch
        {
            TokenType.Block => "Block",
            TokenType.BlockPlus => "Block Plus",
            TokenType.Dodge => "Dodge",
            TokenType.Blind => "Blind",
            TokenType.Taunt => "Taunt",
            TokenType.Stealth => "Stealth",
            TokenType.Combo => "Combo",
            TokenType.Stun => "Stun",
            _ => tokenType.ToString(),
        };

    private static string DotDisplayName(DotType dotType) =>
        dotType switch
        {
            DotType.Bleed => "Bleed",
            DotType.Blight => "Blight",
            DotType.Burn => "Burn",
            _ => dotType.ToString(),
        };
}