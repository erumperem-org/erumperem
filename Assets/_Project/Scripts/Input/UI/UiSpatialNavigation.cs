using System.Collections.Generic;
using UnityEngine;

public static class UiSpatialNavigation
{
    public static UiNavigationTarget FindDefault(KeyboardNavigablePanel panel, IReadOnlyList<UiNavigationTarget> targets)
    {
        if (panel != null && panel.DefaultSelected != null)
        {
            var configuredTarget = UiNavigationTargetCatalog.FindForGameObject(targets, panel.DefaultSelected);

            if (UiNavigationTargetCatalog.IsValid(configuredTarget))
            {
                return configuredTarget;
            }
        }

        return FindTopLeft(targets);
    }

    public static bool TryFindDirectional(IReadOnlyList<UiNavigationTarget> targets, UiNavigationTarget current, UiNavigationDirection direction, out UiNavigationTarget target)
    {
        target = null;

        if (!UiNavigationTargetCatalog.IsValid(current))
        {
            return false;
        }

        var origin = GetScreenCenter(current);
        var desiredDirection = ToVector(direction);
        var perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
        var bestScore = float.PositiveInfinity;

        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            var candidate = targets[targetIndex];

            if (!UiNavigationTargetCatalog.IsValid(candidate) || ReferenceEquals(candidate, current))
            {
                continue;
            }

            var delta = GetScreenCenter(candidate) - origin;
            var forwardDistance = Vector2.Dot(delta, desiredDirection);

            if (forwardDistance <= 0.01f)
            {
                continue;
            }

            var perpendicularDistance = Mathf.Abs(Vector2.Dot(delta, perpendicular));
            var score = forwardDistance + perpendicularDistance * 2.5f;

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            target = candidate;
        }

        return target != null;
    }

    public static bool TryFindWrapped(IReadOnlyList<UiNavigationTarget> targets, UiNavigationTarget current, UiNavigationDirection direction, out UiNavigationTarget target)
    {
        target = null;

        if (!UiNavigationTargetCatalog.IsValid(current))
        {
            return false;
        }

        var origin = GetScreenCenter(current);
        var desiredDirection = ToVector(direction);
        var perpendicular = new Vector2(-desiredDirection.y, desiredDirection.x);
        var bestProjection = float.PositiveInfinity;
        var bestPerpendicular = float.PositiveInfinity;

        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            var candidate = targets[targetIndex];

            if (!UiNavigationTargetCatalog.IsValid(candidate) || ReferenceEquals(candidate, current))
            {
                continue;
            }

            var candidatePosition = GetScreenCenter(candidate);
            var projection = Vector2.Dot(candidatePosition, desiredDirection);
            var perpendicularDistance = Mathf.Abs(Vector2.Dot(candidatePosition - origin, perpendicular));

            if (projection < bestProjection - 0.01f || Mathf.Abs(projection - bestProjection) <= 0.01f && perpendicularDistance < bestPerpendicular)
            {
                bestProjection = projection;
                bestPerpendicular = perpendicularDistance;
                target = candidate;
            }
        }

        return target != null;
    }

    public static Vector2 GetScreenCenter(UiNavigationTarget target)
    {
        if (target == null || target.RectTransform == null)
        {
            return Vector2.zero;
        }

        var canvas = target.RectTransform.GetComponentInParent<Canvas>();
        Camera camera = null;

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera;
        }

        var worldCenter = target.RectTransform.TransformPoint(target.RectTransform.rect.center);
        return RectTransformUtility.WorldToScreenPoint(camera, worldCenter);
    }

    public static Vector2 ToVector(UiNavigationDirection direction)
    {
        switch (direction)
        {
            case UiNavigationDirection.Up:
            {
                return Vector2.up;
            }

            case UiNavigationDirection.Down:
            {
                return Vector2.down;
            }

            case UiNavigationDirection.Left:
            {
                return Vector2.left;
            }

            case UiNavigationDirection.Right:
            {
                return Vector2.right;
            }

            default:
            {
                return Vector2.zero;
            }
        }
    }

    private static UiNavigationTarget FindTopLeft(IReadOnlyList<UiNavigationTarget> targets)
    {
        UiNavigationTarget bestTarget = null;
        var bestPosition = Vector2.zero;

        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            var candidate = targets[targetIndex];

            if (!UiNavigationTargetCatalog.IsValid(candidate))
            {
                continue;
            }

            var candidatePosition = GetScreenCenter(candidate);

            if (bestTarget == null || candidatePosition.y > bestPosition.y + 0.01f || Mathf.Abs(candidatePosition.y - bestPosition.y) <= 0.01f && candidatePosition.x < bestPosition.x)
            {
                bestTarget = candidate;
                bestPosition = candidatePosition;
            }
        }

        return bestTarget;
    }
}
