// ============================================================
// NpcEnemyPool.cs
// Namespace : Systems.NPC.Pool
// ============================================================
// Pool de NPCs inimigos.
//
// Regras:
//   • Máximo de 10 NPCs simultâneos (configurável).
//   • Sem Instantiate/Destroy durante o jogo — apenas no Awake.
//   • NPCs inativos são posicionados em grade fora do mapa,
//     configurável pelo Inspector.
//   • A grade organiza NPCs em pares de coluna (2 por linha).
//
// Grade de armazenamento (exemplo com spacing = 3):
//   [0] [1]
//   [2] [3]
//   [4] [5]
//   [6] [7]
//   [8] [9]
// ============================================================

using System.Collections.Generic;
using Systems.NPC.Enemy;
using Systems.NPC.Enemy.Contracts;
using UnityEngine;

namespace Systems.NPC.Pool
{
    /// <summary>
    /// Pool de NPCs inimigos. Gerencia o ciclo de vida de ativação
    /// e desativação sem Instantiate/Destroy.
    ///
    /// Utiliza SetActive(false/true) e reposicionamento em grade
    /// fora do mapa para organizar os NPCs inativos.
    /// </summary>
    public sealed class NpcEnemyPool : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Prefab")]
        [Tooltip("Prefab do NPC inimigo. Deve ter NpcEnemy, NpcMovementController, NavMeshAgentAdapter e Detector.")]
        [SerializeField] private GameObject _npcPrefab;

        [Header("Capacidade")]
        [Tooltip("Número máximo de NPCs simultâneos.")]
        [SerializeField, Min(1)] private int _poolSize = 10;

        [Header("Posição de Armazenamento (fora do mapa)")]
        [Tooltip("Posição de origem da grade de armazenamento dos NPCs inativos.")]
        [SerializeField] private Vector3 _storageOrigin = new Vector3(0f, -100f, 0f);

        [Tooltip("Espaçamento entre NPCs na grade de armazenamento.")]
        [SerializeField] private float _storageSpacing = 3f;

        // ── Estado interno ────────────────────────────────────────────────

        private readonly Stack<NpcEnemy>   _available  = new Stack<NpcEnemy>();
        private readonly HashSet<NpcEnemy> _active     = new HashSet<NpcEnemy>();

        // ── Propriedades de consulta ──────────────────────────────────────

        /// <summary>Número de NPCs disponíveis para uso.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Número de NPCs atualmente ativos no mundo.</summary>
        public int ActiveCount => _active.Count;

