using UnityEngine;

/// <summary>
/// Validador que zera completamente a direção de movimento quando detecta um
/// obstáculo à frente dentro da distância de verificação — o personagem para
/// seco em vez de deslizar ao longo da superfície.
/// </summary>
[System.Serializable]
public class HardStopValidator : IMovementValidator
{
    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin, float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return desiredDirection;

        float magnitude = desiredDirection.magnitude;
        Vector3 dirNormalized = desiredDirection / magnitude;

        bool hitSomething = Physics.SphereCast(
            origin, castRadius, dirNormalized,
            out _, checkDistance, obstacleMask, QueryTriggerInteraction.Ignore);

        return hitSomething ? Vector3.zero : desiredDirection;
    }
}
