using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;
using Game.Core.Presentation;
using Erumperem.Progression;
using UnityEngine;

namespace Erumperem.UI
{
    /// <summary>
    /// Camada única para transformar identificadores técnicos (enums, ids de skill) em texto legível para o jogador.
    /// O resultado final inclui expansão de marcas de autor (colchetes) para rich text TMP.
    /// </summary>
    public static class PlayerFacingText
    {
        private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
        private static Dictionary<string, string> _nodeIdToDisplayName;

        public static string PresentForUi(string rawTechnicalText, BattleState battleContext = null)
        {
            if (string.IsNullOrEmpty(rawTechnicalText))
            {
                return string.Empty;
            }

            EnsureNodeNameCache();
            var lines = rawTechnicalText.Replace("\r\n", "\n").Split('\n');
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                lines[lineIndex] = PresentLine(lines[lineIndex], battleContext);
            }

            var joined = string.Join("\n", lines);
            joined = AutoWrapKnownDomainTermsWithMarkup(joined);
            return PlayerGameRichText.ExpandAuthoringMarkupToTextMeshPro(joined);
        }

        private static string AutoWrapKnownDomainTermsWithMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            foreach (DotType dotType in Enum.GetValues(typeof(DotType)))
            {
                var displayName = FormatDotTypeDisplayName(dotType);
                text = WrapWordOutsideExistingMarkup(text, displayName, $"[dot {dotType.ToString().ToLowerInvariant()}]");
            }

            foreach (TokenType tokenType in Enum.GetValues(typeof(TokenType)))
            {
                var displayName = FormatTokenTypeDisplayName(tokenType);
                text = WrapWordOutsideExistingMarkup(
                    text,
                    displayName,
                    $"[token {tokenType.ToString().ToLowerInvariant()}]");
            }

