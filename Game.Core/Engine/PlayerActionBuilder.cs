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

        if (!IsValidPlayerClickTarget(state, actor, skill, selectedTarget))
        {
            return null;
        }

        var primaryTargets = SkillTargetResolver.ResolvePrimaryTargets(state, actor, skill, selectedTarget);
        if (primaryTargets.Count == 0)
        {
            return null;
        }

        var chosenTarget = selectedTarget != null &&
            primaryTargets.Any(combatant =>
                string.Equals(combatant.Identity.Id, selectedTarget.Identity.Id, StringComparison.Ordinal))
            ? selectedTarget
            : primaryTargets[0];

        return new ChosenAction
        {
            Actor = actor,
            Target = chosenTarget,
            Skill = skill,
            ActionType = ActionType.Skill,
        };
    }

    /// <summary>
    /// Self skills must be clicked on the caster. Ally skills need a same-side click.
    /// Enemy skills need a living enemy in the valid pool — clicking an ally must not fire them.
    /// </summary>
    private static bool IsValidPlayerClickTarget(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        Combatant? selectedTarget)
    {
        if (selectedTarget == null)
        {
            return false;
        }

        if (SkillTargetKindRules.IsSelfOnly(skill.TargetKind))
        {
            return string.Equals(selectedTarget.Identity.Id, actor.Identity.Id, StringComparison.Ordinal);
        }

        if (SkillTargetKindRules.DirectsPrimaryDamageAtAllies(skill.TargetKind))
        {
            if (selectedTarget.Position.Side != actor.Position.Side)
            {
                return false;
            }

            return !selectedTarget.Health.IsDead || skill.CanTargetDeadAllies;
        }

        if (SkillTargetKindRules.DirectsPrimaryDamageAtEnemies(skill.TargetKind))
        {
            return SkillTargetResolver.GetValidEnemyPool(state, actor)
                .Any(enemy => string.Equals(enemy.Identity.Id, selectedTarget.Identity.Id, StringComparison.Ordinal));
        }

        return false;
    }
}
