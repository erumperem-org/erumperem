using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apenas o registro (lista) dos chasers existentes. NÃO faz pooling, nem reposiciona, nem
/// liga/desliga ninguém: criação é do <see cref="ChaserSpawner"/> e realocação do
/// <see cref="ChaserRelocator"/>.
///
/// O nome é mantido porque o EnemyHuntOrchestrator (que não foi alterado) referencia o tipo
/// <c>ChaserPool</c> e chama <see cref="GetManagedChasers"/> para saber em quem se inscrever.
/// </summary>
public class ChaserPool : MonoBehaviour
{
    private readonly List<ChaserAI> chasers = new List<ChaserAI>();

    public int PoolSize => chasers.Count;

    public IReadOnlyList<ChaserAI> GetManagedChasers() => chasers;

    public void Register(ChaserAI chaser)
    {
        if (chaser != null && !chasers.Contains(chaser))
        {
            chasers.Add(chaser);
        }
    }

    public void Unregister(ChaserAI chaser)
    {
        chasers.Remove(chaser);
    }
}
