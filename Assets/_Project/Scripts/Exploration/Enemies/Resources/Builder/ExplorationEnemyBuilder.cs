using System;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Fábrica responsável por instanciar e destruir GameObjects de inimigos.
    /// Garante que o prefab seja posicionado em um ponto válido do NavMesh
    /// e que todos os componentes necessários estejam presentes.
    /// </summary>
    [Serializable]
    public class ExplorationEnemyBuilder
    {
        [Tooltip("Prefab do inimigo. Deve conter (ou receber em runtime) um ExplorationEnemyController e um NavMeshAgent.")]
        public GameObject EnemyPrefab;

        [Tooltip("Raio de percepção padrão atribuído aos inimigos criados por este builder.")]
        public float DefaultPerceptionRadius = 10f;

        [Tooltip("Raio de patrulha padrão atribuído aos inimigos criados por este builder.")]
        public float DefaultPatrolRadius = 50f;

        [Tooltip("Distância máxima de busca no NavMesh ao validar a posição de spawn.")]
        public float NavMeshSampleDistance = 10f;

        /// <summary>Contador global, incrementado a cada inimigo criado. Pode ser resetado pela pool.</summary>
        public static int EnemyNumber = 0;

        // ── API pública ────────────────────────────────────────────────────────────

        /// <summary>
        /// Instancia um inimigo a partir de <see cref="EnemyPrefab"/>, o posiciona em um
        /// ponto válido do NavMesh próximo a <paramref name="spawnPosition"/> e configura seus dados iniciais.
        /// </summary>
        /// <param name="spawnPosition">Posição desejada de spawn.</param>
        /// <param name="parent">Transform pai do objeto instanciado.</param>
        /// <param name="enemyLevel">Nível inicial do inimigo.</param>
        public ExplorationEnemyController CreateEnemy(
            Vector3 spawnPosition,
            Transform parent,
            ExplorationEnemyLevels enemyLevel)
        {
            Vector3 validPosition = GetValidNavMeshPosition(spawnPosition);
            GameObject newObject  = GameObject.Instantiate(EnemyPrefab, validPosition, Quaternion.identity, parent);

            // Garante o controller; adiciona se o prefab não tiver
            ExplorationEnemyController controller = newObject.GetComponent<ExplorationEnemyController>();
            if (!controller)
                controller = newObject.AddComponent<ExplorationEnemyController>();

            // Garante o NavMeshAgent; adiciona se o prefab não tiver
            if (!newObject.GetComponent<NavMeshAgent>())
                newObject.AddComponent<NavMeshAgent>();

            // Configura os dados do inimigo
            controller.Data.Agent           = newObject.GetComponent<NavMeshAgent>();
            controller.Data.EnemyId         = $"Enemy {EnemyNumber:000}";
            controller.Data.PatrolRadius    = DefaultPatrolRadius;
            controller.Data.PerceptionRadius = DefaultPerceptionRadius;

            _ = ExplorationEnemyController.SetEnemyLevel(controller, enemyLevel);

            EnemyNumber++;

            return controller;
        }

        /// <summary>
        /// Destrói o GameObject do inimigo. Chamado pela pool ao descartar um item permanentemente.
        /// </summary>
        public void DestroyEnemy(ExplorationEnemyController enemy)
        {
            GameObject.Destroy(enemy.gameObject);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Busca o ponto navegável mais próximo de <paramref name="desiredPosition"/> no NavMesh.
        /// Retorna a posição original se nenhum ponto for encontrado dentro de <see cref="NavMeshSampleDistance"/>.
        /// </summary>
        private Vector3 GetValidNavMeshPosition(Vector3 desiredPosition)
        {
            if (NavMesh.SamplePosition(
                    desiredPosition,
                    out NavMeshHit hit,
                    NavMeshSampleDistance,
                    NavMesh.GetAreaFromName("Walkable")))
            {
                return hit.position;
            }

            return desiredPosition;
        }
    }
}
