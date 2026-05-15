using UnityEngine;

namespace Core.Exploration.Character.NPC.Enemy
{
    /// <summary>
    /// Teste de spawn massivo: solicita <see cref="Amount"/> inimigos de uma vez à pool.
    /// Útil para validar o comportamento da pool quando muitos inimigos são ativados simultaneamente.
    /// Invoque <see cref="MassiveSpawnTest"/> via UnityEvent ou pelo inspector em runtime.
    /// </summary>
    public class PoolMassiveSpawnTest : MonoBehaviour
    {
        [SerializeField] private ExplorationEnemyPooling _pool;
        [SerializeField] private ExplorationEnemyLevels  _level;

        [Tooltip("Quantidade de inimigos a spawnar de uma vez.")]
        [SerializeField] private int _amount = 10;

        public void MassiveSpawnTest()
        {
            for (int i = 0; i < _amount; i++)
                _pool.GetEnemy(_level);
        }
    }
}
