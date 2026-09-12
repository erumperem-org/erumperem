using UnityEngine;

/// <summary>
/// Implementação de <c>IMovementValidator</c> (do projeto base de
/// movimentação por física) voltada para IA: em vez de só deslizar
/// (<c>WallSlideValidator</c>) ou parar seco (<c>HardStopValidator</c>) ao
/// encontrar um obstáculo, tenta ativamente achar outro caminho para o
/// mesmo objetivo, testando direções em ângulos crescentes (alternando para
/// a direita e para a esquerda da direção original) até encontrar uma
/// livre.
///
/// Se nenhuma direção testada estiver livre, cai em um fallback equivalente
/// ao <c>WallSlideValidator</c> (projeta no plano da normal do obstáculo),
/// para nunca travar completamente o personagem.
/// </summary>
[System.Serializable]
public class ObstacleAvoidanceValidator : IMovementValidator
{
    private readonly float maxSearchAngle;
    private readonly float angleStep;

    /// <param name="maxSearchAngle">Ângulo máximo, para cada lado, testado na busca por caminho livre.</param>
    /// <param name="angleStep">Incremento angular entre cada tentativa.</param>
    public ObstacleAvoidanceValidator(float maxSearchAngle = 90f, float angleStep = 15f)
    {
        this.maxSearchAngle = Mathf.Max(0f, maxSearchAngle);
        this.angleStep = Mathf.Max(1f, angleStep);
    }

    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin,
                             float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            return desiredDirection;
        }

        Vector3 normalizedDirection = desiredDirection.normalized;
        float magnitude = desiredDirection.magnitude;

        if (!IsBlocked(origin, normalizedDirection, castRadius, checkDistance, obstacleMask))
        {
            return desiredDirection;
        }

        for (float angle = angleStep; angle <= maxSearchAngle; angle += angleStep)
        {
            Vector3 rightCandidate = Quaternion.AngleAxis(angle, Vector3.up) * normalizedDirection;
            if (!IsBlocked(origin, rightCandidate, castRadius, checkDistance, obstacleMask))
            {
                return rightCandidate * magnitude;
            }

            Vector3 leftCandidate = Quaternion.AngleAxis(-angle, Vector3.up) * normalizedDirection;
            if (!IsBlocked(origin, leftCandidate, castRadius, checkDistance, obstacleMask))
            {
                return leftCandidate * magnitude;
            }
        }

        if (Physics.SphereCast(origin, castRadius, normalizedDirection, out RaycastHit hit, checkDistance, obstacleMask))
        {
            return Vector3.ProjectOnPlane(desiredDirection, hit.normal);
        }

        return desiredDirection;
    }

    private static bool IsBlocked(Vector3 origin, Vector3 direction, float castRadius,
                                   float checkDistance, LayerMask obstacleMask)
    {
        return Physics.SphereCast(origin, castRadius, direction, out _, checkDistance, obstacleMask);
    }
}
