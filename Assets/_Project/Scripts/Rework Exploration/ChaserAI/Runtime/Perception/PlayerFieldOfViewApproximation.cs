using UnityEngine;

/// <summary>
/// Aproximação simplificada de "campo de visão do player" por distância,
/// usada pelas regras de posicionamento (<see cref="ChaserPlacementRules"/>)
/// para nunca nascer/realocar um chaser onde o player o veria aparecer.
///
/// viewPoint = player.position + player.forward * forwardOffset
///
/// Um ponto é "visível" se estiver a uma distância menor ou igual a <c>radius</c>
/// do viewPoint: geometricamente uma esfera, não um cone. Barato (uma distância,
/// sem raycast), mas não conhece obstáculos nem o ângulo real da câmera.
/// Se ficar impreciso, trocar por cone/raycast fica isolado neste arquivo.
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
}
