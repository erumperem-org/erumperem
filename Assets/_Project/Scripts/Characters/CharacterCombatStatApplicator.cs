using Game.Core.Domain;
using Game.Core.Models;
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
            bool preserveCurrentHitPoints = false)
        {
            if (combatant == null)
            {
                return;
            }

            var maxHitPoints = Mathf.Max(1, combatMaxHitPoints);
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
