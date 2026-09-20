using System.Collections.Generic;
using Game.Core.Models;

public static class CombatDirectionalTargetResolver
{
    public static bool TryResolve(IReadOnlyList<Combatant> stableRoster, IReadOnlyList<Combatant> validCandidates, CombatInputDirection direction, int upSlot, int downSlot, int leftSlot, int rightSlot, out Combatant target)
    {
        target = null;

        if (stableRoster == null || stableRoster.Count == 0 || validCandidates == null || validCandidates.Count == 0)
        {
            return false;
        }

        var desiredSlot = ResolveSlot(direction, upSlot, downSlot, leftSlot, rightSlot);
        var desiredIndex = desiredSlot - 1;

        if (desiredIndex < 0 || desiredIndex >= stableRoster.Count)
        {
            return false;
        }

        var candidate = stableRoster[desiredIndex];

        if (candidate == null || candidate.Health.IsDead || !ContainsCandidate(validCandidates, candidate))
        {
            return false;
        }

        target = candidate;
        return true;
    }

    private static bool ContainsCandidate(IReadOnlyList<Combatant> validCandidates, Combatant candidate)
    {
        for (var candidateIndex = 0; candidateIndex < validCandidates.Count; candidateIndex++)
        {
            if (ReferenceEquals(validCandidates[candidateIndex], candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static int ResolveSlot(CombatInputDirection direction, int upSlot, int downSlot, int leftSlot, int rightSlot)
    {
        switch (direction)
        {
            case CombatInputDirection.Up:
            {
                return upSlot;
            }

            case CombatInputDirection.Down:
            {
                return downSlot;
            }

            case CombatInputDirection.Left:
            {
                return leftSlot;
            }

            case CombatInputDirection.Right:
            {
                return rightSlot;
            }

            default:
            {
                return -1;
            }
        }
    }
}
