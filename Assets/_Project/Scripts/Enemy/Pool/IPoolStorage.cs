// ============================================================
// IPoolStorage.cs
// Namespace : Systems.NPC.Pool
// ============================================================

using Systems.NPC.Enemy;
using UnityEngine;

namespace Systems.NPC.Pool
{
    /// <summary>
    /// Abstrai o posicionamento físico dos NPCs inativos no mundo.
    /// Permite trocar a estratégia de storage sem alterar a NpcEnemyPool.
    /// </summary>
    public interface IPoolStorage
    {
        Vector3 PositionFor(int index);
        void StoreAt(NpcEnemy enemy, int index);
    }
}
