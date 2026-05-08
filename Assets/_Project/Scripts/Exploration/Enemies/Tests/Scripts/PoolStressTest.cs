using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolStressTest : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyPooling pool;
    [SerializeField] private ExplorationEnemyLevels level;

    [SerializeField] private int amount = 20;
    [SerializeField] private float releaseDelay = 60f;

    private readonly List<ExplorationEnemyController> spawned =
        new();

    public void StressTest()
    {
        StartCoroutine(TestRoutine());
    }

    private IEnumerator TestRoutine()
    {
        spawned.Clear();

        for(int i = 0; i < amount; i++)
        {
            ExplorationEnemyController enemy =
                pool.GetEnemy(level);

            spawned.Add(enemy);
        }

        yield return new WaitForSeconds(releaseDelay);

        foreach(ExplorationEnemyController enemy in spawned)
        {
            pool.ReleaseEnemy(enemy);
        }
    }
}
