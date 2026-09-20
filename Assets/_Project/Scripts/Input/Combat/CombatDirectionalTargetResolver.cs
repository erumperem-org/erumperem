using System.Collections.Generic;
using Game.Core.Models;

public static class CombatDirectionalTargetResolver
{
    public static bool TryResolve(IReadOnlyList<Combatant> candidates, CombatInputDirection direction, int upRank, int downRank, int leftRank, int rightRank, out Combatant target)
    {
        target = null;

        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        var desiredRank = ResolveRank(direction, upRank, downRank, leftRank, rightRank);

        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];

            if (candidate == null || candidate.Health.IsDead)
            {
                continue;
            }

            if (candidate.Position.FrontRank != desiredRank)
            {
                continue;
            }

            target = candidate;
            return true;
        }

        return false;
    }

    private static int ResolveRank(CombatInputDirection direction, int upRank, int downRank, int leftRank, int rightRank)
    {
        switch (direction)
        {
            case CombatInputDirection.Up:
            {
                return upRank;
            }

            case CombatInputDirection.Down:
            {
                return downRank;
            }

            case CombatInputDirection.Left:
            {
                return leftRank;
            }

            case CombatInputDirection.Right:
            {
                return rightRank;
            }

            default:
            {
                return -1;
            }
        }
    }
}
