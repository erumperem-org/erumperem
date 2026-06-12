using System;
using Game.Core.Presentation;

namespace Erumperem.Combat
{
    public static class CombatSkillHudInfoFormatter
    {
        public static string FormatTargetCountLine(int targetCount) =>
            $"{Math.Max(1, targetCount)}\nTGT";

        public static string FormatDamageRangeLine(SkillCombatHudStats skillStats)
        {
            if (!skillStats.HasDirectDamage)
            {
                return "—\nDMG";
            }

            if (skillStats.DamageMin == skillStats.DamageMax)
            {
                return $"{skillStats.DamageMin}\nDMG";
            }

            return $"{skillStats.DamageMin}-{skillStats.DamageMax}\nDMG";
        }

        public static string FormatCriticalChanceLine(double criticalChanceFraction)
        {
            var criticalPercent = (int)Math.Round(Math.Clamp(criticalChanceFraction, 0.0, 1.0) * 100.0);
            return $"{criticalPercent}%\nCRT";
        }

        public static string FormatCorruptionCostLine(double corruptionCost)
        {
            var formattedCost = Math.Abs(corruptionCost - Math.Round(corruptionCost)) < 1e-9
                ? ((int)Math.Round(corruptionCost)).ToString()
                : corruptionCost.ToString("0.#");
            return $"{formattedCost}\nCORR";
        }
    }
}
