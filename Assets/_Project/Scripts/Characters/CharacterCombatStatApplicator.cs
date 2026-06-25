using Game.Core.Domain;
using Game.Core.Models;
using Services.DebugUtilities;
using UnityEngine;

namespace Erumperem.Characters
{
    internal static class CharacterCombatStatApplicator
    {
        internal static void Apply(
            Combatant combatant,
            string displayName,
            int combatMaxHitPoints,
            int speed,
            double accuracy,
            double critChance,
            double burnResistance,
            double blightResistance,
            double stunResistance,
            ElementType elementType,
            bool preserveCurrentHitPoints = false,
            bool applyHealth = true)
        {
            if (combatant == null)
            {
                return;
            }

            if (applyHealth)
            {
                var maxHitPoints = Mathf.Max(1, combatMaxHitPoints);
                var previousHitPoints = combatant.Health.CurrentHp;
                var previousMaxHitPoints = combatant.Health.MaxHp;
                var currentHitPoints = preserveCurrentHitPoints
                    ? Mathf.Clamp(combatant.Health.CurrentHp, 0, maxHitPoints)
                    : maxHitPoints;

                combatant.Health = new HealthComponent
                {
                    MaxHp = maxHitPoints,
                    CurrentHp = currentHitPoints,
                    IsDead = currentHitPoints <= 0,
                    IsDeathblowPending = false,
                };

                if (combatant.Identity.Faction == Faction.Player && currentHitPoints > previousHitPoints)
                {
                    LoggerService.PrintLogMessage(LogLevel.Debug,
                        $"[HEAL-DEBUG] [COMBAT-STATS] '{displayName}' HP aumentou " +
                        $"{previousHitPoints}/{previousMaxHitPoints} → {currentHitPoints}/{maxHitPoints} " +
                        $"(preserveCurrent={preserveCurrentHitPoints}, applyHealth={applyHealth}).",
                        LogCategory.Player);
                }
            }

            combatant.Stats = new StatsComponent
            {
                Speed = speed,
                Accuracy = accuracy,
                CritChance = critChance,
            };

            var existingResistances = combatant.Resistances;
            combatant.Resistances = new ResistanceComponent
            {
                BurnRes = burnResistance,
                BlightRes = blightResistance,
                MoveRes = existingResistances.MoveRes,
                StunRes = stunResistance,
                DeathblowRes = existingResistances.DeathblowRes,
            };

            combatant.ElementAffinity = new ElementAffinityComponent
            {
                Element = elementType,
            };

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            var existingIdentity = combatant.Identity;
            combatant.Identity = new IdentityComponent
            {
                Id = existingIdentity.Id,
                DisplayName = displayName,
                Faction = existingIdentity.Faction,
                Tags = existingIdentity.Tags,
            };
        }
    }
}
