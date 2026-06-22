// ============================================================
// NpcEnemyBuilder.cs
// Namespace : Systems.NPC.Builder
// ============================================================
// Responsabilidade única: montar o NPC conectando pool,
// config e sistemas externos.
//
// CORREÇÕES:
//   [9] Build() usa o playerTransform injetado pelo Spawner para
//       calcular um centro deslocado pelo raio mínimo antes de
//       chamar TryGetPosition — sem inventar API inexistente.
//       NpcEnemySpawner injeta via SetPlayerTransform().
// ============================================================

using DetectionSystem.Core;
using Services.Spawning;
using Systems.NPC.Enemy;
using Systems.NPC.Enemy.Contracts;
using Systems.NPC.Pool;
using UnityEngine;

namespace Systems.NPC.Builder
{
    public sealed class NpcEnemyBuilder : MonoBehaviour
    {
        [Header("Dependências")]
        [SerializeField] private NpcEnemyPool                    _pool;
        [SerializeField] private NavMeshSpawnPositionServiceMono _spawnService;
        [SerializeField] private NpcEnemyContactHandler          _contactHandler;
        [SerializeField] private ExplorationCorruptionSystem      corruptionSystem;

        [Header("Comportamento")]
        [SerializeField, Min(1f)]   private float _wanderRadius    = 8f;
        [SerializeField, Min(1f)]   private float _chaseRadius     = 40f;
        [SerializeField, Min(0.1f)] private float _contactDistance = 1.2f;

        [Tooltip("Tempo máximo (s) em Wander antes de retornar à pool.")]
        [SerializeField, Min(1f)]   private float _wanderLifetime  = 30f;

        [Tooltip("Distância mínima do player para spawn aleatório (sem spawn points configurados). " +
                 "Deve coincidir com o PlayerMinSpawnRadius do NpcEnemySpawner.")]
        [SerializeField, Min(0f)]   private float _minSpawnRadiusFromPlayer = 10f;

        // [9] Transform do Main atual, injetado pelo NpcEnemySpawner via SetPlayerTransform().
        private Transform _playerTransform;

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Injeta o Transform do personagem Main atual.
        /// Chamado pelo NpcEnemySpawner sempre que o Main muda.
        /// </summary>
        public void SetPlayerTransform(Transform playerTransform)
        {
            _playerTransform = playerTransform;
        }

        public bool Build(Vector3 spawnCenter = default)
        {
            if (!ValidateDependenciesForRandomSpawn()) return false;
            if (!_pool.HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Pool esgotada.");
                return false;
            }

            Vector3 spawnPoint;
            bool    found;

            if (spawnCenter != Vector3.zero)
            {
                // Centro explícito fornecido pelo chamador.
                found = _spawnService.TryGetPosition(spawnCenter, _wanderRadius * 2f, out spawnPoint);
            }
            else if (_playerTransform != null && _minSpawnRadiusFromPlayer > 0f)
            {
                // [9] Calcula um centro candidato deslocado do player pelo raio mínimo
                //     em direção aleatória — sem precisar de API nova no serviço.
                Vector2 randomDir2D = Random.insideUnitCircle.normalized;
                var     offset      = new Vector3(randomDir2D.x, 0f, randomDir2D.y)
                                      * _minSpawnRadiusFromPlayer;
                Vector3 candidateCenter = _playerTransform.position + offset;

                found = _spawnService.TryGetPosition(candidateCenter, _wanderRadius, out spawnPoint);

                // Fallback sem restrição se o candidato não tiver NavMesh acessível.
                if (!found)
                    found = _spawnService.TryGetPosition(out spawnPoint);
            }
            else
            {
                found = _spawnService.TryGetPosition(out spawnPoint);
            }

            if (!found)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Nenhuma posição de spawn válida encontrada no NavMesh.");
                return false;
            }

            return SpawnAt_Internal(spawnPoint);
        }

        public bool BuildAt(Vector3 exactSpawnPoint)
        {
            if (!ValidateDependenciesForExactSpawn()) return false;
            if (!_pool.HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Pool esgotada.");
                return false;
            }

            return SpawnAt_Internal(exactSpawnPoint);
        }

        // ── Implementação ─────────────────────────────────────────────────

        private bool SpawnAt_Internal(Vector3 spawnPoint)
        {
            NpcEnemy npc = _pool.Get();
            if (npc == null) return false;

            var config = new NpcEnemyConfig(
                spawnPoint      : spawnPoint,
                wanderRadius    : _wanderRadius,
                chaseRadius     : _chaseRadius,
                contactDistance : _contactDistance,
                wanderLifetime  : _wanderLifetime,
                onReturnToPool  : (enemy) =>
                {
                    if (enemy is NpcEnemy concreteEnemy)
                    {
                        _contactHandler?.Unregister(enemy);
                        _pool.Return(concreteEnemy);
                    }
                }
            );

            npc.Initialize(config);
            npc.Activate();

            _contactHandler?.Register(npc);

            Debug.Log($"[NpcEnemyBuilder] '{npc.name}' ativado em {spawnPoint} " +
                      $"[Wander: {_wanderRadius}m | Chase: {_chaseRadius}m | Lifetime: {_wanderLifetime}s]", npc);

            return true;
        }

        private void Awake() => ResolveDependencies();

        private void ResolveDependencies()
        {
            Transform enemySystemRoot = transform.parent;
            if (enemySystemRoot == null) return;

            if (_pool == null)
                _pool = enemySystemRoot.GetComponentInChildren<NpcEnemyPool>(true);

            if (_spawnService == null)
                _spawnService = enemySystemRoot.GetComponentInChildren<NavMeshSpawnPositionServiceMono>(true);

            if (_contactHandler == null)
                _contactHandler = enemySystemRoot.GetComponentInChildren<NpcEnemyContactHandler>(true);
        }

        private bool ValidateDependenciesForExactSpawn()
        {
            ResolveDependencies();
            if (_pool != null) return true;
            Debug.LogError("[NpcEnemyBuilder] Pool não configurada!", this);
            return false;
        }

        private bool ValidateDependenciesForRandomSpawn()
        {
            ResolveDependencies();
            if (_pool != null && _spawnService != null) return true;
            Debug.LogError("[NpcEnemyBuilder] Pool ou SpawnService não configurados!", this);
            return false;
        }
    }
}