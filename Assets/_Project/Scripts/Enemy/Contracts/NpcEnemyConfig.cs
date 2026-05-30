// ============================================================
// NpcEnemyConfig.cs
// Namespace : Systems.NPC.Enemy.Contracts
// ============================================================
// Campo Detector removido: era passado pelo Builder mas nunca
// lido pelo NpcEnemy (que já obtém o Detector via Awake).
// Elimina o campo morto e a dependência desnecessária.
// ============================================================

using UnityEngine;

namespace Systems.NPC.Enemy.Contracts
{
    public sealed class NpcEnemyConfig
    {
        public readonly Vector3 SpawnPoint;
        public readonly float   WanderRadius;
        public readonly float   ChaseRadius;
        public readonly float   ContactDistance;
        public readonly float   WanderLifetime;

        public Transform PursuitTarget;

        public readonly System.Action<INpcEnemy> OnReturnToPool;

        public NpcEnemyConfig(
            Vector3 spawnPoint,
            float   wanderRadius,
            float   chaseRadius,
            float   contactDistance,
            float   wanderLifetime,
            System.Action<INpcEnemy> onReturnToPool)
        {
            SpawnPoint      = spawnPoint;
            WanderRadius    = wanderRadius;
            ChaseRadius     = chaseRadius;
            ContactDistance = contactDistance;
            WanderLifetime  = wanderLifetime;
            OnReturnToPool  = onReturnToPool;
            PursuitTarget   = null;
        }
    }
}
