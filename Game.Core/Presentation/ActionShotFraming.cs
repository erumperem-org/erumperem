using System.Numerics;

namespace Game.Core.Presentation;

/// <summary>
/// Camera math for the Target Focus: how far back a fixed-orientation camera must sit so every affected combatant fits.
/// </summary>
public static class ActionShotFraming
{
    public static Vector3 Center(IReadOnlyList<Vector3> points)
    {
        if (points.Count == 0)
        {
            return Vector3.Zero;
        }

        var min = points[0];
        var max = points[0];
        foreach (var point in points)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        return (min + max) * 0.5f;
    }

    /// <summary>Distance from <see cref="Center"/> along -<paramref name="forward"/> where the camera frames all points.</summary>
    public static float RequiredDistance(
        IReadOnlyList<Vector3> points,
        Vector3 forward,
        Vector3 up,
        float verticalFovDegrees,
        float aspect,
        float padding,
        float minimumDistance)
    {
        forward = Vector3.Normalize(forward);
        var right = Vector3.Normalize(Vector3.Cross(up, forward));
        var trueUp = Vector3.Cross(forward, right);

        var tanHalfVertical = MathF.Tan(verticalFovDegrees * 0.5f * MathF.PI / 180f);
        var tanHalfHorizontal = tanHalfVertical * aspect;
        var center = Center(points);
        var distance = minimumDistance;

        foreach (var point in points)
        {
            var offset = point - center;
            var towardCamera = -Vector3.Dot(offset, forward);
            var horizontal = MathF.Abs(Vector3.Dot(offset, right)) * padding / tanHalfHorizontal;
            var vertical = MathF.Abs(Vector3.Dot(offset, trueUp)) * padding / tanHalfVertical;
            distance = MathF.Max(distance, MathF.Max(horizontal, vertical) + towardCamera);
        }

        return distance;
    }
}
