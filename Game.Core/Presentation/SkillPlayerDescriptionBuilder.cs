using System.Globalization;
using System.Text;
using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Presentation;

/// <summary>
/// Gera a linha resumo de uma skill para UI (ex.: "Execução de leilão: 1 alvo | 10–16 de dano | 12% de crít.").
/// </summary>
public static class SkillPlayerDescriptionBuilder
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");

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
            var comboPart = DescribeEffects(skill.ComboBonus, skill, context, prefix: "com combo");
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
            SkillTargetKind.Enemy => "1 alvo",
            SkillTargetKind.Ally => "1 aliado",
            SkillTargetKind.Self => "ti (auto)",
            _ => "1 alvo",
        };

    private static bool HasDirectDamage(SkillDefinition skill) =>
        skill.BaseDamage.Min > 0 || skill.BaseDamage.Max > 0;

    private static string DescribeDirectDamage(SkillDefinition skill)
    {
        if (!HasDirectDamage(skill))
        {
            return "sem dano direto";
        }

        var minimumDamage = skill.BaseDamage.Min;
        var maximumDamage = skill.BaseDamage.Max;
        if (minimumDamage == maximumDamage)
        {
            return $"{minimumDamage} de dano";
        }

        return $"{minimumDamage}–{maximumDamage} de dano";
    }

    private static string DescribeHitChance(SkillDefinition skill, SkillDescriptionContext? context)
    {
        var actorAccuracy = context?.Actor?.Stats.Accuracy ?? 1.0;
        var combinedHitChance = skill.Accuracy * actorAccuracy;
        if (combinedHitChance >= 0.9995)
        {
            return string.Empty;
        }

        return $"{FormatPercentFromFraction(combinedHitChance)} de acerto";
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
        $"{FormatPercentFromFraction(ComputeEffectiveCriticalChanceFraction(skill, context))} de crít";

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
            return "sem corrupção";
        }

        if (corruptionCost > 0)
        {
            return $"+{FormatPlainNumber(corruptionCost)} corrupção";
        }

        return $"{FormatPlainNumber(corruptionCost)} corrupção";
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
            ? $"{FormatPercentFromFraction(effect.Chance)} de "
            : string.Empty;

        return effect.Type switch
        {
            EffectType.ApplyToken when effect.Token.HasValue =>
                $"{chancePrefix}{FormatTokenGrantPhrase(effect, skill)}",
            EffectType.ApplyDot when effect.Dot.HasValue =>
                $"{chancePrefix}{FormatDotGrantPhrase(effect, skill, context)}",
            EffectType.HealHpPercent =>
                $"{chancePrefix}cura bloqueada fora da vila ({FormatPlainNumber(Math.Max(0, effect.Potency))}% HP)",
            EffectType.HealHp =>
                $"{chancePrefix}cura bloqueada fora da vila ({Math.Max(0, effect.Potency)} HP)",
            EffectType.Push =>
                $"{chancePrefix}empurra {Math.Max(1, Math.Abs(effect.Steps))} posição",
            EffectType.Pull =>
                $"{chancePrefix}puxa {Math.Max(1, Math.Abs(effect.Steps))} posição",
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
        if (skill.TargetKind == SkillTargetKind.Self && scopePrefix == "em ti")
        {
            scopePrefix = string.Empty;
        }

        return string.IsNullOrEmpty(scopePrefix)
            ? $"+{stackPhrase} {tokenName}"
            : $"+{stackPhrase} {tokenName} ({scopePrefix})";
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

        return $"{dotName} ({potency} de dano por {FormatTurnCount(duration)})";
    }

    private static string DescribeEffectScopePrefix(string effectScope, SkillDefinition skill)
    {
        if (string.Equals(effectScope, "AllAllies", StringComparison.OrdinalIgnoreCase))
        {
            return "todos os aliados";
        }

        if (string.Equals(effectScope, "Self", StringComparison.OrdinalIgnoreCase))
        {
            return "em ti";
        }

        if (string.Equals(effectScope, "Default", StringComparison.OrdinalIgnoreCase))
        {
            return skill.TargetKind switch
            {
                SkillTargetKind.Self => "em ti",
                SkillTargetKind.Ally => "no aliado",
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
                $"passiva: +{FormatTokenStackCount(Math.Max(1, passiveDefinition.IntValue))} " +
                $"{TokenDisplayName(passiveDefinition.TokenType.Value)}",

            PassiveEffectKind.ExtraHealPercentOnSelfSkill
                when skill.TargetKind == SkillTargetKind.Self &&
                     string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive > 0 =>
                $"passiva: cura bloqueada fora da vila (+{FormatPlainNumber(passiveDefinition.Additive)}% HP)",

            PassiveEffectKind.OutgoingDamageVsSkillId
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive != 0 =>
                $"passiva: {FormatSignedPercentBonus(passiveDefinition.Additive)} de dano",

            PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.DotType.HasValue &&
                     passiveDefinition.Additive != 0 =>
                $"passiva: {FormatSignedPercentBonus(passiveDefinition.Additive)} de dano se alvo tiver " +
                $"{DotDisplayName(passiveDefinition.DotType.Value)}",

            PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.Additive != 0 =>
                $"passiva: {FormatSignedPercentBonus(passiveDefinition.Additive)} de dano após skill preparatória",

            PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot
                when string.Equals(passiveDefinition.SkillId, skill.Id, StringComparison.Ordinal) &&
                     passiveDefinition.DotType.HasValue =>
                $"passiva: aplica {DotDisplayName(passiveDefinition.DotType.Value)} extra se alvo já tiver " +
                $"{DotDisplayName(passiveDefinition.DotType.Value)}",

            PassiveEffectKind.DotDurationBonus
                when passiveDefinition.DotType.HasValue &&
                     SkillAppliesDotType(skill, passiveDefinition.DotType.Value) &&
                     passiveDefinition.IntValue > 0 =>
                passiveDefinition.IntValue2 > 0
                    ? $"passiva: {DotDisplayName(passiveDefinition.DotType.Value)} dura +{FormatTurnCount(passiveDefinition.IntValue)} " +
                      $"(máx. {FormatTurnCount(passiveDefinition.IntValue2)})"
                    : $"passiva: {DotDisplayName(passiveDefinition.DotType.Value)} dura +{FormatTurnCount(passiveDefinition.IntValue)}",

            PassiveEffectKind.OutgoingDamagePenaltyWhenToken
                when passiveDefinition.TokenType.HasValue &&
                     context.Actor!.Tokens.GetStacks(passiveDefinition.TokenType.Value) > 0 &&
                     passiveDefinition.Additive != 0 =>
                $"passiva: {FormatSignedPercentBonus(passiveDefinition.Additive)} de dano com " +
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
                $"passiva: +{FormatPercentFromFraction(passiveDefinition.AdditivePerStack)} de dano por acúmulo de " +
                $"{dotName} no alvo (máx. +{FormatPercentFromFraction(passiveDefinition.Cap)})";
        }

        if (passiveDefinition.Additive != 0)
        {
            return $"passiva: {FormatSignedPercentBonus(passiveDefinition.Additive)} de dano contra alvo com {dotName}";
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
        turns == 1 ? "1 turno" : $"{turns} turnos";

    private static string FormatPercentFromFraction(double fraction) =>
        (fraction * 100.0).ToString("0.##", BrazilianCulture) + "%";

    private static string FormatPlainNumber(double value) =>
        value.ToString("0.##", BrazilianCulture);

    private static string TokenDisplayName(TokenType tokenType) =>
        tokenType switch
        {
            TokenType.Block => "Bloqueio",
            TokenType.BlockPlus => "Bloqueio Reforçado",
            TokenType.Dodge => "Esquiva",
            TokenType.Blind => "Cegueira",
            TokenType.Taunt => "Provocação",
            TokenType.Stealth => "Furtividade",
            TokenType.Combo => "Combo",
            TokenType.Stun => "Atordoamento",
            _ => tokenType.ToString(),
        };

    private static string DotDisplayName(DotType dotType) =>
        dotType switch
        {
            DotType.Bleed => "Sangramento",
            DotType.Blight => "Praga",
            DotType.Burn => "Fogo",
            _ => dotType.ToString(),
        };
}
