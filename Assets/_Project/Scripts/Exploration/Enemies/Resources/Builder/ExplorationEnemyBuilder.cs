using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class ExplorationEnemyBuilder
{
    public GameObject enemyPrefab;
    public static int enemyNumber = 0;

    public ExplorationEnemyController CreateEnemy(
        Vector3 spawnPosition,
        Transform parent,
        ExplorationEnemyLevels enemyLevel)
    {
        Vector3 validPosition = GetValidNavMeshPosition(spawnPosition);
        GameObject newObject  = GameObject.Instantiate(enemyPrefab, validPosition, Quaternion.identity, parent);

        ExplorationEnemyController controller = newObject.GetComponent<ExplorationEnemyController>();
        if (!controller)
        {
            controller = newObject.AddComponent<ExplorationEnemyController>();
        }

        if (!newObject.GetComponent<NavMeshAgent>())
        {
            newObject.AddComponent<NavMeshAgent>();
        }

        controller.data.agent = newObject.GetComponent<NavMeshAgent>();
        _ = ExplorationEnemyController.SetEnemyLevel(controller, enemyLevel);
        controller.data.enemyId       = $"Enemy {enemyNumber:000}";
        controller.data.patrolRadius  = 50f;
        controller.data.perceptionRadius = 10f;
        enemyNumber++;

        return controller;
    }

    private Vector3 GetValidNavMeshPosition(Vector3 desiredPosition, float maxDistance = 10f)
    {
        if (NavMesh.SamplePosition(
                desiredPosition,
                out NavMeshHit hit,
                maxDistance,
                NavMesh.GetAreaFromName("Walkable")))
        {
            return hit.position;
        }

        return desiredPosition;
    }

    public void DestroyEnemy(ExplorationEnemyController enemy)
    {
        GameObject.Destroy(enemy.gameObject);
    }
}
