// ============================================================
// NpcEnemyBuilder.cs
// Namespace : Systems.NPC.Builder
// ============================================================
// Builder / Factory do NPC inimigo.
//
// Responsabilidades:
//   1. Solicitar NPC à pool.
//   2. Resolver spawn point via ISpawnPositionService.
//   3. Montar NpcEnemyConfig com todos os parâmetros.
//   4. Chamar Initialize() e Activate() no NPC.
//
// O Builder é o único ponto onde pool + config + NPC se encontram.
// O NPC não conhece a pool. A pool não conhece o config.
// O Spawner não conhece os detalhes de configuração.
// ============================================================

using DetectionSystem.Core;
using Services.Spawning;
using Systems.NPC.Enemy;
using Systems.NPC.Enemy.Contracts;
using Systems.NPC.Pool;
using UnityEngine;

namespace Systems.NPC.Builder
{
    /// <summary>
    /// Constrói e configura NPCs inimigos a partir da pool.
    ///
    /// Expõe um método Build() que o Spawner chama sempre que
    /// precisar de um novo NPC. Internamente faz todas as ligações
    /// entre pool, config e comportamento.
    /// </summary>
    public sealed class NpcEnemyBuilder : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Dependências")]
        [Tooltip("Pool de NPCs inimigos.")]
        [SerializeField] private NpcEnemyPool _pool;

        [Tooltip("Serviço de posição de spawn (NavMeshPositionMono ou implementação própria).")]
        [SerializeField] private NavMeshSpawnPositionServiceMono _spawnService;

        [Header("Configuração de Comportamento")]
        [Tooltip("Raio de caminhada aleatória a partir do ponto de spawn.")]
        [SerializeField, Min(1f)] private float _wanderRadius = 8f;

        [Tooltip("Distância máxima do spawn que o NPC pode percorrer durante chase. Ao ultrapassar, retorna para pool.")]
        [SerializeField, Min(1f)] private float _chaseRadius = 20f;

        [Tooltip("Distância mínima para considerar contato com o Player.")]
        [SerializeField, Min(0.1f)] private float _contactDistance = 1.2f;

        // ═════════════════════════════════════════════════════════════════
        // API pública — usada pelo Spawner
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Constrói e ativa um NPC inimigo.
        ///
        /// Fluxo:
        ///   1. Solicita NPC à pool.
        ///   2. Obtém spawn point válido no NavMesh.
        ///   3. Monta a config com todos os parâmetros.
        ///   4. Inicializa e ativa o NPC.
        ///
        /// Retorna true se o NPC foi criado com sucesso, false se a pool
        /// estiver esgotada ou não houver posição de spawn disponível.
        /// </summary>
        /// <param name="spawnCenter">
        /// Centro da área de spawn. Se Vector3.zero, usa o centro padrão do serviço.
        /// </param>
        public bool Build(Vector3 spawnCenter = default)
        {
            // ── 1. Valida dependências ─────────────────────────────────────
            if (_pool == null || _spawnService == null)
            {
                Debug.LogError("[NpcEnemyBuilder] Pool ou SpawnService não configurados!", this);
                return false;
            }

            // ── 2. Verifica disponibilidade da pool ────────────────────────
            if (!_pool.HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Pool esgotada. Nenhum NPC disponível.");
                return false;
            }

            // ── 3. Resolve spawn point no NavMesh ──────────────────────────
            Vector3 spawnPoint;
            bool found;

            if (spawnCenter == Vector3.zero)
            {
                found = _spawnService.TryGetPosition(out spawnPoint);
            }
            else
            {
                found = _spawnService.TryGetPosition(spawnCenter, _wanderRadius * 2f, out spawnPoint);
            }

            if (!found)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Nenhuma posição de spawn válida encontrada no NavMesh.");
                return false;
            }

            // ── 4. Retira NPC da pool ──────────────────────────────────────
            NpcEnemy npc = _pool.Get();
            if (npc == null) return false;

            // ── 5. Obtém o Detector do NPC ─────────────────────────────────
            var detector = npc.GetComponent<Detector>();
            if (detector == null)
            {
                Debug.LogError($"[NpcEnemyBuilder] NPC '{npc.name}' não possui Detector!", this);
                _pool.Return(npc);
                return false;
            }

            // ── 6. Monta a configuração ────────────────────────────────────
            //    O callback OnReturnToPool fecha o ciclo: NpcEnemy → Pool
            //    sem que o NPC precise de referência direta à pool.
            var config = new NpcEnemyConfig(
                spawnPoint      : spawnPoint,
                wanderRadius    : _wanderRadius,
                chaseRadius     : _chaseRadius,
                contactDistance : _contactDistance,
                detector        : detector,
                onReturnToPool  : (enemy) => _pool.Return(enemy)
            );

            // ── 7. Inicializa e ativa o NPC ────────────────────────────────
            //    O centro do chase radius SEMPRE é o novo spawnPoint.
            npc.Initialize(config);
            npc.Activate();

            Debug.Log($"[NpcEnemyBuilder] NPC '{npc.name}' ativado em {spawnPoint}. " +
                      $"[Wander: {_wanderRadius}m | Chase: {_chaseRadius}m]", npc);

            return true;
        }

        /// <summary>
        /// Versão com spawn point fixo explícito (útil para spawners com transform específico).
        /// </summary>
        public bool BuildAt(Vector3 exactSpawnPoint)
        {
            if (_pool == null)
            {
                Debug.LogError("[NpcEnemyBuilder] Pool não configurada!", this);
                return false;
            }

            if (!_pool.HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyBuilder] Pool esgotada.");
                return false;
            }

            NpcEnemy npc = _pool.Get();
            if (npc == null) return false;

            var detector = npc.GetComponent<Detector>();
            if (detector == null)
            {
                _pool.Return(npc);
                return false;
            }

            var config = new NpcEnemyConfig(
                spawnPoint      : exactSpawnPoint,
                wanderRadius    : _wanderRadius,
                chaseRadius     : _chaseRadius,
                contactDistance : _contactDistance,
                detector        : detector,
                onReturnToPool  : (enemy) => _pool.Return(enemy)
            );

            npc.Initialize(config);
            npc.Activate();

            return true;
        }
    }
}
