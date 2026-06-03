// ============================================================
// GridPoolStorage.cs
// Namespace : Systems.NPC.Pool
// ============================================================
// Responsabilidade única: calcular e aplicar posicionamento
// em grade para NPCs inativos da pool.
//
// Extraído de NpcEnemyPool onde era uma responsabilidade
// secundária junto à gestão de disponibilidade.
// ============================================================

using Systems.NPC.Enemy;
using UnityEngine;

namespace Systems.NPC.Pool
{
    public sealed class GridPoolStorage : IPoolStorage
    {
        private readonly Vector3 _origin;
        private readonly float   _spacing;

        public GridPoolStorage(Vector3 origin, float spacing)
        {
            _origin  = origin;
            _spacing = spacing;
        }

        public Vector3 PositionFor(int index)
        {
            int col = index % 2;
            int row = index / 2;
            return _origin + new Vector3(col * _spacing, 0f, row * _spacing);
        }

        public void StoreAt(NpcEnemy enemy, int index)
        {
            enemy.transform.position = PositionFor(index);
            enemy.transform.rotation = Quaternion.identity;
        }
    }
}
