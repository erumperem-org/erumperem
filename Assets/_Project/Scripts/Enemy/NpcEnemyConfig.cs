// ============================================================
// NpcEnemyConfig.cs
// Namespace : Systems.NPC.Enemy.Contracts
// ============================================================
// Configuração imutável fornecida pelo Builder ao NPC.
// Centraliza todos os parâmetros que o inimigo precisa para
// funcionar corretamente, sem acoplamento direto à pool ou builder.
// ============================================================

using DetectionSystem.Core;
using Services.Navigation;
using UnityEngine;

namespace Systems.NPC.Enemy.Contracts
{
    /// <summary>
    /// Dados de configuração transferidos do <see cref="NpcEnemyBuilder"/>
    /// para o <see cref="NpcEnemy"/> no momento do spawn/reaproveitamento.
    ///
    /// Tudo que varia por ciclo de vida (spawn point, radii) fica aqui.
    /// Referências de infraestrutura (NavMesh, pool) ficam no NPC como campos fixos.
    /// </summary>
    public sealed class NpcEnemyConfig
    {
        // ── Posicionamento ────────────────────────────────────────────────

        /// <summary>
        /// Ponto de spawn atual. Também serve como centro do chase radius.
        /// Atualizado a cada saída da pool.
        /// </summary>
        public readonly Vector3 SpawnPoint;

        // ── Raios de comportamento ────────────────────────────────────────

        /// <summary>Raio de caminhada aleatória ao redor do spawn point.</summary>
        public readonly float WanderRadius;

        /// <summary>
        /// Distância máxima do spawn point que o NPC pode percorrer
        /// durante a perseguição antes de ser devolvido à pool.
        /// </summary>
        public readonly float ChaseRadius;

        /// <summary>
        /// Distância mínima para considerar que o NPC "tocou" o Player.
        /// Dispara o evento de contato.
        /// </summary>
        public readonly float ContactDistance;

        // ── Detecção ──────────────────────────────────────────────────────

        /// <summary>
        /// Detector já configurado no prefab do NPC.
        /// O Builder garante que a tag-alvo é "Player".
        /// </summary>
        public readonly Detector Detector;

        // ── Alvo de perseguição ───────────────────────────────────────────

        /// <summary>
        /// Transform do Player. Pode ser null se ainda não detectado.
        /// Preenchido pelo handler de detecção quando o Player é detectado.
        /// </summary>
        public Transform PursuitTarget;

        // ── Pool callback ─────────────────────────────────────────────────

        /// <summary>
        /// Callback invocado quando o NPC solicita retorno à pool.
        /// Desacopla o NPC da referência direta à pool.
        /// </summary>
        public readonly System.Action<INpcEnemy> OnReturnToPool;

        // ── Construção ────────────────────────────────────────────────────

        public NpcEnemyConfig(
            Vector3 spawnPoint,
            float wanderRadius,
            float chaseRadius,
            float contactDistance,
            Detector detector,
            System.Action<INpcEnemy> onReturnToPool)
        {
            SpawnPoint       = spawnPoint;
            WanderRadius     = wanderRadius;
            ChaseRadius      = chaseRadius;
            ContactDistance  = contactDistance;
            Detector         = detector;
            OnReturnToPool   = onReturnToPool;
            PursuitTarget    = null;
        }
    }
}