        /// <summary>True se houver ao menos um NPC disponível na pool.</summary>
        public bool HasAvailable => _available.Count > 0;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_npcPrefab == null)
            {
                Debug.LogError("[NpcEnemyPool] Nenhum prefab configurado!", this);
                return;
            }

            PrewarmPool();
        }

        // ═════════════════════════════════════════════════════════════════
        // API pública — usada pelo Builder
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Retira um NPC da pool e retorna sua referência.
        /// Retorna null se a pool estiver esgotada.
        /// </summary>
        public NpcEnemy Get()
        {
            if (!HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyPool] Pool esgotada. Nenhum NPC disponível.", this);
                return null;
            }

            var npc = _available.Pop();
            _active.Add(npc);

            // Ativa o GameObject — Initialize() e Activate() são chamados pelo Builder.
            // O NavMeshAgent é reabilitado dentro de Activate() via EnableAgent(),
            // após o Warp para o spawn point, garantindo a ordem correta de inicialização.
            npc.gameObject.SetActive(true);

            return npc;
        }

        /// <summary>
        /// Devolve um NPC à pool. Chamado pelo NpcEnemy via config.OnReturnToPool.
        /// Desativa o GameObject e o reposiciona na grade de armazenamento.
        /// </summary>
        public void Return(INpcEnemy npc)
        {
            if (npc is not NpcEnemy enemy)
            {
                Debug.LogWarning("[NpcEnemyPool] Tentativa de retornar tipo não suportado.");
                return;
            }

            if (!_active.Contains(enemy))
            {
                Debug.LogWarning($"[NpcEnemyPool] NPC '{enemy.name}' não está na lista de ativos.", this);
                return;
            }

            _active.Remove(enemy);
            _available.Push(enemy);

            // Desabilita o NavMeshAgent ANTES de reposicionar fora do NavMesh —
            // mesma razão do PrewarmPool: evita que o Unity tente corrigir a posição.
            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // Reposiciona na grade antes de desativar
            RepositionInStorage(enemy, _available.Count - 1);

            // Desativa o GameObject — nenhum código roda enquanto inativo
            enemy.gameObject.SetActive(false);
        }

        // ═════════════════════════════════════════════════════════════════
        // Inicialização da pool
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Instancia todos os NPCs no Awake na posição do próprio Pool GameObject
        /// (que deve estar sobre o NavMesh), desativa o NavMeshAgent, move para a
        /// grade de armazenamento e desativa o GameObject.
        ///
        /// Por que não instanciar direto na storageOrigin:
        ///   O NavMeshAgent exige estar próximo ao NavMesh na primeira ativação.
        ///   Instanciar em y = -100 (fora do mapa) gera o erro
        ///   "Failed to create agent because it is not close enough to the NavMesh".
        ///   A solução é: instanciar no NavMesh → desativar agent → mover → desativar GO.
        /// </summary>
        private void PrewarmPool()
        {
            // Posição de nascimento: sobre o NavMesh (onde o Pool GameObject está).
            // O Pool GameObject DEVE ser posicionado sobre o NavMesh no Editor.
            Vector3 birthPosition = transform.position;

            for (int i = 0; i < _poolSize; i++)
            {
                // 1. Instancia no NavMesh — agent inicializa sem erro
                var go = Instantiate(_npcPrefab, birthPosition, Quaternion.identity, transform);
                go.name = $"NpcEnemy_{i:D2}";

                var enemy = go.GetComponent<NpcEnemy>();

                if (enemy == null)
                {
                    Debug.LogError($"[NpcEnemyPool] Prefab não possui NpcEnemy. NPC '{go.name}' ignorado.", this);
                    Destroy(go);
                    continue;
                }

                // 2. Desativa o NavMeshAgent ANTES de mover para fora do NavMesh.
                //    Com o agent desativado, o Unity não tenta corrigir a posição.
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                // 3. Reposiciona na grade de armazenamento (pode ser fora do NavMesh)
                go.transform.position = CalculateStoragePosition(i);
                go.transform.rotation = Quaternion.identity;

                // 4. Desativa o GameObject — zero processamento enquanto na pool
                go.SetActive(false);

                _available.Push(enemy);
            }

            Debug.Log($"[NpcEnemyPool] Pool inicializada com {_available.Count} NPCs.", this);
        }

        // ═════════════════════════════════════════════════════════════════
        // Grade de armazenamento
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Calcula a posição na grade de armazenamento para o índice dado.
        ///
        /// Grade: 2 NPCs por linha.
        ///   index 0 → coluna 0, linha 0
        ///   index 1 → coluna 1, linha 0
        ///   index 2 → coluna 0, linha 1
        ///   index 3 → coluna 1, linha 1
        ///   ...
        /// </summary>
        private Vector3 CalculateStoragePosition(int index)
        {
            int col = index % 2;         // 0 ou 1
            int row = index / 2;         // incrementa a cada 2 NPCs

            return _storageOrigin + new Vector3(
                col * _storageSpacing,
                0f,
                row * _storageSpacing
            );
        }

        /// <summary>
        /// Reposiciona o NPC na grade de armazenamento.
        /// O índice dentro da Stack determina a célula.
        /// </summary>
        private void RepositionInStorage(NpcEnemy enemy, int indexInPool)
        {
            // Usa o índice na pool para determinar a célula na grade
            // (após Push, o NPC é o topo da stack = índice Count-1)
            int storageIndex = indexInPool;
            enemy.transform.position = CalculateStoragePosition(storageIndex);
            enemy.transform.rotation = Quaternion.identity;
        }

        // ═════════════════════════════════════════════════════════════════
        // Gizmo de debug — Grade de armazenamento
        // ═════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            for (int i = 0; i < _poolSize; i++)
            {
                Vector3 pos = CalculateStoragePosition(i);
                Gizmos.DrawWireCube(pos, Vector3.one * 0.8f);
            }

            // Origem da grade
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_storageOrigin, 0.3f);
        }
#endif
    }
}