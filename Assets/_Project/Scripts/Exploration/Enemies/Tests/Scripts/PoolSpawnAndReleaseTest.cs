using System.Collections;
using UnityEngine;

public class PoolSpawnAndReleaseTest : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyPooling pool;
    [SerializeField] private ExplorationEnemyLevels level;

    [SerializeField] private float releaseDelay = 5f;

    public void SpawnAndReleaseTestFunction()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        ExplorationEnemyController enemy =
            pool.GetEnemy(level);

        yield return new WaitForSeconds(releaseDelay);

        pool.ReleaseEnemy(enemy);
    }
}
