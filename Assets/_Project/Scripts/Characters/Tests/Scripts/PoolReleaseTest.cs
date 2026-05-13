using UnityEngine;

namespace Core.Exploration.Character.NPC.Enemy
{
    /// <summary>
    /// Teste de devolução manual: libera um inimigo específico de volta para a pool.
    /// Útil para validar o callback <see cref="ExplorationEnemyPooling.ReleaseEnemy"/>.
    /// Invoque <see cref="ReleaseTest"/> via UnityEvent ou pelo inspector em runtime.
    /// </summary>
    public class PoolReleaseTest : MonoBehaviour
    {
        [SerializeField] private ExplorationEnemyPooling   _pool;
        [SerializeField] private ExplorationEnemyController _target;

        public void ReleaseTest()
        {
            _pool.ReleaseEnemy(_target);
        }
    }
}
