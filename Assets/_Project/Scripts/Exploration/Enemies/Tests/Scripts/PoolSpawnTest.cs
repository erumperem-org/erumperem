using UnityEngine;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Teste simples de spawn único: solicita um inimigo à pool.
    /// Útil para validar o callback OnGet e o início do PatrolBehavior.
    /// Invoque <see cref="SpawnTest"/> via UnityEvent ou pelo inspector em runtime.
    /// </summary>
    public class PoolSpawnTest : MonoBehaviour
    {
        [SerializeField] private ExplorationEnemyPooling _pool;
        [SerializeField] private ExplorationEnemyLevels  _level;

        public void SpawnTest()
        {
            _pool.GetEnemy(_level);
        }
    }
}
