using UnityEngine;
using UnityEngine.Pool;
using System.Collections;
using System.Threading.Tasks;

namespace Core.Exploration.Enemies
{
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Low Level Build Settings")]
        public int lowMinOnPool;
        public int lowMaxOnPool;

        [Header("Medium Level Build Settings")]
        public int midMinOnPool;
        public int midMaxOnPool;

        [Header("High Level Build Settings")]
        public int highMinOnPool;
        public int highMaxOnPool;

        [Header("Pools")]
        public ObjectPool<ExplorationEnemyController> lowEnemy;
        public ObjectPool<ExplorationEnemyController> midEnemy;
        public ObjectPool<ExplorationEnemyController> highEnemy;

        [Header("Player")]
        public GameObject player;
        [Header("Enemy")]
        public GameObject enemyPrefab;
        private EnemyLowLevelBuilder lowLevelBuilder;
        private EnemyMediumLevelBuilder mediumLevelBuilder;
        private EnemyHighLevelBuilder highLevelBuilder;
        void Awake()
        {
            lowLevelBuilder = new EnemyLowLevelBuilder();
            mediumLevelBuilder = new EnemyMediumLevelBuilder();
            highLevelBuilder = new EnemyHighLevelBuilder();

            lowEnemy = new ObjectPool<ExplorationEnemyController>(
                createFunc: () => CreateStandard(lowLevelBuilder),
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: true,
                defaultCapacity: lowMinOnPool,
                maxSize: lowMaxOnPool
            );

            midEnemy = new ObjectPool<ExplorationEnemyController>(
                createFunc: () => CreateStandard(mediumLevelBuilder),
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: true,
                defaultCapacity: midMinOnPool,
                maxSize: midMaxOnPool
            );

            highEnemy = new ObjectPool<ExplorationEnemyController>(
                createFunc: () => CreateStandard(highLevelBuilder),
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyItem,
                collectionCheck: true,
                defaultCapacity: highMinOnPool,
                maxSize: highMaxOnPool
            );
        }
        private ExplorationEnemyController CreateStandard(IEnemyBuilder builder)
        {
            var enemy = builder.CreateStandard(GetSpawnPosition(), this.transform, player, enemyPrefab);
            enemy.StateChange(ExplorationEnemyStates.OnCreate);
            enemy.gameObject.SetActive(false);
            return enemy;
        }

        private void OnGet(ExplorationEnemyController enemy)
        {
            enemy.gameObject.SetActive(true);
            enemy.InitializeState();
        }

        private void OnRelease(ExplorationEnemyController enemy)
        {
            enemy.gameObject.SetActive(false);
            enemy.StateChange(ExplorationEnemyStates.OnPool);
        }

        private void OnDestroyItem(ExplorationEnemyController enemy)
        {
            enemy.StateChange(ExplorationEnemyStates.OnDestroy);
            Destroy(enemy.gameObject);
        }

        private IEnumerator ReturnAfter(
            ExplorationEnemyController enemy,
            float seconds,
            ObjectPool<ExplorationEnemyController> pool)
        {
            yield return new WaitForSeconds(seconds);
            pool.Release(enemy);
        }

        public ExplorationEnemyController Spawn(ExplorationEnemiesLevel level, float returnTime = -1f)
        {
            ExplorationEnemyController enemy = null;

            switch (level)
            {
                case ExplorationEnemiesLevel.Low:
                    enemy = lowEnemy.Get();
                    break;

                case ExplorationEnemiesLevel.Medium:
                    enemy = midEnemy.Get();
                    break;

                case ExplorationEnemiesLevel.High:
                    enemy = highEnemy.Get();
                    break;
            }

            enemy.transform.position = GetSpawnPosition();
            if (returnTime > 0)
            {
                StartCoroutine(ReturnAfter(enemy, returnTime, GetPool(level)));
            }

            return enemy;
        }

        private ObjectPool<ExplorationEnemyController> GetPool(ExplorationEnemiesLevel level)
        {
            return level switch
            {
                ExplorationEnemiesLevel.Low => lowEnemy,
                ExplorationEnemiesLevel.Medium => midEnemy,
                ExplorationEnemiesLevel.High => highEnemy,
                _ => lowEnemy
            };
        }

        private Vector3 GetSpawnPosition() => new Vector3(
            Random.Range(player.transform.position.x + 200, player.transform.position.x + 400), 
            player.transform.position.y, Random.Range(player.transform.position.z + 200, 
            player.transform.position.z + 400));

        
    }
}