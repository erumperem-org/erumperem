using System.Globalization;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Presentation;

/// <summary>
/// Player-facing summary for data-driven passives (conditions + effects).
/// </summary>
public static class PassivePlayerDescriptionBuilder
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.InvariantCulture;

    public static string BuildSummaryLine(
        PassiveDefinition passiveDefinition,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById = null)
    {
        if (passiveDefinition == null)
        {
            return string.Empty;
        }

        if (!passiveDefinition.HasDataDrivenEffects)
        {
            return string.Empty;
        }

        var conditionPhrase = DescribeConditions(passiveDefinition.Conditions, skillsById);
        var effectPhrase = DescribeEffects(passiveDefinition.Effects, skillsById);
        if (string.IsNullOrEmpty(effectPhrase))
        {
            return string.IsNullOrEmpty(conditionPhrase)
                ? passiveDefinition.Id
                : conditionPhrase + ".";
        }

        if (string.IsNullOrEmpty(conditionPhrase))
        {
            return Capitalize(effectPhrase) + ".";
        }

        return $"{conditionPhrase}: {effectPhrase}.";
    }

    private static string DescribeConditions(
        IReadOnlyList<PassiveConditionDefinition> conditions,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return string.Empty;
        }

        var phrases = new List<string>();
        foreach (var condition in conditions)
        {
            var phrase = DescribeCondition(condition, skillsById);
            if (!string.IsNullOrEmpty(phrase))
            {
                phrases.Add(phrase);
            }
        }

        return string.Join(" and ", phrases);
    }

    private static string DescribeCondition(
        PassiveConditionDefinition condition,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById)
    {
        var statusName = condition.RequiredStatus.HasValue
            ? SkillPlayerDescriptionBuilder.FormatStatusDisplayName(condition.RequiredStatus.Value)
            : DescribeStatusMatch(condition.StatusMatch);
        var skillName = FormatSkillName(condition.SkillId, skillsById);

        return condition.Activation switch
        {
            PassiveActivationKind.Permanent => string.Empty,
            PassiveActivationKind.WhileHavingStatus =>
                string.IsNullOrEmpty(statusName) ? "While you have a status" : $"While you have {statusName}",
            PassiveActivationKind.UponDamageTaken when condition.HitPointsLostPerTrigger > 0 =>
                $"For every {condition.HitPointsLostPerTrigger} HP lost",
            PassiveActivationKind.UponDamageTaken => "When you take damage",
            PassiveActivationKind.UponKill => "When you defeat an enemy",
            PassiveActivationKind.UponCriticalStrike => "When you land a critical hit",
            PassiveActivationKind.UponApplyingStatus =>
                string.IsNullOrEmpty(statusName) ? "When you apply a status" : $"When you apply {statusName}",
            PassiveActivationKind.UponReceivingStatus =>
                string.IsNullOrEmpty(statusName) ? "When you receive a status" : $"When you receive {statusName}",
            PassiveActivationKind.UponStatThreshold =>
                DescribeStatThreshold(condition),
            PassiveActivationKind.OnTurnEnd => "At the end of your turn",
            PassiveActivationKind.UponHittingTargetWithStatus when !string.IsNullOrEmpty(skillName) =>
                $"When {skillName} hits a target with {statusName}",
            PassiveActivationKind.UponHittingTargetWithStatus =>
                string.IsNullOrEmpty(statusName)
                    ? "When hitting a target that has a status"
                    : $"When hitting a target with {statusName}",
            PassiveActivationKind.UponDealingDamage => "When you deal damage",
            PassiveActivationKind.UponHealing => "When you heal",
            PassiveActivationKind.OnTurnStart => "At the start of your turn",
            PassiveActivationKind.WhileOppositeHasStatus =>
                string.IsNullOrEmpty(statusName)
                    ? "While the target has a status"
                    : $"While the target has {statusName}",
            PassiveActivationKind.UponUsingSkill when !string.IsNullOrEmpty(skillName) =>
                $"When using {skillName}",
            PassiveActivationKind.UponUsingSkill => "When you use a skill",
            PassiveActivationKind.UponAllyDamageTaken => "When an ally takes damage",
            PassiveActivationKind.UponAllyDealingDamage => "When an ally deals damage",
            _ => string.Empty,
        };
    }

    private static string DescribeStatThreshold(PassiveConditionDefinition condition)
    {
        if (condition.StatThresholdFraction <= 0)
        {
            return "When a stat threshold is crossed";
        }

        var percent = FormatPercentFromFraction(condition.StatThresholdFraction);
        return condition.StatThresholdComparison == PassiveStatThresholdComparison.AboveOrEqual
            ? $"When HP is at least {percent}"
            : $"When HP is {percent} or below";
    }

    private static string DescribeStatusMatch(PassiveStatusMatchKind statusMatch) =>
        statusMatch switch
        {
            PassiveStatusMatchKind.AnyBuff => "a buff",
            PassiveStatusMatchKind.AnyDebuff => "a debuff",
            PassiveStatusMatchKind.AnyStatus => "a status",
            _ => string.Empty,
        };

    private static string DescribeEffects(
        IReadOnlyList<PassiveEffectDefinition> effects,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById)
    {
        if (effects == null || effects.Count == 0)
        {
            return string.Empty;
        }

        var phrases = new List<string>();
        foreach (var effect in effects)
        {
            var phrase = DescribeEffect(effect, skillsById);
            if (!string.IsNullOrEmpty(phrase))
            {
                phrases.Add(phrase);
            }
        }

        return string.Join(", ", phrases);
    }

    private static string DescribeEffect(
        PassiveEffectDefinition effect,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById)
    {
        var chancePrefix = effect.ChanceToTrigger > 0 && effect.ChanceToTrigger < 0.9995
            ? $"{FormatPercentFromFraction(effect.ChanceToTrigger)} chance to "
            : string.Empty;

        var body = effect.Operation switch
        {
            PassiveEffectOperationKind.CharacterStatChange => DescribeCharacterStatChange(effect),
            PassiveEffectOperationKind.SkillStatChange => DescribeSkillStatChange(effect, skillsById),
            PassiveEffectOperationKind.TokenStatChange => DescribeTokenStatChange(effect),
            PassiveEffectOperationKind.ExtraStatsFromResource => DescribeExtraStatsFromResource(effect),
            PassiveEffectOperationKind.TokenManipulation => DescribeTokenManipulation(effect),
            PassiveEffectOperationKind.TurnManipulation => "gain an extra action",
            PassiveEffectOperationKind.Heal =>
                effect.Magnitude >= 1
                    ? $"heal {FormatPlainNumber(effect.Magnitude)} HP"
                    : $"heal {FormatPercentFromFraction(effect.Magnitude)} HP",
            PassiveEffectOperationKind.DealDamage =>
                $"deal {FormatPlainNumber(Math.Max(0, effect.Magnitude))} damage",
            PassiveEffectOperationKind.TriggerDestabilization => "trigger Destabilization",
            PassiveEffectOperationKind.CastSkill =>
                $"cast {FormatSkillName(effect.SkillId, skillsById)}",
            PassiveEffectOperationKind.SummonEnemy =>
                string.IsNullOrWhiteSpace(effect.SummonEnemyId)
                    ? "summon an enemy"
                    : $"summon {effect.SummonEnemyId}",
            _ => string.Empty,
        };

        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        if (effect.ExpiresAtEndOfOpposingSideTurn)
        {
            body += " until the end of the enemies' turn";
        }

        return chancePrefix + body;
    }

    private static string DescribeCharacterStatChange(PassiveEffectDefinition effect)
    {
        var statName = FormatCharacterStatName(effect.CharacterStat);
        if (string.IsNullOrEmpty(statName))
        {
            return string.Empty;
        }

        var magnitudeText = IsPercentCharacterStat(effect.CharacterStat)
            ? FormatSignedPercent(effect.Magnitude)
            : FormatSignedNumber(effect.Magnitude);
        var targetSuffix = DescribeStatChangeTarget(effect.StatChangeTarget);
        return string.IsNullOrEmpty(targetSuffix)
            ? $"{magnitudeText} {statName}"
            : $"{magnitudeText} {statName} {targetSuffix}";
    }

    private static string DescribeSkillStatChange(
        PassiveEffectDefinition effect,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById)
    {
        var skillName = FormatSkillName(effect.SkillId, skillsById);
        var skillPrefix = string.IsNullOrEmpty(skillName) ? "skills gain" : $"{skillName} gains";
        return effect.SkillStat switch
        {
            PassiveSkillStatKind.Damage => $"{skillPrefix} {FormatSignedPercent(effect.Magnitude)} damage",
            PassiveSkillStatKind.Accuracy => $"{skillPrefix} {FormatSignedPercent(effect.Magnitude)} accuracy",
            PassiveSkillStatKind.CorruptionCost => $"{skillPrefix} {FormatSignedNumber(effect.Magnitude)} corruption cost",
            PassiveSkillStatKind.CriticalChance => $"{skillPrefix} {FormatSignedPercent(effect.Magnitude)} crit chance",
            PassiveSkillStatKind.HitCount => $"{skillPrefix} +{Math.Max(1, effect.Stacks)} hit(s)",
            PassiveSkillStatKind.ChanceToNotEndTurn =>
                $"{skillPrefix} {FormatPercentFromFraction(effect.Magnitude)} chance to not end the turn",
            PassiveSkillStatKind.HealEffectiveness =>
                $"{skillPrefix} {FormatSignedPercent(effect.Magnitude)} healing",
            PassiveSkillStatKind.HealDoubleChance =>
                $"{FormatPercentFromFraction(effect.Magnitude)} chance to double the heal",
            _ => string.Empty,
        };
    }

    private static string DescribeTokenStatChange(PassiveEffectDefinition effect)
    {
        if (!effect.Token.HasValue)
        {
            return string.Empty;
        }

        var tokenName = SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.Token.Value);
        return $"{tokenName} tokens are {FormatSignedPercent(effect.Magnitude)} more effective";
    }

    private static string DescribeExtraStatsFromResource(PassiveEffectDefinition effect)
    {
        var statName = FormatCharacterStatName(effect.CharacterStat);
        if (string.IsNullOrEmpty(statName))
        {
            return string.Empty;
        }

        var bonus = IsPercentCharacterStat(effect.CharacterStat)
            ? FormatSignedPercent(effect.Magnitude)
            : FormatSignedNumber(effect.Magnitude);
        return $"{bonus} {statName} {DescribeResource(effect)}";
    }

    private static string DescribeResource(PassiveEffectDefinition effect) =>
        effect.Resource switch
        {
            PassiveResourceKind.MissingHitPointsFraction => "per missing HP fraction",
            PassiveResourceKind.CurrentHitPointsFraction => "per current HP fraction",
            PassiveResourceKind.TokenStacks when effect.ResourceToken.HasValue =>
                $"per {SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.ResourceToken.Value)} stack",
            PassiveResourceKind.TokenStacks when effect.Token.HasValue =>
                $"per {SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.Token.Value)} stack",
            PassiveResourceKind.MissingHitPointsChunks when effect.Stacks > 0 =>
                $"per {effect.Stacks} missing HP",
            PassiveResourceKind.OppositeTokenStacks when effect.Token.HasValue =>
                $"per {SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.Token.Value)} stack on the target",
            PassiveResourceKind.TokenStacksChunks when effect.Token.HasValue && effect.Stacks > 0 =>
                $"per {effect.Stacks} {SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.Token.Value)} stacks",
            PassiveResourceKind.EnemiesDefeatedThisBattle => "per enemy defeated this battle",
            _ => string.Empty,
        };

    private static string DescribeTokenManipulation(PassiveEffectDefinition effect)
    {
        if (!effect.Token.HasValue)
        {
            return string.Empty;
        }

        var tokenName = SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.Token.Value);
        var stackCount = effect.Stacks > 0 ? effect.Stacks : 1;
        var verb = effect.TokenManipulationMode == PassiveTokenManipulationMode.Remove
            ? "lose"
            : "gain";
        var targetSuffix = DescribeStatChangeTarget(effect.StatChangeTarget);

        if (effect.ScaleStacksPerSourceStack > 0)
        {
            var source = effect.ResourceToken.HasValue
                ? SkillPlayerDescriptionBuilder.FormatStatusDisplayName(effect.ResourceToken.Value)
                : "source";
            var scaled = effect.ScaleStacksSourceDivisor > 1
                ? $"{verb} {tokenName} equal to {source} stacks / {effect.ScaleStacksSourceDivisor}"
                : $"{verb} {tokenName} equal to {source} stacks";
            return string.IsNullOrEmpty(targetSuffix) ? scaled : $"{scaled} {targetSuffix}";
        }

        var phrase = $"{verb} {stackCount} {tokenName}";
        return string.IsNullOrEmpty(targetSuffix) ? phrase : $"{phrase} {targetSuffix}";
    }

    private static string DescribeStatChangeTarget(PassiveStatChangeTarget target) =>
        target switch
        {
            PassiveStatChangeTarget.Ally => "to an ally",
            PassiveStatChangeTarget.SelfAndAlly => "to self and ally",
            PassiveStatChangeTarget.Opposite => "to the target",
            PassiveStatChangeTarget.AllOpposites => "to all enemies",
            PassiveStatChangeTarget.EventRecipient => "to the recipient",
            _ => string.Empty,
        };

    private static string FormatCharacterStatName(PassiveCharacterStatKind statKind) =>
        statKind switch
        {
            PassiveCharacterStatKind.CurrentHitPoints => "HP",
            PassiveCharacterStatKind.MaximumHitPoints => "Max HP",
            PassiveCharacterStatKind.DefenseChance => "Defense",
            PassiveCharacterStatKind.DamageCaused => "Damage Caused",
            PassiveCharacterStatKind.Accuracy => "Accuracy",
            PassiveCharacterStatKind.CriticalChance => "Critical Chance",
            PassiveCharacterStatKind.CriticalDamage => "Critical Damage",
            _ => string.Empty,
        };

    private static bool IsPercentCharacterStat(PassiveCharacterStatKind statKind) =>
        statKind is PassiveCharacterStatKind.DefenseChance
            or PassiveCharacterStatKind.DamageCaused
            or PassiveCharacterStatKind.Accuracy
            or PassiveCharacterStatKind.CriticalChance
            or PassiveCharacterStatKind.CriticalDamage;

    private static string FormatSkillName(
        string? skillId,
        IReadOnlyDictionary<string, SkillDefinition>? skillsById)
    {
        if (string.IsNullOrWhiteSpace(skillId))
        {
            return string.Empty;
        }

        if (skillsById != null && skillsById.TryGetValue(skillId, out var skillDefinition))
        {
            return SkillPlayerDescriptionBuilder.TranslateToEnglish(skillDefinition.Name);
        }

        return skillId;
    }

    private static string FormatSignedPercent(double fraction)
    {
        var percentText = FormatPercentFromFraction(Math.Abs(fraction));
        return fraction < 0 ? $"-{percentText}" : $"+{percentText}";
    }

    private static string FormatSignedNumber(double value)
    {
        var formatted = FormatPlainNumber(Math.Abs(value));
        return value < 0 ? $"-{formatted}" : $"+{formatted}";
    }

    private static string FormatPercentFromFraction(double fraction) =>
        (fraction * 100.0).ToString("0.##", EnglishCulture) + "%";

    private static string FormatPlainNumber(double value) =>
        value.ToString("0.##", EnglishCulture);

    private static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
