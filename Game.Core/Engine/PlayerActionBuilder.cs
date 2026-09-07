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
}
