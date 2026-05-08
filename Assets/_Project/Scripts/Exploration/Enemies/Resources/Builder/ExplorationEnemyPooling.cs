using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

public class ExplorationEnemyPooling : MonoBehaviour
{
    [SerializeField] private ExplorationEnemyBuilder builder;
    [SerializeField] private Vector3 poolPosition;
    public Transform pooledObjectsParent;
    public Transform activeObjectsParent;
    private ObjectPool<ExplorationEnemyController> enemyPool;

    [Header("Default Enemy Level")]
    [SerializeField] private ExplorationEnemyLevels nextEnemyLevelToCreate;
    public Transform player;

    private void Awake()
    {
        enemyPool = new ObjectPool<ExplorationEnemyController>(
            createFunc: CreateEnemy,
            actionOnGet: OnGet,
            actionOnRelease: OnRelease,
            actionOnDestroy: OnDestroyItem,
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: 20
        );
    }

    private ExplorationEnemyController CreateEnemy()
    {
        return builder.CreateEnemy(
            GetRandomNavMeshPoint(Vector3.zero, 50f),
            activeObjectsParent,
            nextEnemyLevelToCreate
        );
    }

    private void OnGet(ExplorationEnemyController controller)
    {
        controller.transform.position = GetRandomNavMeshPoint(Vector3.zero, 50f);
        controller.gameObject.SetActive(true);

        if (controller.data.enemyLevel != nextEnemyLevelToCreate)
        {
            _ = ExplorationEnemyController.SetEnemyLevel(controller, nextEnemyLevelToCreate);
        }

        _ = ExplorationEnemyController.SetEnemyStartegy(
            controller,
            new PatrolBehavior(),
            new PatrolBehaviorContext(controller, player, controller.data.perceptionRadius)
        );
    }

    private void OnRelease(ExplorationEnemyController controller)
    {
        _ = SetPoolState(controller);
    }

    private async Task SetPoolState(ExplorationEnemyController controller)
    {
        await ExplorationEnemyController.SetEnemyStartegy(
            controller,
            new OnPoolBehavior(),
            new OnPoolBehaviorContext(controller, poolPosition, pooledObjectsParent)
        );

        controller.gameObject.SetActive(false);
    }

    private void OnDestroyItem(ExplorationEnemyController controller)
    {
        builder.DestroyEnemy(controller);
    }

    public ExplorationEnemyController GetEnemy(ExplorationEnemyLevels level)
    {
        nextEnemyLevelToCreate = level;
        ExplorationEnemyController enemy = enemyPool.Get();
        enemy.transform.SetParent(activeObjectsParent);
        return enemy;
    }

    public void ReleaseEnemy(ExplorationEnemyController controller)
    {
        enemyPool.Release(controller);
    }

    private IEnumerator ReturnAfter(ExplorationEnemyController controller, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        enemyPool.Release(controller);
    }

    private Vector3 GetRandomNavMeshPoint(Vector3 center, float range)
    {
        Vector3 randomPoint = center + Random.insideUnitSphere * range;
        bool foundPosition = NavMesh.SamplePosition(
            randomPoint, out NavMeshHit hit, range, NavMesh.AllAreas);

        return foundPosition ? hit.position : Vector3.zero;
    }
}
