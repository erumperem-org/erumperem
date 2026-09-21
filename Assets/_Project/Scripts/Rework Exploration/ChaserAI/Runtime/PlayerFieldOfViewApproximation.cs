using UnityEngine;

/// <summary>
/// Aproximação simplificada de "campo de visão do player" por distância,
/// já usada pelo <c>ChaserPool</c> para não spawnar Chasers visíveis (ver
/// README, seção "Fora do campo de visão"). Extraída para cá porque agora
/// também é usada pelo próprio <see cref="ChaserAI"/>, no início da cena,
/// para não nascer dentro da visão do player.
///
/// viewPoint = player.position + player.forward * forwardOffset
///
/// Um ponto é considerado "visível" se estiver a uma distância menor ou
/// igual a <c>radius</c> desse viewPoint - geometricamente uma esfera, não
/// um cone de visão real. Mesmo trade-off documentado no README: barato
/// (uma distância, sem raycast), mas não conhece obstáculos nem o ângulo
/// real de visão da câmera.
/// </summary>
public static class PlayerFieldOfViewApproximation
{
    public static Vector3 GetViewPoint(Transform player, float forwardOffset)
    {
        return player.position + player.forward * forwardOffset;
    }

    public static bool IsInside(Vector3 point, Transform player, float forwardOffset, float radius)
    {
        if (player == null)
        {
            return false;
        }

        Vector3 viewPoint = GetViewPoint(player, forwardOffset);
        return Vector3.Distance(point, viewPoint) <= radius;
    }

    /// <summary>
    /// Empurra <paramref name="point"/> para fora da esfera de visão em
    /// linha reta (a partir do viewPoint, passando por point), com uma
    /// margem extra. Usado como fallback quando nenhuma tentativa aleatória
    /// encontrou um ponto válido - não garante respeitar MapLimits/áreas
    /// excluídas, só garante sair da visão do player.
    /// </summary>
    public static Vector3 PushOutside(Vector3 point, Transform player, float forwardOffset, float radius, float margin)
    {
        Vector3 viewPoint = GetViewPoint(player, forwardOffset);
        Vector3 delta = point - viewPoint;
        delta.y = 0f;

        if (delta.sqrMagnitude <= 0.0001f)
        {
            // Ponto exatamente sobre o viewPoint - usa a direção oposta à
            // frente do player como fallback arbitrário, mas determinístico.
            delta = -player.forward;
            delta.y = 0f;

            if (delta.sqrMagnitude <= 0.0001f)
            {
                delta = Vector3.forward;
            }
        }

        Vector3 result = viewPoint + delta.normalized * (radius + margin);
        result.y = point.y;
        return result;
    }
}
