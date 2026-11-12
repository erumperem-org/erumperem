using System.Numerics;
using Game.Core.Presentation;

namespace Game.Tests;

public sealed class ActionShotFramingTests
{
    // Camera looks down -X (like the combat zoom camera); right = -Z, up = +Y.
    private static readonly Vector3 Forward = new(-1, 0, 0);
    private static readonly Vector3 Up = Vector3.UnitY;

    [Fact]
    public void SingleCombatant_UsesMinimumDistance()
    {
        var distance = ActionShotFraming.RequiredDistance(
            new[] { new Vector3(0, 0, 0) }, Forward, Up,
            verticalFovDegrees: 90f, aspect: 1f, padding: 1f, minimumDistance: 6f);

        Assert.Equal(6f, distance, 3);
    }

    [Fact]
    public void TwoCombatantsTenApart_With90DegreeFov_NeedDistanceFive()
    {
        var distance = ActionShotFraming.RequiredDistance(
            new[] { new Vector3(0, 0, -5), new Vector3(0, 0, 5) }, Forward, Up,
            verticalFovDegrees: 90f, aspect: 1f, padding: 1f, minimumDistance: 1f);

        Assert.Equal(5f, distance, 3);
    }

    [Fact]
    public void CombatantCloserToCamera_PushesTheCameraBackByThatDepth()
    {
        // Center is (0,0,0); the near combatant sits 2 units toward the camera and 5 to the side.
        var distance = ActionShotFraming.RequiredDistance(
            new[] { new Vector3(2, 0, -5), new Vector3(-2, 0, 5) }, Forward, Up,
            verticalFovDegrees: 90f, aspect: 1f, padding: 1f, minimumDistance: 1f);

        Assert.Equal(7f, distance, 3);
    }
}
