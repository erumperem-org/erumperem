using UnityEngine;

/// <summary>Estratégia padrão: sorteia pontos dentro dos limites do mapa, fora da área segura.</summary>
public sealed class WanderAreaRoam : IRoamStrategy
{
    private readonly WanderArea area;

    public WanderAreaRoam(WanderArea area)
    {
        this.area = area;
    }

    public Vector3 GetNextDestination(Vector3 currentPosition)
    {
        return area.GetRandomPoint();
    }
}
