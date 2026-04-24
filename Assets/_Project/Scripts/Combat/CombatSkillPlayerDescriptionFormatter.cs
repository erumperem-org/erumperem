using System.Globalization;
using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Uma linha de texto para o jogador (ex.: "Talho Direto: 1 alvo | 6 de dano | 10% de crít.").
    /// </summary>
    public static class CombatSkillPlayerDescriptionFormatter
    {
        public static string BuildSummaryLine(SkillDefinition skill)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            var targetPart = DescribeTargetForPlayer(skill);
            var damagePart = DescribeDamageForPlayer(skill);
            var critPart = DescribeCritForPlayer(skill);

            return $"{skill.Name}: {targetPart} | {damagePart} | {critPart}{DescribeCooldownPart(skill)}.";
        }

        private static string DescribeTargetForPlayer(SkillDefinition skill) =>
            skill.TargetKind switch
            {
                SkillTargetKind.Enemy => "1 alvo",
                SkillTargetKind.Ally => "1 aliado",
                SkillTargetKind.Self => "ti (auto)",
                _ => "1 alvo",
            };

        private static string DescribeDamageForPlayer(SkillDefinition skill)
        {
            var min = skill.BaseDamage.Min;
            var max = skill.BaseDamage.Max;
            if (min == 0 && max == 0)
            {
                return "sem dano direto";
            }

            if (min == max)
            {
                return $"{min} de dano";
            }

            return $"{min}–{max} de dano";
        }

        private static string DescribeCritForPlayer(SkillDefinition skill)
        {
            var percent = (float)(skill.BaseCritChance * 100.0);
            var text = percent == Mathf.Round(percent)
                ? percent.ToString("0", CultureInfo.InvariantCulture)
                : percent.ToString("0.#", CultureInfo.InvariantCulture);
            return $"{text}% de crít";
        }

        private static string DescribeCooldownPart(SkillDefinition skill)
        {
            if (skill.Cooldown <= 0)
            {
                return string.Empty;
            }

            return $" | recarga {skill.Cooldown}";
        }
    }
}
