// ============================================================
// GridPoolStorage.cs
// Namespace : Systems.NPC.Pool
// ============================================================
// Responsabilidade única: calcular e aplicar posicionamento
// em grade para NPCs inativos da pool.
//
// CORREÇÕES:
//   • PositionFor agora usa largura de coluna configurável em vez
//     de 2 colunas fixas — evita grade excessivamente alta em Z
//     para pools grandes.
//   • Construtor aceita columnCount para que NpcEnemyPool passe
//     um valor proporcional ao tamanho da pool (ex: raiz quadrada).
// ============================================================

using Systems.NPC.Enemy;
using UnityEngine;

namespace Systems.NPC.Pool
{
    public sealed class GridPoolStorage : IPoolStorage
    {
        private readonly Vector3 _origin;
        private readonly float   _spacing;
        private readonly int     _columnCount;

        /// <param name="origin">Posição de origem da grade.</param>
        /// <param name="spacing">Espaçamento entre slots.</param>
        /// <param name="columnCount">Número de colunas. Padrão 4.</param>
        public GridPoolStorage(Vector3 origin, float spacing, int columnCount = 4)
        {
            _origin      = origin;
            _spacing     = spacing;
            _columnCount = Mathf.Max(1, columnCount);
        }

        public Vector3 PositionFor(int index)
        {
            int col = index % _columnCount;
            int row = index / _columnCount;
            return _origin + new Vector3(col * _spacing, 0f, row * _spacing);
        }

        public void StoreAt(NpcEnemy enemy, int index)
        {
            enemy.transform.position = PositionFor(index);
            enemy.transform.rotation = Quaternion.identity;
        }
    }
}