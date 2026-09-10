using UnityEngine;

/// <summary>
/// Mantém quem se move fora de uma SafeArea/Hub - diferente do
/// ObstacleAvoidanceValidator (que desvia de colisores sólidos via
/// SphereCast), este verifica contra uma zona circular lógica
/// (CircularZone.Contains), porque o collider do Hub é trigger
/// (isTrigger = true) e por isso não bloqueia movimento fisicamente por
/// conta própria. Sem este validador, nada impede um Chaser de atravessar
/// o Hub em linha reta enquanto persegue ou investiga - o WanderArea já
/// evita ESCOLHER um destino de patrulha dentro dele, mas isso não impede
/// o CAMINHO até um destino válido de cruzar por dentro.
///
/// Mesma estratégia de "busca por ângulos alternados" do
/// ObstacleAvoidanceValidator, para os dois comporem naturalmente via
/// CompositeMovementValidator.
/// </summary>
public class SafeAreaAvoidanceValidator : IMovementValidator
{
    private readonly CircularZone excludedArea;
    private readonly float margin;
    private readonly float maxSearchAngle;
    private readonly float angleStep;

    public SafeAreaAvoidanceValidator(CircularZone excludedArea, float margin = 2f, float maxSearchAngle = 150f, float angleStep = 15f)
    {
        this.excludedArea = excludedArea;
        this.margin = Mathf.Max(0f, margin);
        this.maxSearchAngle = Mathf.Max(0f, maxSearchAngle);
        this.angleStep = Mathf.Max(1f, angleStep);
    }

    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin, float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        if (excludedArea == null || desiredDirection.sqrMagnitude < 0.0001f)
        {
            return desiredDirection;
        }

        Vector3 normalizedDirection = desiredDirection.normalized;
        float magnitude = desiredDirection.magnitude;

        if (!WouldEnterSafeArea(origin, normalizedDirection, checkDistance))
        {
            return desiredDirection;
        }

        for (float angle = angleStep; angle <= maxSearchAngle; angle += angleStep)
        {
            Vector3 rightCandidate = Quaternion.AngleAxis(angle, Vector3.up) * normalizedDirection;
            if (!WouldEnterSafeArea(origin, rightCandidate, checkDistance))
            {
                return rightCandidate * magnitude;
            }

            Vector3 leftCandidate = Quaternion.AngleAxis(-angle, Vector3.up) * normalizedDirection;
            if (!WouldEnterSafeArea(origin, leftCandidate, checkDistance))
            {
                return leftCandidate * magnitude;
            }
        }

        // Nenhuma direção livre encontrada no leque testado (ex: já
        // encostado na fronteira, cercado por outra coisa do outro lado):
        // para, em vez de entrar no Hub.
        return Vector3.zero;
    }

    private bool WouldEnterSafeArea(Vector3 origin, Vector3 direction, float checkDistance)
    {
        Vector3 projectedPoint = origin + direction * checkDistance;
        Vector3 delta = projectedPoint - excludedArea.Center;
        delta.y = 0f;

        float effectiveRadius = excludedArea.Radius + margin;
        return delta.sqrMagnitude <= effectiveRadius * effectiveRadius;
    }
}