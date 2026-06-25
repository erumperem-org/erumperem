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
                    $"Ao usar {skillRef}, causa +{additivePercent} de dano.",

                PassiveEffectKind.OutgoingDamageVsDotOnTarget when def.AdditivePerStack > 0 && def.Cap > 0 =>
                    $"Causa +{perStackPercent} de dano contra alvos com {dotName} para cada acúmulo de {dotName} " +
                    $"(até +{capPercent}).",

                PassiveEffectKind.OutgoingDamageVsDotOnTarget =>
                    $"Causa +{additivePercent} de dano contra alvos com {dotName}.",

                PassiveEffectKind.DotDurationBonus when def.IntValue2 > 0 =>
                    $"Seus efeitos de {dotName} duram +{FormatTurnCountWithUnit(def.IntValue)} " +
                    $"(até o máximo de {FormatTurnCountWithUnit(def.IntValue2)}).",

                PassiveEffectKind.DotDurationBonus =>
                    $"Seus efeitos de {dotName} duram +{FormatTurnCountWithUnit(def.IntValue)}.",

                PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow =>
                    FormatIncomingDamageMultiplierBelowHp(def.Additive, hpThresholdPercent),

                PassiveEffectKind.OutgoingDamagePenaltyWhenToken =>
                    FormatOutgoingDamageWhileTokenIsActive(def.Additive, def.TokenType),

                PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill =>
                    $"Após usar {prerequisiteSkillRef}, o próximo {skillRef} causa +{additivePercent} de dano.",

                PassiveEffectKind.ExtraTokenOnSelfSkill =>
                    $"Ao usar {skillRef} em si mesmo, ganha " +
                    $"{FormatTokenStackCountWithUnit(Math.Max(1, def.IntValue))} adicional de " +
                    $"{(def.TokenType.HasValue ? FormatTokenTypeDisplayName(def.TokenType.Value) : "ficha")}.",

                PassiveEffectKind.ExtraHealPercentOnSelfSkill =>
                    $"Ao usar {skillRef} em si mesmo, tentaria recuperar {FormatPlainPercent(def.Additive)} do HP máximo, " +
                    "mas cura é bloqueada fora da vila.",

                PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot =>
                    FormatApplyExtraDot(def, skillRef, dotName),

                PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot =>
                    $"Ao usar {skillRef} contra um alvo com {dotName}, causa +{additivePercent} de dano.",

                PassiveEffectKind.DotTickDamageBonusWhenTargetHpBelow =>
                    $"Seus efeitos de {dotName} causam +{additivePercent} de dano por turno enquanto " +
                    $"o HP do alvo estiver abaixo de {hpThresholdPercent}.",

                PassiveEffectKind.GrantTokenAtTurnStartIfCondition =>
                    FormatGrantTokenAtTurnStart(def, grantedTokenName),

                _ => DescribePassiveEffectKind(def.EffectKind),
            };
        }

        private static string FormatSkillReference(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return "esta habilidade";
            }

            return $"«{NodeOrSkillDisplayName(skillId)}»";
        }

        private static string FormatIncomingDamageMultiplierBelowHp(double multiplier, string hpThresholdPercent)
        {
            if (multiplier <= 0)
            {
                return $"Enquanto seu HP estiver abaixo de {hpThresholdPercent}, o dano recebido é alterado.";
            }

            if (multiplier < 1)
            {
                var damageReductionPercent = FormatPercentFromFraction(1 - multiplier);
                return $"Enquanto seu HP estiver abaixo de {hpThresholdPercent}, você recebe {damageReductionPercent} " +
                       $"menos dano (multiplicador ×{FormatMultiplier(multiplier)}).";
            }

            var extraDamagePercent = FormatPercentFromFraction(multiplier - 1);
            return $"Enquanto seu HP estiver abaixo de {hpThresholdPercent}, você recebe {extraDamagePercent} " +
                   $"mais dano (multiplicador ×{FormatMultiplier(multiplier)}).";
        }

        private static string FormatOutgoingDamageWhileTokenIsActive(double additive, TokenType? tokenType)
        {
            var tokenName = tokenType.HasValue ? FormatTokenTypeDisplayName(tokenType.Value) : "esta ficha";
            if (additive < 0)
            {
                var penaltyPercent = FormatPercentFromFraction(-additive);
                return $"Causa {penaltyPercent} menos dano enquanto tiver {tokenName}.";
            }

            var bonusPercent = FormatPercentFromFraction(additive);
            return $"Causa +{bonusPercent} de dano enquanto tiver {tokenName}.";
        }

        private static string FormatApplyExtraDot(PassiveDefinition def, string skillRef, string dotName)
        {
            var potency = def.IntValue > 0 ? def.IntValue : 2;
            var duration = def.IntValue2 > 0 ? def.IntValue2 : 2;
            return $"Após usar {skillRef} em um alvo que já sofre {dotName}, aplica {dotName} extra " +
                   $"({potency} de dano por turno, durante {FormatTurnCountWithUnit(duration)}).";
        }

        private static string FormatGrantTokenAtTurnStart(PassiveDefinition def, string grantedTokenName)
        {
            if (def.GrantTokenType is null)
            {
                return "No início do seu turno, ganha uma ficha conforme as condições configuradas.";
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

            var lead = $"No início do seu turno, ganha {FormatTokenStackCountWithUnit(stacks)} de {grantedTokenName}";
            if (hasRequirement && hasBlocker)
            {
                return $"{lead} se você tiver {requirementName} e não tiver {blockerName}.";
            }

            if (hasRequirement)
            {
                return $"{lead} se você tiver {requirementName}.";
            }

            if (hasBlocker)
            {
                return $"{lead}, a menos que você tenha {blockerName}.";
            }

            return $"{lead}.";
        }

        public static string FormatTokenTypeDisplayName(TokenType tokenType) =>
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
            percentValue.ToString("0.##", BrazilianCulture) + " %";

        private static string FormatTokenStackCountWithUnit(int stackCount) =>
            stackCount == 1 ? "1 ficha" : $"{stackCount} fichas";

        private static string FormatTurnCountWithUnit(int turnCount) =>
            turnCount == 1 ? "1 turno" : $"{turnCount} turnos";

        public static string DescribePassiveEffectKind(PassiveEffectKind kind) =>
            kind switch
            {
                PassiveEffectKind.OutgoingDamageVsSkillId =>
                    "Aumenta o dano causado ao usar uma habilidade específica.",
                PassiveEffectKind.OutgoingDamageVsDotOnTarget =>
                    "Aumenta o dano contra alvos que sofrem um tipo de dano contínuo (DoT) indicado.",
                PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot =>
                    "Aumenta o dano de uma habilidade se o alvo já tiver um DoT específico.",
                PassiveEffectKind.DotDurationBonus => "Aumenta a duração de um DoT aplicado por você.",
                PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow =>
                    "Altera o dano recebido quando seu HP está abaixo de um limite.",
                PassiveEffectKind.OutgoingDamagePenaltyWhenToken =>
                    "Modifica o seu dano enquanto você possuir certos tokens.",
                PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill =>
                    "Após usar uma habilidade de preparação, o próximo uso de outra habilidade ganha bônus de dano.",
                PassiveEffectKind.ExtraTokenOnSelfSkill =>
                    "Ganha tokens extras ao usar certas habilidades em si mesmo.",
                PassiveEffectKind.ExtraHealPercentOnSelfSkill =>
                    "Cura bloqueada fora da vila; HP só é recuperado pelo Main após 3 segundos na área da vila.",
                PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot =>
                    "Aplica dano contínuo extra quando o alvo já sofre de um DoT.",
                PassiveEffectKind.DotTickDamageBonusWhenTargetHpBelow =>
                    "Aumenta o dano por turno do seu DoT quando o HP do alvo está baixo.",
                PassiveEffectKind.GrantTokenAtTurnStartIfCondition =>
                    "No início do turno, você pode receber tokens se cumprir certas condições.",
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
                DotType.Bleed => "Sangramento",
                DotType.Blight => "Praga",
                DotType.Burn => "Queimadura",
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
                return $"Passiva «{passiveLabel}» ativada.";
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
                    $"Passiva «{passiveLabel}»: +{bonusPct} de dano neste golpe " +
                    $"{(string.IsNullOrEmpty(relatedSkill) ? string.Empty : $"(habilidade «{relatedSkill}»)")}.",
                PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow =>
                    $"Passiva «{passiveLabel}»: dano recebido ×{FormatMultiplier(combatEvent.PassiveMagnitude)} (HP baixo).",
                PassiveEffectKind.ExtraHealPercentOnSelfSkill when combatEvent.PassiveAuxInt > 0 =>
                    $"Passiva «{passiveLabel}»: cura bloqueada fora da vila ({combatEvent.PassiveAuxInt} PV, {bonusPct} do máximo).",
                PassiveEffectKind.ExtraHealPercentOnSelfSkill =>
                    $"Passiva «{passiveLabel}»: cura bloqueada fora da vila ({bonusPct} do máximo).",
                _ =>
                    $"Passiva «{passiveLabel}»: {DescribePassiveEffectKind(kind)}",
            };
        }

        private static string FormatDotInflictedLine(BattleState state, CombatEvent combatEvent)
        {
            var targetName = DisplayCombatantName(state, combatEvent.TargetId);
            var dotName = FormatDotTypeDisplayName(combatEvent.DotType);
            var source = string.IsNullOrEmpty(combatEvent.PassiveId)
                ? string.Empty
                : $" (passiva «{NodeOrSkillDisplayName(combatEvent.PassiveId)}»)";
            return $"{targetName} sofre {dotName} ({combatEvent.DotAmount}/turno, {combatEvent.DotDurationTurns} turnos){source}.";
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
                match => $"Habilidade: «{NodeOrSkillDisplayName(match.Groups[1].Value)}»");

            trimmed = Regex.Replace(
                trimmed,
                @"\b([a-z]+_[a-z0-9_]+)\b",
                match => NodeOrSkillDisplayName(match.Groups[1].Value));

            trimmed = Regex.Replace(
                trimmed,
                @"(?<=\d)\.(?=\d)",
                ",");

            return trimmed;
        }

        private static string FormatPercentFromFraction(double fraction) =>
            (fraction * 100.0).ToString("0.##", BrazilianCulture) + " %";

        private static string FormatMultiplier(double factor) =>
            factor.ToString("0.##", BrazilianCulture);
    }
}