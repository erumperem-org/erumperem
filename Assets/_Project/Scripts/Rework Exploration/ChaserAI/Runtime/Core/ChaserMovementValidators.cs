/// <summary>
/// Monta o validador de movimento do chaser: desvio de obstáculos sempre,
/// mais desvio da área segura quando existir uma.
/// </summary>
public static class ChaserMovementValidators
{
    public static IMovementValidator Create(ChaserSettings settings, CircularZone safeArea, float safeAreaMargin)
    {
        IMovementValidator obstacleValidator = new ObstacleAvoidanceValidator(
            settings.maxAvoidanceAngle,
            settings.avoidanceAngleStep);

        if (safeArea == null)
        {
            return obstacleValidator;
        }

        IMovementValidator safeAreaValidator = new SafeAreaAvoidanceValidator(
            safeArea,
            safeAreaMargin,
            settings.safeAreaAvoidanceMaxSearchAngle,
            settings.avoidanceAngleStep);

        return new CompositeMovementValidator(obstacleValidator, safeAreaValidator);
    }
}
