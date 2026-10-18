using UnityEngine;

/// <summary>
/// Decide para onde o chaser vai enquanto não tem alvo (estado Wandering).
/// Chamada toda vez que ele chega ao destino anterior ou volta a Wandering.
/// Trocar a estratégia muda o "tipo" de chaser sem tocar na máquina de estados.
/// </summary>
public interface IRoamStrategy
{
    Vector3 GetNextDestination(Vector3 currentPosition);
}
