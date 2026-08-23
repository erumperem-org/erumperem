using UnityEngine;

/// <summary>
/// Validador que projeta a direção desejada no plano do obstáculo detectado,
/// permitindo que o personagem "deslize" ao longo de paredes em vez de ficar
/// empurrando contra elas indefinidamente.
///
/// Só a componente da direção que aponta para dentro do obstáculo é removida;
/// a componente tangencial (paralela à superfície) é preservada, então andar
/// em diagonal contra uma parede continua resultando em movimento ao longo
/// dela.
/// </summary>
[System.Serializable]
public class WallSlideValidator : IMovementValidator
{
    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin, float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
            return desiredDirection;

        float magnitude = desiredDirection.magnitude;
        Vector3 dirNormalized = desiredDirection / magnitude;

        bool hitSomething = Physics.SphereCast(
            origin, castRadius, dirNormalized,
            out RaycastHit hit, checkDistance, obstacleMask, QueryTriggerInteraction.Ignore);

        if (!hitSomething)
            return desiredDirection;

        // Remove apenas a componente da direção que aponta para dentro do
        // obstáculo, mantendo a componente tangencial à superfície.
        Vector3 slideDirection = Vector3.ProjectOnPlane(dirNormalized, hit.normal);

        if (slideDirection.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return slideDirection.normalized * magnitude;
    }
}
