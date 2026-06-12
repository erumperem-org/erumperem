using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Presentation;

/// <summary>
/// Resolve quais combatentes são afetados por uma skill (preview HUD / marcadores de seleção).
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

        var affectedCombatants = new List<Combatant>();
        TryAddPrimaryTarget(battleState, actor, skill, primaryTargetOrNull, affectedCombatants);
        AppendScopedTargets(battleState, actor, skill.EffectsOnHit, affectedCombatants);
        AppendScopedTargets(battleState, actor, skill.ComboBonus, affectedCombatants);

        return affectedCombatants
            .Where(combatant => combatant != null && !combatant.Health.IsDead)
            .Select(combatant => combatant.Identity.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void TryAddPrimaryTarget(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill,
        Combatant? primaryTargetOrNull,
        List<Combatant> affectedCombatants)
    {
        switch (skill.TargetKind)
        {
            case SkillTargetKind.Self:
                affectedCombatants.Add(actor);
                return;

            case SkillTargetKind.Ally:
                if (primaryTargetOrNull != null &&
                    IsLivingSameSideCombatant(battleState, actor, primaryTargetOrNull))
                {
                    affectedCombatants.Add(primaryTargetOrNull);
                }

                return;

            case SkillTargetKind.Enemy:
            default:
                if (primaryTargetOrNull != null &&
                    IsLivingOppositeSideCombatant(battleState, actor, primaryTargetOrNull))
                {
                    affectedCombatants.Add(primaryTargetOrNull);
                }

                return;
        }
    }

    private static void AppendScopedTargets(
        BattleState battleState,
        Combatant actor,
        IReadOnlyList<EffectSpec> effects,
        List<Combatant> affectedCombatants)
    {
        if (effects == null || effects.Count == 0)
        {
            return;
        }

        foreach (var effect in effects)
        {
            if (string.Equals(effect.EffectScope, "AllAllies", StringComparison.OrdinalIgnoreCase))
            {
                affectedCombatants.AddRange(LivingSameSide(battleState, actor));
                continue;
            }

            if (string.Equals(effect.EffectScope, "AllEnemies", StringComparison.OrdinalIgnoreCase))
            {
                affectedCombatants.AddRange(LivingOppositeSide(battleState, actor));
            }
        }
    }

    private static bool IsLivingSameSideCombatant(
        BattleState battleState,
        Combatant actor,
        Combatant candidate) =>
        candidate != null &&
        !candidate.Health.IsDead &&
        candidate.Position.Side == actor.Position.Side &&
        LivingSameSide(battleState, actor).Any(combatant => combatant.Identity.Id == candidate.Identity.Id);

    private static bool IsLivingOppositeSideCombatant(
        BattleState battleState,
        Combatant actor,
        Combatant candidate) =>
        candidate != null &&
        !candidate.Health.IsDead &&
        candidate.Position.Side != actor.Position.Side &&
        LivingOppositeSide(battleState, actor).Any(combatant => combatant.Identity.Id == candidate.Identity.Id);

    private static IEnumerable<Combatant> LivingSameSide(BattleState battleState, Combatant actor)
    {
        var combatantsOnSameSide = actor.Position.Side == Side.Allies
            ? (IEnumerable<Combatant>)battleState.Allies
            : battleState.Enemies;
        return LivingCombatantsOnSide(combatantsOnSameSide);
    }

    private static IEnumerable<Combatant> LivingOppositeSide(BattleState battleState, Combatant actor)
    {
        var combatantsOnOppositeSide = actor.Position.Side == Side.Allies
            ? (IEnumerable<Combatant>)battleState.Enemies
            : battleState.Allies;
        return LivingCombatantsOnSide(combatantsOnOppositeSide);
    }

    private static IEnumerable<Combatant> LivingCombatantsOnSide(IEnumerable<Combatant> combatantsOnSide) =>
        combatantsOnSide.Where(combatant => !combatant.Health.IsDead);
}
