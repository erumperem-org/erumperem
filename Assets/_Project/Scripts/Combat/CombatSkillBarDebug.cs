using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core.Domain;
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
            var skillIds = ally.SkillLoadout.Skills
                .Where(id => state.SkillsById.ContainsKey(id))
                .Take(7)
                .ToList();

            var hotbarText = new StringBuilder();
            hotbarText.AppendLine($"{ally.Identity.DisplayName}({allyIndex})");

            for (var i = 0; i < skillIds.Count; i++)
            {
                var skill = state.SkillsById[skillIds[i]];
                hotbarText.AppendLine($"[{i + 1}]- {SummarizeSkill(skill)}");
            }

            if (skillIds.Count == 0)
            {
                hotbarText.AppendLine("(sem skills no catálogo)");
            }

            Debug.Log(hotbarText.ToString());
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
                    case EffectType.HealCorruption:
                        bits.Add($"-corrupção {effectOnHit.Potency}");
                        break;
                    case EffectType.IncreaseCorruption:
                        bits.Add($"+corrupção {effectOnHit.Potency}");
                        break;
                }
            }

            return bits.Count == 0 ? string.Empty : string.Join(", ", bits);
        }
    }
}
