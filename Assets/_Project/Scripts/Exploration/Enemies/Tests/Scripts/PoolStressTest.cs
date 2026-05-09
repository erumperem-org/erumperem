using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Teste de stress da pool: spawna <see cref="Amount"/> inimigos simultaneamente
    /// e os libera todos após <see cref="ReleaseDelay"/> segundos.
    /// Útil para identificar gargalos de performance e validar a estabilidade da pool sob carga.
    /// Invoque <see cref="StressTest"/> via UnityEvent ou pelo inspector em runtime.
    /// </summary>
    public class PoolStressTest : MonoBehaviour
    {
        [SerializeField] private ExplorationEnemyPooling _pool;
        [SerializeField] private ExplorationEnemyLevels  _level;

        [Tooltip("Quantidade de inimigos a spawnar durante o teste.")]
        [SerializeField] private int _amount = 20;

        [Tooltip("Segundos até todos os inimigos serem liberados de volta à pool.")]
        [SerializeField] private float _releaseDelay = 60f;

        private readonly List<ExplorationEnemyController> _spawned = new();

        public void StressTest()
        {
            StartCoroutine(TestRoutine());
        }

        private IEnumerator TestRoutine()
        {
            _spawned.Clear();

            for (int i = 0; i < _amount; i++)
            {
                ExplorationEnemyController enemy = _pool.GetEnemy(_level);
                _spawned.Add(enemy);
            }

            yield return new WaitForSeconds(_releaseDelay);

            foreach (ExplorationEnemyController enemy in _spawned)
                _pool.ReleaseEnemy(enemy);
        }
    }
}
