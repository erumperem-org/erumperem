// ============================================================
// NpcEnemyBuilder.cs
// Namespace : Systems.NPC.Builder
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
        [SerializeField] private NpcEnemyPool _pool;
        [SerializeField] private NavMeshSpawnPositionServiceMono _spawnService;

        [Header("Comportamento")]
        [SerializeField, Min(1f)]   private float _wanderRadius    = 8f;
        [SerializeField, Min(1f)]   private float _chaseRadius     = 20f;
        [SerializeField, Min(0.1f)] private float _contactDistance = 1.2f;

        [Tooltip("Tempo máximo (segundos) que o NPC pode ficar em Wander antes de retornar à pool e ser realocado.")]
        [SerializeField, Min(1f)]   private float _wanderLifetime  = 30f;

        // ── API pública ───────────────────────────────────────────────────

        public bool Build(Vector3 spawnCenter = default)
        {
            if (!ValidateDependencies()) return false;
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
            if (!ValidateDependencies()) return false;
            if (!_pool.HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Pool esgotada.");
                return false;
            }

            return SpawnAt_Internal(exactSpawnPoint);
        }

        // ── Implementação compartilhada ───────────────────────────────────

        private bool SpawnAt_Internal(Vector3 spawnPoint)
        {
            NpcEnemy npc = _pool.Get();
            if (npc == null) return false;

            var detector = npc.GetComponent<Detector>();
            if (detector == null)
            {
                Debug.LogError($"[NpcEnemyBuilder] '{npc.name}' sem Detector.", this);
                _pool.Return(npc);
                return false;
            }

            var config = new NpcEnemyConfig(
                spawnPoint      : spawnPoint,
                wanderRadius    : _wanderRadius,
                chaseRadius     : _chaseRadius,
                contactDistance : _contactDistance,
                wanderLifetime  : _wanderLifetime,
                detector        : detector,
                onReturnToPool  : (enemy) => _pool.Return(enemy)
            );

            npc.Initialize(config);
            npc.Activate();

            Debug.Log($"[NpcEnemyBuilder] '{npc.name}' ativado em {spawnPoint} " +
                      $"[Wander: {_wanderRadius}m | Chase: {_chaseRadius}m | Lifetime: {_wanderLifetime}s]", npc);

            return true;
        }

        private bool ValidateDependencies()
        {
            if (_pool != null && _spawnService != null) return true;
            Debug.LogError("[NpcEnemyBuilder] Pool ou SpawnService não configurados!", this);
            return false;
        }
    }
}