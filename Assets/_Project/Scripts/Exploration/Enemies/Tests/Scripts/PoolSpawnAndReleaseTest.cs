using System.Collections;
using UnityEngine;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Teste de spawn com devolução automática após um delay.
    /// Spawna um inimigo e o libera de volta à pool após <see cref="ReleaseDelay"/> segundos.
    /// Invoque <see cref="SpawnAndReleaseTestFunction"/> via UnityEvent ou pelo inspector.
    /// </summary>
    public class PoolSpawnAndReleaseTest : MonoBehaviour
    {
        [SerializeField] private ExplorationEnemyPooling _pool;
        [SerializeField] private ExplorationEnemyLevels  _level;

        [Tooltip("Segundos até o inimigo ser devolvido à pool após o spawn.")]
        [SerializeField] private float _releaseDelay = 5f;

        public void SpawnAndReleaseTestFunction()
        {
            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            ExplorationEnemyController enemy = _pool.GetEnemy(_level);
            yield return new WaitForSeconds(_releaseDelay);
            _pool.ReleaseEnemy(enemy);
        }
    }
}
