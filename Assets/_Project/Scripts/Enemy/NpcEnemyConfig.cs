// ============================================================
// NpcEnemyConfig.cs
// Namespace : Systems.NPC.Enemy.Contracts
// ============================================================

using DetectionSystem.Core;
using UnityEngine;

namespace Systems.NPC.Enemy.Contracts
{
    public sealed class NpcEnemyConfig
    {
        // ── Posicionamento ────────────────────────────────────────────────

        public readonly Vector3 SpawnPoint;

        // ── Raios de comportamento ────────────────────────────────────────

        public readonly float WanderRadius;
        public readonly float ChaseRadius;
        public readonly float ContactDistance;

        // ── Tempo máximo em Wander ────────────────────────────────────────

        /// <summary>
        /// Segundos que o NPC pode permanecer vagando antes de retornar à pool.
        /// Evita NPCs em Wander eterno quando nunca detectam o Player.
        /// </summary>
        public readonly float WanderLifetime;

        // ── Detecção ──────────────────────────────────────────────────────

        public readonly Detector Detector;

        // ── Alvo de perseguição ───────────────────────────────────────────

        public Transform PursuitTarget;

        // ── Pool callback ─────────────────────────────────────────────────

        public readonly System.Action<INpcEnemy> OnReturnToPool;

        // ── Construtor ────────────────────────────────────────────────────

        public NpcEnemyConfig(
            Vector3 spawnPoint,
            float   wanderRadius,
            float   chaseRadius,
            float   contactDistance,
            float   wanderLifetime,
            Detector detector,
            System.Action<INpcEnemy> onReturnToPool)
        {
            SpawnPoint      = spawnPoint;
            WanderRadius    = wanderRadius;
            ChaseRadius     = chaseRadius;
            ContactDistance = contactDistance;
            WanderLifetime  = wanderLifetime;
            Detector        = detector;
            OnReturnToPool  = onReturnToPool;
            PursuitTarget   = null;
        }
    }
}