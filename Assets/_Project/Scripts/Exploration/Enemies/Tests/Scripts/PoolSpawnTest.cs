using UnityEngine;

public class PoolSpawnTest : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyPooling pool;
    [SerializeField] private ExplorationEnemyLevels level;

    public void SpawnTest()
    {
        pool.GetEnemy(level);
    }
}
