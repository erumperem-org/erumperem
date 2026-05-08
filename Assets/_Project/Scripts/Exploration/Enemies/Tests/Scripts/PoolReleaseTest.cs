using UnityEngine;

public class PoolReleaseTest : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyPooling pool;
    [SerializeField] private ExplorationEnemyController target;

    public void ReleaseTest()
    {
        pool.ReleaseEnemy(target);
    }
}
