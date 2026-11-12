using Game.Core.Analytics;
using Game.Core.Domain;

namespace Game.Core.Presentation;

/// <summary>
/// Who an Action Shot frames and how they react, derived from the events one active skill produced.
/// </summary>
public sealed class ActionShotPlan
{
    public IReadOnlyList<string> AffectedCombatantIds { get; }

    /// <summary>One entry per damaging hit, in resolution order (multi-hit repeats the id).</summary>
    public IReadOnlyList<string> HitTakenCombatantIds { get; }

    public IReadOnlyList<string> DiedCombatantIds { get; }

    /// <summary>False when only the actor is affected: the shot goes Actor Focus → Return.</summary>
    public bool HasTargetFocus { get; }

    private ActionShotPlan(
        IReadOnlyList<string> affected,
        IReadOnlyList<string> hitTaken,
        IReadOnlyList<string> died,
        bool hasTargetFocus)
    {
        AffectedCombatantIds = affected;
        HitTakenCombatantIds = hitTaken;
        DiedCombatantIds = died;
        HasTargetFocus = hasTargetFocus;
    }

    public static ActionShotPlan Build(string actorId, string primaryTargetId, IEnumerable<CombatEvent> actionEvents)
    {
        var affected = new List<string>();
        var hitTaken = new List<string>();
        var died = new List<string>();

        // Heals and some buffs resolve without a targeted event; the primary target is still affected.
        if (!string.IsNullOrEmpty(primaryTargetId))
        {
            affected.Add(primaryTargetId);
        }

        foreach (var combatEvent in actionEvents)
        {
            if (string.IsNullOrEmpty(combatEvent.TargetId) || combatEvent.EventType == BattleEventType.ActionUsed)
            {
                continue;
            }

            if (!affected.Contains(combatEvent.TargetId))
            {
                affected.Add(combatEvent.TargetId);
            }

            if (combatEvent.EventType == BattleEventType.DamageApplied && combatEvent.DamageAmount > 0)
            {
                hitTaken.Add(combatEvent.TargetId);
            }

            if (combatEvent.EventType == BattleEventType.CombatantDied)
            {
                died.Add(combatEvent.TargetId);
            }
        }

        return new ActionShotPlan(affected, hitTaken, died, affected.Any(id => id != actorId));
    }
}