            return text;
        }

        private static string WrapWordOutsideExistingMarkup(string text, string targetWord, string replacementMarkup)
        {
            if (string.IsNullOrEmpty(targetWord) || !text.Contains(targetWord, StringComparison.Ordinal))
            {
                return text;
            }

            var pattern = $@"(?<![<\[\w])({Regex.Escape(targetWord)})(?![\w>\]])";
            return Regex.Replace(text, pattern, replacementMarkup);
        }

        public static string ApplyRichMarkupForTextMeshPro(string alreadyLocalizedText) =>
            PlayerGameRichText.ExpandAuthoringMarkupToTextMeshPro(alreadyLocalizedText ?? string.Empty);

        public static string FormatSkillTreeNodeDescription(SkillTreeNodeAsset nodeAsset)
        {
            if (nodeAsset == null)
            {
                return string.Empty;
            }

            if (nodeAsset.IsPassiveNode)
            {
                return PresentForUi(BuildPassiveDescriptionFromAsset(nodeAsset));
            }

            try
            {
                var skillDefinition = nodeAsset.ToRuntimeSkillDefinition();
                return PresentForUi(SkillPlayerDescriptionBuilder.BuildSummaryLine(skillDefinition));
            }
            catch (InvalidOperationException)
            {
                var body = nodeAsset.DescriptionForUi;
                if (string.IsNullOrWhiteSpace(body))
                {
                    return nodeAsset.DisplayName;
                }

                return PresentForUi(body);
            }
        }

        public static string FormatCombatLogLine(BattleState state, CombatEvent combatEvent)
        {
            string line;
            if (combatEvent.EventType == BattleEventType.PassiveCombatNarrative)
            {
                line = FormatPassiveCombatNarrativeLine(state, combatEvent);
            }
            else if (combatEvent.EventType == BattleEventType.DotInflicted)
            {
                line = FormatDotInflictedLine(state, combatEvent);
            }
            else
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            return PlayerGameRichText.ExpandAuthoringMarkupToTextMeshPro(line);
        }

        private static string BuildPassiveDescriptionFromAsset(SkillTreeNodeAsset asset) =>
            DescribePassiveDefinitionInDetail(asset.ToRuntimePassiveDefinition());

        public static string DescribePassiveDefinitionInDetail(PassiveDefinition def)
        {
            var skillRef = FormatSkillReference(def.SkillId);
            var prerequisiteSkillRef = FormatSkillReference(def.PrerequisiteSkillId);
            var dotName = def.DotType.HasValue ? FormatDotTypeDisplayName(def.DotType.Value) : string.Empty;
            var grantedTokenName = def.GrantTokenType.HasValue
                ? FormatTokenTypeDisplayName(def.GrantTokenType.Value)
                : string.Empty;
            var additivePercent = FormatPercentFromFraction(def.Additive);
            var perStackPercent = FormatPercentFromFraction(def.AdditivePerStack);
            var capPercent = FormatPercentFromFraction(def.Cap);
            var hpThresholdPercent = FormatPercentFromFraction(def.HpBelowPercent);

            return def.EffectKind switch
            {
                PassiveEffectKind.OutgoingDamageVsSkillId =>
                    $"When using {skillRef}, deals +{additivePercent} damage.",

                PassiveEffectKind.OutgoingDamageVsDotOnTarget when def.AdditivePerStack > 0 && def.Cap > 0 =>
                    $"Deals +{perStackPercent} damage against targets with {dotName} for each stack of {dotName} " +
                    $"(up to +{capPercent}).",

                PassiveEffectKind.OutgoingDamageVsDotOnTarget =>
                    $"Deals +{additivePercent} damage against targets with {dotName}.",

                PassiveEffectKind.DotDurationBonus when def.IntValue2 > 0 =>
                    $"Your {dotName} effects last +{FormatTurnCountWithUnit(def.IntValue)} " +
                    $"(up to a maximum of {FormatTurnCountWithUnit(def.IntValue2)}).",

                PassiveEffectKind.DotDurationBonus =>
                    $"Your effects of {dotName} last +{FormatTurnCountWithUnit(def.IntValue)}.",

                PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow =>
                    FormatIncomingDamageMultiplierBelowHp(def.Additive, hpThresholdPercent),

                PassiveEffectKind.OutgoingDamagePenaltyWhenToken =>
                    FormatOutgoingDamageWhileTokenIsActive(def.Additive, def.TokenType),

                PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill =>
                    $"After using {prerequisiteSkillRef}, the next {skillRef} deals +{additivePercent} damage.",

                PassiveEffectKind.ExtraTokenOnSelfSkill =>
                    $"When using {skillRef} on self, gains " +
                    $"{FormatTokenStackCountWithUnit(Math.Max(1, def.IntValue))} additional " +
                    $"{(def.TokenType.HasValue ? FormatTokenTypeDisplayName(def.TokenType.Value) : "token")}.",

                PassiveEffectKind.ExtraHealPercentOnSelfSkill =>
                    $"When using {skillRef} on self, would restore {FormatPlainPercent(def.Additive)} of Max HP, " +
                    "but healing is blocked outside of town.",

                PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot =>
                    FormatApplyExtraDot(def, skillRef, dotName),

                PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot =>
                    $"When using {skillRef} against a target with {dotName}, deals +{additivePercent} damage.",

                PassiveEffectKind.DotTickDamageBonusWhenTargetHpBelow =>
                    $"Your {dotName} effects deal +{additivePercent} damage per turn while " +
                    $"the target's HP is below {hpThresholdPercent}.",

                PassiveEffectKind.GrantTokenAtTurnStartIfCondition =>
                    FormatGrantTokenAtTurnStart(def, grantedTokenName),

                _ => DescribePassiveEffectKind(def.EffectKind),
            };
        }

        private static string FormatSkillReference(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return "this skill";
            }

            return $"«{NodeOrSkillDisplayName(skillId)}»";
        }

        private static string FormatIncomingDamageMultiplierBelowHp(double multiplier, string hpThresholdPercent)
        {
            if (multiplier <= 0)
            {
                return $"While your HP is below {hpThresholdPercent}, incoming damage is modified.";
            }

            if (multiplier < 1)
            {
                var damageReductionPercent = FormatPercentFromFraction(1 - multiplier);
                return $"While your HP is below {hpThresholdPercent}, you take {damageReductionPercent} " +
                       $"less damage (multiplier ×{FormatMultiplier(multiplier)}).";
            }

            var extraDamagePercent = FormatPercentFromFraction(multiplier - 1);
            return $"While your HP is below {hpThresholdPercent}, you receive {extraDamagePercent} " +
                   $"more damage (multiplier ×{FormatMultiplier(multiplier)}).";
        }

        private static string FormatOutgoingDamageWhileTokenIsActive(double additive, TokenType? tokenType)
        {
            var tokenName = tokenType.HasValue ? FormatTokenTypeDisplayName(tokenType.Value) : "this token";
            if (additive < 0)
            {
                var penaltyPercent = FormatPercentFromFraction(-additive);
                return $"Deals {penaltyPercent} less damage while carrying {tokenName}.";
            }

            var bonusPercent = FormatPercentFromFraction(additive);
            return $"Deals +{bonusPercent} damage while carrying {tokenName}.";
        }

        private static string FormatApplyExtraDot(PassiveDefinition def, string skillRef, string dotName)
        {
            var potency = def.IntValue > 0 ? def.IntValue : 2;
            var duration = def.IntValue2 > 0 ? def.IntValue2 : 2;
            return $"After using {skillRef} on a target already suffering from {dotName}, applies extra {dotName} " +
                   $"({potency} damage per turn, for {FormatTurnCountWithUnit(duration)}).";
        }

        private static string FormatGrantTokenAtTurnStart(PassiveDefinition def, string grantedTokenName)
        {
            if (def.GrantTokenType is null)
            {
                return "At the start of your turn, gain a token based on configured conditions.";
            }

            var stacks = Math.Max(1, def.IntValue);
            var hasRequirement = def.IfHasTokenType.HasValue;
            var hasBlocker = def.UnlessHasTokenType.HasValue;
            var requirementName = hasRequirement
                ? FormatTokenTypeDisplayName(def.IfHasTokenType!.Value)
                : string.Empty;
            var blockerName = hasBlocker
                ? FormatTokenTypeDisplayName(def.UnlessHasTokenType!.Value)
                : string.Empty;

            var lead = $"At the start of your turn, gain {FormatTokenStackCountWithUnit(stacks)} of {grantedTokenName}";
            if (hasRequirement && hasBlocker)
            {
                return $"{lead} if you have {requirementName} and do not have {blockerName}.";
            }

            if (hasRequirement)
            {
                return $"{lead} if you have {requirementName}.";
            }

            if (hasBlocker)
            {
                return $"{lead}, unless you have {blockerName}.";
            }

            return $"{lead}.";
        }

        public static string FormatTokenTypeDisplayName(TokenType tokenType) =>
            tokenType switch
            {
                TokenType.Block => "Block",
                TokenType.BlockPlus => "Block+",
                TokenType.Dodge => "Dodge",
                TokenType.Blind => "Blind",
                TokenType.Taunt => "Taunt",
                TokenType.Stealth => "Stealth",
                TokenType.Combo => "Combo",
                TokenType.Stun => "Stun",
                _ => tokenType.ToString(),
            };

        public static string FormatTokenTypeDisplayName(string tokenTypeName)
        {
            if (string.IsNullOrEmpty(tokenTypeName)) return string.Empty;

            if (Enum.TryParse<TokenType>(tokenTypeName, ignoreCase: true, out var parsed))
            {
                return FormatTokenTypeDisplayName(parsed);
            }

            return tokenTypeName;
        }

        private static string FormatPlainPercent(double percentValue) =>
            percentValue.ToString("0.##", CultureInfo.InvariantCulture) + "%";

        private static string FormatTokenStackCount(int stackCount) =>
            stackCount == 1 ? "1" : stackCount.ToString(CultureInfo.InvariantCulture);

        private static string FormatTokenStackCountWithUnit(int stackCount) =>
            stackCount == 1 ? "1 stack" : $"{stackCount} stacks";

        private static string FormatTurnCountWithUnit(int turnCount) =>
            turnCount == 1 ? "1 turn" : $"{turnCount} turns";

        public static string DescribePassiveEffectKind(PassiveEffectKind kind) =>
            kind switch
            {
                PassiveEffectKind.OutgoingDamageVsSkillId =>
                    "Increases damage dealt when using a specific skill.",
                PassiveEffectKind.OutgoingDamageVsDotOnTarget =>
                    "Increases damage against targets suffering from a specific damage-over-time (DoT) effect.",
                PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot =>
                    "Increases damage of a specific skill if the target already carries a specific DoT.",
                PassiveEffectKind.DotDurationBonus => "Increases the duration of a DoT applied by you.",
                PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow =>
                    "Modifies incoming damage when your HP is below a threshold.",
                PassiveEffectKind.OutgoingDamagePenaltyWhenToken =>
                    "Modifies your damage while you carry certain tokens.",
                PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill =>
                    "After using a setup skill, the next cast of another skill gains bonus damage.",
                PassiveEffectKind.ExtraTokenOnSelfSkill =>
                    "Gain extra tokens when using specific self-targeted skills.",
                PassiveEffectKind.ExtraHealPercentOnSelfSkill =>
                    "Healing blocked outside of town; HP is only recovered in the village sanctuary.",
                PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot =>
                    "Applies an extra DOT when the target is already suffering from that DOT type.",
                PassiveEffectKind.DotTickDamageBonusWhenTargetHpBelow =>
                    "Increases tick damage of your DOTs when the target's HP is low.",
                PassiveEffectKind.GrantTokenAtTurnStartIfCondition =>
                    "At the start of your turn, receive tokens if conditions are met.",
                _ => kind.ToString(),
            };

        public static string FormatDotTypeDisplayName(string dotTypeName)
        {
            if (string.IsNullOrEmpty(dotTypeName)) return string.Empty;

            if (Enum.TryParse<DotType>(dotTypeName, ignoreCase: true, out var parsed))
            {
                return FormatDotTypeDisplayName(parsed);
            }

            return dotTypeName;
        }

        public static string FormatDotTypeDisplayName(DotType dotType) =>
            dotType switch
            {
                DotType.Bleed => "Bleed",
                DotType.Blight => "Blight",
                DotType.Burn => "Burn",
                _ => dotType.ToString(),
            };

        private static string FormatPassiveCombatNarrativeLine(BattleState state, CombatEvent combatEvent)
        {
            EnsureNodeNameCache();
            var passiveLabel = NodeOrSkillDisplayName(combatEvent.PassiveId);
            if (string.IsNullOrEmpty(passiveLabel))
            {
                passiveLabel = combatEvent.PassiveId;
            }

            if (!Enum.TryParse<PassiveEffectKind>(combatEvent.PassiveEffectKindName, out var kind))
            {
                return $"Passive «{passiveLabel}» activated.";
            }

            var relatedSkill = NodeOrSkillDisplayName(combatEvent.PassiveRelatedSkillId);
            var bonusPct = FormatPercentFromFraction(combatEvent.PassiveMagnitude);

            return kind switch
            {
                PassiveEffectKind.OutgoingDamageVsSkillId or
                    PassiveEffectKind.OutgoingDamageVsDotOnTarget or
                    PassiveEffectKind.OutgoingDamagePenaltyWhenToken or
                    PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill or
                    PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot =>
                    $"Passive «{passiveLabel}»: +{bonusPct} damage on this hit " +
                    $"{(string.IsNullOrEmpty(relatedSkill) ? string.Empty : $"(skill «{relatedSkill}»)")}.",
                PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow =>
                    $"Passive «{passiveLabel}»: incoming damage ×{FormatMultiplier(combatEvent.PassiveMagnitude)} (low HP).",
                PassiveEffectKind.ExtraHealPercentOnSelfSkill when combatEvent.PassiveAuxInt > 0 =>
                    $"Passive «{passiveLabel}»: healing blocked outside town ({combatEvent.PassiveAuxInt} HP, {bonusPct} of max).",
                PassiveEffectKind.ExtraHealPercentOnSelfSkill =>
                    $"Passive «{passiveLabel}»: healing blocked outside town ({bonusPct} of max).",
                _ =>
                    $"Passive «{passiveLabel}»: {DescribePassiveEffectKind(kind)}",
            };
        }

        private static string FormatDotInflictedLine(BattleState state, CombatEvent combatEvent)
        {
            var targetName = DisplayCombatantName(state, combatEvent.TargetId);
            var dotName = FormatDotTypeDisplayName(combatEvent.DotType);
            var source = string.IsNullOrEmpty(combatEvent.PassiveId)
                ? string.Empty
                : $" (passive «{NodeOrSkillDisplayName(combatEvent.PassiveId)}»)";
            return $"{targetName} suffers {dotName} ({combatEvent.DotAmount}/turn, {combatEvent.DotDurationTurns} turns){source}.";
        }

        public static string FormatCombatantSpawnedLine(BattleState state, CombatEvent combatEvent)
        {
            if (combatEvent.EventType != BattleEventType.CombatantSpawned)
            {
                return string.Empty;
            }

            var summonerName = DisplayCombatantName(state, combatEvent.ActorId);
            var summonedName = DisplayCombatantName(state, combatEvent.TargetId);
            if (string.IsNullOrEmpty(summonedName))
            {
                summonedName = "uma fada corrompida";
            }

            return $"{summonerName} invoca {summonedName}!";
        }

        private static string DisplayCombatantName(BattleState state, string combatantId)
        {
            if (state == null || string.IsNullOrEmpty(combatantId))
            {
                return combatantId ?? string.Empty;
            }

            foreach (var combatant in state.GetAllCombatants())
            {
                if (combatant.Identity.Id == combatantId)
                {
                    return combatant.Identity.DisplayName;
                }
            }

            return combatantId;
        }

        private static void EnsureNodeNameCache()
        {
            if (_nodeIdToDisplayName != null) return;

            _nodeIdToDisplayName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var asset in Resources.LoadAll<SkillTreeNodeAsset>("SkillTreeNodes"))
            {
                if (string.IsNullOrEmpty(asset.NodeId)) continue;
                _nodeIdToDisplayName[asset.NodeId] = asset.DisplayName;
            }
        }

        private static string NodeOrSkillDisplayName(string nodeOrSkillId)
        {
            if (string.IsNullOrEmpty(nodeOrSkillId)) return string.Empty;
            EnsureNodeNameCache();
            return _nodeIdToDisplayName.TryGetValue(nodeOrSkillId, out var name) ? name : nodeOrSkillId;
        }

        private static string PresentLine(string line, BattleState battleContext)
        {
            if (string.IsNullOrEmpty(line)) return line;

            var trimmed = line.Trim();
            foreach (PassiveEffectKind kind in Enum.GetValues(typeof(PassiveEffectKind)))
            {
                var technical = kind.ToString();
                if (!trimmed.Contains(technical, StringComparison.Ordinal)) continue;

                var friendlyShort = DescribePassiveEffectKind(kind);
                trimmed = Regex.Replace(
                    trimmed,
                    Regex.Escape(technical),
                    friendlyShort,
                    RegexOptions.IgnoreCase);
            }

            trimmed = Regex.Replace(
                trimmed,
                @"(?i)\bskill\s*:\s*(\S+)",
                match => $"Skill: '{NodeOrSkillDisplayName(match.Groups[1].Value)}'");

            trimmed = Regex.Replace(
                trimmed,
                @"\b([a-z]+_[a-z0-9_]+)\b",
                match => NodeOrSkillDisplayName(match.Groups[1].Value));

            trimmed = Regex.Replace(
                trimmed,
                @"(?<=\d)\.(?=\d)",
                ".");

            return trimmed;
        }

        private static string FormatPercentFromFraction(double fraction) =>
            (fraction * 100.0).ToString("0.##", CultureInfo.InvariantCulture) + "%";

        private static string FormatMultiplier(double factor) =>
            factor.ToString("0.##", CultureInfo.InvariantCulture);
    }
}