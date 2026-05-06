using Core.Exploration.Enemies;
using UnityEngine;

public class SpawnerTeste : MonoBehaviour
{
    public EnemySpawner enemySpawner;

    public void SpawnLow() { enemySpawner.Spawn(ExplorationEnemiesLevel.Low); }
    public void SpawnMid() { enemySpawner.Spawn(ExplorationEnemiesLevel.Medium);}
    public void SpawnHigh() { enemySpawner.Spawn(ExplorationEnemiesLevel.High); }

}
