using System.Linq;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;

namespace Erumperem.Combat
{
    /// <summary>
    /// Clicável quando <see cref="PlayerActionBuilder.TryCreate"/> consegue montar a ação
    /// (ex.: inimigo selecionável no mapa para skills em inimigo).
    /// </summary>
    public static class CombatSkillSlotUiEligibility
    {
        public static bool IsSlotUiInteractable(
            BattleState state,
            BattleSimulator simulator,
            Combatant actor,
            int hotkeyIndexZeroBased,
            Combatant? preferredTarget)
        {
            var skillIds = actor.SkillLoadout.Skills
                .Where(id => state.SkillsById.ContainsKey(id))
                .Take(7)
                .ToList();
            if (hotkeyIndexZeroBased < 0 || hotkeyIndexZeroBased >= skillIds.Count)
            {
                return false;
            }

            var skill = state.SkillsById[skillIds[hotkeyIndexZeroBased]];
            if (!simulator.IsSkillUsable(actor, skill))
            {
                return false;
            }

            if (PlayerActionBuilder.TryCreate(state, simulator, actor, hotkeyIndexZeroBased, preferredTarget) != null)
            {
                return true;
            }

            if (!SkillTargetKindRules.DirectsPrimaryDamageAtEnemies(skill.TargetKind))
            {
                return false;
            }

            return HasAnyValidEnemyForSlot(state, simulator, actor, hotkeyIndexZeroBased);
        }

        private static bool HasAnyValidEnemyForSlot(
            BattleState state,
            BattleSimulator simulator,
            Combatant actor,
            int hotkeyIndexZeroBased)
        {
            foreach (var candidate in SkillTargetResolver.GetValidEnemyPool(state, actor))
            {
                if (PlayerActionBuilder.TryCreate(state, simulator, actor, hotkeyIndexZeroBased, candidate) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
