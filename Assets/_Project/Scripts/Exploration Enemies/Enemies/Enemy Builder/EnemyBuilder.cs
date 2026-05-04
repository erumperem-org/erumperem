using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Enemies
{
    public interface IEnemyBuilder
    {
        static string EnemyPrefab { get => "Assets/System/Exploration Enemies/Enemy.prefab"; }
        ExplorationEnemyController CreateStandard(Vector3 position, Transform parent, GameObject target, GameObject prefab);
        public static ExplorationEnemyController SpawnEnemy(Vector3 position, Transform parent, GameObject prefab, string name)
        {
            GameObject instance = GameObject.Instantiate(prefab, position, Quaternion.identity, parent);
            instance.name = name;
            return instance.GetComponent<ExplorationEnemyController>();
        }
    }


    public class EnemyLowLevelBuilder : IEnemyBuilder
    {
        public ExplorationEnemyController CreateStandard(Vector3 position, Transform parent, GameObject target, GameObject prefab)
        {
            var enemy = IEnemyBuilder.SpawnEnemy(position, parent, prefab, "LowLevelEnemy"); ;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            enemy.data = new ExplorationEnemyData(
                ExplorationEnemiesLevel.Low,
                ExplorationEnemyStates.OnCreate,
                new ExplorationEnemyChasingData(enemy, agent, target, 2f, 500f),
                new ExplorationEnemyWanderingData(enemy, agent, 2f, 200, 200, 5, 5, 500f, target),
                new ExplorationEnemyOnPoolData(),
                new ExplorationEnemyOnDestroyData(),
                new ExplorationEnemyOnCreateData()
            );
            return enemy;
        }
    }
    public class EnemyMediumLevelBuilder : IEnemyBuilder
    {
        public ExplorationEnemyController CreateStandard(Vector3 position, Transform parent, GameObject target, GameObject prefab)
        {
            var enemy = IEnemyBuilder.SpawnEnemy(position, parent, prefab, "LowLevelEnemy"); ;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            enemy.data = new ExplorationEnemyData(
                ExplorationEnemiesLevel.Medium,
                ExplorationEnemyStates.OnCreate,
                new ExplorationEnemyChasingData(enemy, agent, target, 2f, 500f),
                new ExplorationEnemyWanderingData(enemy, agent, 2f, 200, 200, 5, 5, 500f, target),
                new ExplorationEnemyOnPoolData(),
                new ExplorationEnemyOnDestroyData(),
                new ExplorationEnemyOnCreateData()
            );
            return enemy;
        }
    }
    public class EnemyHighLevelBuilder : IEnemyBuilder
    {
        public ExplorationEnemyController CreateStandard(Vector3 position, Transform parent, GameObject target, GameObject prefab)
        {
            var enemy = IEnemyBuilder.SpawnEnemy(position, parent, prefab, "LowLevelEnemy"); ;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            enemy.data = new ExplorationEnemyData(
                ExplorationEnemiesLevel.High,
                ExplorationEnemyStates.OnCreate,
                new ExplorationEnemyChasingData(enemy, agent, target, 2f, 500f),
                new ExplorationEnemyWanderingData(enemy, agent, 2f, 200, 200, 5, 5, 500f, target),
                new ExplorationEnemyOnPoolData(),
                new ExplorationEnemyOnDestroyData(),
                new ExplorationEnemyOnCreateData()
            );
            return enemy;
        }
    }

}