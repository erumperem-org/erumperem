using System.Collections.Generic;
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

            if (skill.TargetKind != SkillTargetKind.Enemy)
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
            var enemies = actor.Position.Side == Side.Allies ? state.Enemies : state.Allies;
            var living = enemies.Where(c => !c.Health.IsDead).ToList();
            if (living.Count == 0)
            {
                return false;
            }

            foreach (var candidate in GetEnemyIntentPoolForUiProbe(living))
            {
                if (PlayerActionBuilder.TryCreate(state, simulator, actor, hotkeyIndexZeroBased, candidate) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Combatant> GetEnemyIntentPoolForUiProbe(IReadOnlyList<Combatant> living)
        {
            var taunt = living.Where(c => c.Tokens.GetStacks(TokenType.Taunt) > 0).ToList();
            return taunt.Count > 0 ? taunt : living.ToList();
        }
    }
}
