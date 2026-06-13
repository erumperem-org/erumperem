// ============================================================
// ISpawnPointSelector.cs
// Namespace : Systems.NPC.Spawner
// ============================================================

using UnityEngine;

namespace Systems.NPC.Spawner
{
    /// <summary>
    /// Abstrai a seleção e ordenação de spawn points.
    /// Permite trocar a estratégia (aleatória, por distância, etc.)
    /// sem alterar o NpcEnemySpawner.
    /// </summary>
    public interface ISpawnPointSelector
    {
        /// <summary>Retorna o próximo spawn point válido, ou null se nenhum disponível.</summary>
        Transform Next();

        /// <summary>True se há pelo menos um spawn point disponível.</summary>
        bool HasAny { get; }
    }
}
