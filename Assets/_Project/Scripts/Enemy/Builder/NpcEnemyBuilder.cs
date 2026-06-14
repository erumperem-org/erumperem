// ============================================================
// NpcEnemyBuilder.cs
// Namespace : Systems.NPC.Builder
// ============================================================
// Responsabilidade única: montar o NPC conectando pool,
// config e sistemas externos.
//
// Mudanças em relação à versão original:
//   • Não passa mais Detector no NpcEnemyConfig (campo removido —
//     era um campo morto; o NpcEnemy já obtém o Detector via Awake).
//   • Registra o NPC no NpcEnemyContactHandler após Activate().
//   • Chama pool.Return(NpcEnemy) diretamente, sem cast de interface.
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

        [Header("Comportamento")]
        [SerializeField, Min(1f)]   private float _wanderRadius    = 8f;
        [SerializeField, Min(1f)]   private float _chaseRadius     = 40f;
        [SerializeField, Min(0.1f)] private float _contactDistance = 1.2f;

        [Tooltip("Tempo máximo (s) em Wander antes de retornar à pool.")]
        [SerializeField, Min(1f)]   private float _wanderLifetime  = 30f;

        // ── API pública ───────────────────────────────────────────────────

        public bool Build(Vector3 spawnCenter = default)
        {
            if (!ValidateDependenciesForRandomSpawn()) return false;
            if (!_pool.HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Pool esgotada.");
                return false;
            }

            Vector3 spawnPoint;
            bool found = spawnCenter == Vector3.zero
                ? _spawnService.TryGetPosition(out spawnPoint)
                : _spawnService.TryGetPosition(spawnCenter, _wanderRadius * 2f, out spawnPoint);

            if (!found)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Nenhuma posição de spawn válida.");
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
                    // Cast seguro: o Builder sabe que só coloca NpcEnemy na pool
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
