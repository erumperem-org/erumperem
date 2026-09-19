using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;

namespace Game.Core.Presentation;

/// <summary>
/// Resolve quais combatentes são afetados por uma skill (preview HUD / marcadores de seleção).
/// Primary targets come from <see cref="SkillTargetResolver"/>; extra ids come from non-default EffectScope.
/// </summary>
public static class SkillCombatTargetPreviewResolver
{
    public static IReadOnlyList<string> ResolveAffectedCombatantIds(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill,
        Combatant? primaryTargetOrNull)
    {
        if (battleState == null || actor == null || skill == null)
        {
            return Array.Empty<string>();
        }

        var affectedCombatantIds = new List<string>();
        foreach (var primaryTarget in SkillTargetResolver.ResolvePrimaryTargets(
                     battleState,
                     actor,
                     skill,
                     primaryTargetOrNull))
        {
            TryAddCombatantId(affectedCombatantIds, primaryTarget);
        }

        foreach (var effect in skill.EffectsOnHit)
        {
            if (effect.EffectScope == EffectScope.Default)
            {
                continue;
            }

            foreach (var recipient in SkillTargetResolver.ResolveEffectRecipients(
                         battleState,
                         actor,
                         primaryTargetOrNull ?? actor,
                         effect.EffectScope))
            {
                TryAddCombatantId(affectedCombatantIds, recipient);
            }
        }

        return affectedCombatantIds;
    }

    private static void TryAddCombatantId(List<string> affectedCombatantIds, Combatant? combatant)
    {
        if (combatant == null || combatant.Health.IsDead)
        {
            return;
        }

        if (affectedCombatantIds.Contains(combatant.Identity.Id))
        {
            return;
        }

        affectedCombatantIds.Add(combatant.Identity.Id);
    }
}
