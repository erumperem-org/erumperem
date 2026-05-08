using UnityEngine;

public class PoolMassiveSpawnTest : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyPooling pool;
    [SerializeField] private ExplorationEnemyLevels level;

    [SerializeField] private int amount = 10;

    public void MassiveSpawnTest()
    {
        for(int i = 0; i < amount; i++)
        {
            pool.GetEnemy(level);
        }
    }
}
