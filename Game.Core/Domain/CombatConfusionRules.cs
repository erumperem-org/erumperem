using Game.Core.Models;

namespace Game.Core.Domain;

/// <summary>
/// Confusion (player-facing Status): at turn start each skill has 33% chance to swap
/// Ally↔Enemy targeting for the rest of the turn, and Self↔None.
/// </summary>
public static class CombatConfusionRules
{
    public static SkillTargetKind? SwapTargetKind(SkillTargetKind originalTargetKind) =>
        originalTargetKind switch
        {
            SkillTargetKind.OneEnemy => SkillTargetKind.OneAlly,
            SkillTargetKind.UpToThreeEnemies => SkillTargetKind.SelfAndAlly,
            SkillTargetKind.AllEnemies => SkillTargetKind.SelfAndAlly,
            SkillTargetKind.OneAlly => SkillTargetKind.OneEnemy,
            SkillTargetKind.SelfOrAlly => SkillTargetKind.OneEnemy,
            SkillTargetKind.SelfAndAlly => SkillTargetKind.AllEnemies,
            SkillTargetKind.Self => null,
            _ => originalTargetKind,
        };

    public static IReadOnlyList<Combatant> ResolveSwappedPrimaryTargets(
        BattleState battleState,
        Combatant actor,
        SkillTargetKind originalTargetKind,
        Combatant? selectedCombatant)
    {
        var swappedTargetKind = SwapTargetKind(originalTargetKind);
        if (swappedTargetKind is null)
        {
            return Array.Empty<Combatant>();
        }

        return ResolveForKindIgnoringSelectedSide(battleState, actor, swappedTargetKind.Value, selectedCombatant);
    }

    private static IReadOnlyList<Combatant> ResolveForKindIgnoringSelectedSide(
        BattleState battleState,
        Combatant actor,
        SkillTargetKind swappedTargetKind,
        Combatant? selectedCombatant)
    {
        var sameSideLiving = LivingOnSide(battleState, actor.Position.Side);
        var oppositeSideLiving = LivingOnSide(
            battleState,
            actor.Position.Side == Side.Allies ? Side.Enemies : Side.Allies);

        switch (swappedTargetKind)
        {
            case SkillTargetKind.OneAlly:
                return PickSingle(sameSideLiving, selectedCombatant, preferActor: true, actor);
            case SkillTargetKind.SelfAndAlly:
                return OrderByRank(sameSideLiving);
            case SkillTargetKind.OneEnemy:
                return PickSingle(oppositeSideLiving, selectedCombatant, preferActor: false, actor);
            case SkillTargetKind.AllEnemies:
                return OrderByRank(oppositeSideLiving);
            default:
                return Array.Empty<Combatant>();
        }
    }

    private static IReadOnlyList<Combatant> PickSingle(
        IReadOnlyList<Combatant> candidates,
        Combatant? selectedCombatant,
        bool preferActor,
        Combatant actor)
    {
        if (selectedCombatant != null &&
            candidates.Any(combatant =>
                string.Equals(combatant.Identity.Id, selectedCombatant.Identity.Id, StringComparison.Ordinal)))
        {
            return [selectedCombatant];
        }

        if (preferActor &&
            candidates.Any(combatant =>
                string.Equals(combatant.Identity.Id, actor.Identity.Id, StringComparison.Ordinal)))
        {
            return [actor];
        }

        return candidates.Count > 0 ? [candidates[0]] : Array.Empty<Combatant>();
    }

    private static List<Combatant> LivingOnSide(BattleState battleState, Side side)
    {
        var roster = side == Side.Allies ? battleState.Allies : battleState.Enemies;
        return roster
            .Where(combatant => !combatant.Health.IsDead)
            .OrderBy(combatant => combatant.Position.FrontRank)
            .ToList();
    }

    private static IReadOnlyList<Combatant> OrderByRank(IReadOnlyList<Combatant> combatants) =>
        combatants.OrderBy(combatant => combatant.Position.FrontRank).ToList();
}
