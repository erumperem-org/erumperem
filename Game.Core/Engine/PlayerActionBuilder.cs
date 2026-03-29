using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>
/// Monta <see cref="ChosenAction"/> para input humano (hotkeys 1–7 = primeiras skills do loadout, até 7).
/// </summary>
public static class PlayerActionBuilder
{
    /// <param name="hotkeyIndexZeroBased">0 = tecla 1, … 6 = tecla 7.</param>
    public static ChosenAction? TryCreate(
        BattleState state,
        BattleSimulator simulator,
        Combatant actor,
        int hotkeyIndexZeroBased,
        Combatant? selectedTarget)
    {
        var skillIds = actor.SkillLoadout.Skills
            .Where(id => state.SkillsById.ContainsKey(id))
            .Take(7)
            .ToList();

        if (hotkeyIndexZeroBased < 0 || hotkeyIndexZeroBased >= skillIds.Count)
        {
            return null;
        }

        var skill = state.SkillsById[skillIds[hotkeyIndexZeroBased]];
        if (!simulator.IsSkillUsable(actor, skill))
        {
            return null;
        }

        Combatant target;
        if (skill.TargetKind == SkillTargetKind.Self)
        {
            target = actor;
        }
        else if (skill.TargetKind == SkillTargetKind.Ally)
        {
            var sameSide = actor.Position.Side == Side.Allies ? state.Allies : state.Enemies;
            var allies = sameSide.Where(c => !c.Health.IsDead).ToList();
            var pick = selectedTarget is not null && allies.Contains(selectedTarget) ? selectedTarget : actor;
            if (!allies.Contains(pick))
            {
                return null;
            }

            if (pick.Tokens.GetStacks(TokenType.Stealth) > 0)
            {
                return null;
            }

            if (!pick.Position.OccupiedRanks.Any(r => skill.AllowedTargetRanks.Contains(r)))
            {
                return null;
            }

            target = pick;
        }
        else
        {
            var enemies = actor.Position.Side == Side.Allies ? state.Enemies : state.Allies;
            var living = enemies.Where(e => !e.Health.IsDead).ToList();
            if (selectedTarget is null || !living.Contains(selectedTarget))
            {
                return null;
            }

            var taunt = living.Where(t => t.Tokens.GetStacks(TokenType.Taunt) > 0).ToList();
            var pool = taunt.Count > 0 ? taunt : living;
            if (!pool.Contains(selectedTarget))
            {
                return null;
            }

            if (selectedTarget.Tokens.GetStacks(TokenType.Stealth) > 0)
            {
                return null;
            }

            if (!selectedTarget.Position.OccupiedRanks.Any(r => skill.AllowedTargetRanks.Contains(r)))
            {
                return null;
            }

            target = selectedTarget;
        }

        return new ChosenAction
        {
            Actor = actor,
            Target = target,
            Skill = skill,
            ActionType = ActionType.Skill,
        };
    }
}
