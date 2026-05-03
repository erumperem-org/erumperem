using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Texto simplificado da hotbar (1–7) para debug no console — mesma ordem que <see cref="Game.Core.Engine.PlayerActionBuilder"/>.
    /// </summary>
    public static class CombatSkillBarDebug
    {
        public static void LogHotbar(Combatant ally, int allyIndex, BattleState state)
        {
            Debug.Log(BuildHotbarPanelText(ally, allyIndex, state, null, null));
        }

        /// <summary>
        /// Texto multi-linha da hotbar [1]–[7]. Com <paramref name="simulatorForAvailability"/>, acrescenta sufixo por slot quando skill não pode ser usada agora.
        /// </summary>
        public static string BuildHotbarPanelText(
            Combatant ally,
            int allyIndex,
            BattleState state,
            BattleSimulator simulatorForAvailability = null,
            Combatant selectedEnemyTarget = null)
        {
            var skillIds = ally.SkillLoadout.Skills
                .Where(id => state.SkillsById.ContainsKey(id))
                .Take(7)
                .ToList();

            var hotbarText = new StringBuilder();
            hotbarText.AppendLine($"{ally.Identity.DisplayName}({allyIndex})");

            for (var i = 0; i < skillIds.Count; i++)
            {
                var skill = state.SkillsById[skillIds[i]];
                var line = $"[{i + 1}]- {SummarizeSkill(skill)}";
                if (simulatorForAvailability != null)
                {
                    line += DescribeAvailabilitySuffix(
                        state,
                        simulatorForAvailability,
                        ally,
                        hotkeyIndexZeroBased: i,
                        selectedEnemyTarget,
                        skill);
                }

                hotbarText.AppendLine(line);
            }

            if (skillIds.Count == 0)
            {
                hotbarText.AppendLine("(sem skills no catálogo)");
            }

            return hotbarText.ToString().TrimEnd();
        }

        private static string DescribeAvailabilitySuffix(
            BattleState state,
            BattleSimulator simulator,
            Combatant actor,
            int hotkeyIndexZeroBased,
            Combatant selectedEnemyTarget,
            SkillDefinition skill)
        {
            if (!simulator.IsSkillUsable(actor, skill))
            {
                return " — indisponível (cooldown ou bloqueio)";
            }

            if (PlayerActionBuilder.TryCreate(state, simulator, actor, hotkeyIndexZeroBased, selectedEnemyTarget) != null)
            {
                return string.Empty;
            }

            return " — indisponível (alvo, rank ou outro)";
        }

        private static string SummarizeSkill(SkillDefinition skillDefinition)
        {
            var target = TargetAfterA(skillDefinition.TargetKind);
            string core;
            if (skillDefinition.BaseDamage.Min == 0 && skillDefinition.BaseDamage.Max == 0)
            {
                core = $"{skillDefinition.Name} — sem dano direto, alvo: {target}";
            }
            else
            {
                core = $"{skillDefinition.Name} — {skillDefinition.BaseDamage.Min}-{skillDefinition.BaseDamage.Max} dano a {target}";
            }

            var effectsSummary = SummarizeEffects(skillDefinition);
            if (!string.IsNullOrEmpty(effectsSummary))
            {
                core += $", {effectsSummary}";
            }

            if (skillDefinition.Cooldown > 0)
            {
                core += $", CD{skillDefinition.Cooldown}";
            }

            return core;
        }

        /// <summary>Complemento depois de "dano a …".</summary>
        private static string TargetAfterA(SkillTargetKind targetKind) => targetKind switch
        {
            SkillTargetKind.Enemy => "um inimigo único",
            SkillTargetKind.Ally => "um aliado",
            SkillTargetKind.Self => "ti (self)",
            _ => targetKind.ToString(),
        };

        private static string SummarizeEffects(SkillDefinition skillDefinition)
        {
            var bits = new List<string>();
            if (Math.Abs(skillDefinition.CorruptionCost) > 1e-12)
            {
                bits.Add(
                    skillDefinition.CorruptionCost > 0
                        ? $"+corrupção (uso) {skillDefinition.CorruptionCost:0.##}"
                        : $"-corrupção (uso) {-skillDefinition.CorruptionCost:0.##}");
            }

            foreach (var effectOnHit in skillDefinition.EffectsOnHit)
            {
                switch (effectOnHit.Type)
                {
                    case EffectType.ApplyDot when effectOnHit.Dot.HasValue:
                        bits.Add($"DOT {effectOnHit.Dot.Value}");
                        break;
                    case EffectType.Push:
                        bits.Add($"empurra {effectOnHit.Steps}");
                        break;
                    case EffectType.Pull:
                        bits.Add($"puxa {effectOnHit.Steps}");
                        break;
                    case EffectType.ApplyToken when effectOnHit.Token.HasValue:
                        bits.Add($"{effectOnHit.Token.Value} x{effectOnHit.Stacks}");
                        break;
                    case EffectType.HealHp:
                        bits.Add($"cura {effectOnHit.Potency} HP");
                        break;
                    case EffectType.HealHpPercent:
                        bits.Add($"cura {effectOnHit.Potency}% HP");
                        break;
                    case EffectType.ApplyStun:
                        bits.Add("stun");
                        break;
                }
            }

            return bits.Count == 0 ? string.Empty : string.Join(", ", bits);
        }
    }
}
