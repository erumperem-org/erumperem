// ============================================================
// ChestPool.cs
// Namespace : Systems.Chest.Pool
// ============================================================
// Responsabilidade única: gerenciar disponibilidade dos baús
// (Get / Return / PreWarm).
//
// Espelha o padrão de NpcEnemyPool:
//   • Itens inativos ficam escondidos em _storageOrigin (abaixo do mapa).
//   • Return() reseta o baú e sorteia uma nova LootTable da lista configurada.
//   • Pool e Builder ficam separados — o Builder monta e posiciona,
//     a Pool só gerencia disponibilidade.
// ============================================================

using System;
using System.Collections.Generic;
using Core.Exploration.Interactables.Chest;
using Services.DebugUtilities;
using UnityEngine;

namespace Systems.Chest.Pool
{
    public sealed class ChestPool : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Prefab")]
        [SerializeField] private GameObject _chestPrefab;

        [Header("Capacidade")]
        [SerializeField, Min(1)] private int _poolSize = 10;

        [Header("Armazenamento (fora do mapa)")]
        [SerializeField] private Vector3 _storageOrigin  = new Vector3(0f, -200f, 0f);
        [SerializeField] private float   _storageSpacing = 3f;

        [Header("Loot Tables disponíveis")]
        [Tooltip("O Builder escolherá aleatoriamente uma destas tabelas ao alocar cada baú.")]
        [SerializeField] private List<LootTable> _availableLootTables = new();

        // ── Estado interno ────────────────────────────────────────────────

        private readonly Stack<ChestInteractable>   _available = new();
        private readonly HashSet<ChestInteractable> _active    = new();

        // ── Propriedades ──────────────────────────────────────────────────

        public int  AvailableCount => _available.Count;
        public int  ActiveCount    => _active.Count;
        public bool HasAvailable   => _available.Count > 0;

        /// <summary>
        /// Lista somente-leitura das LootTables configuradas no Inspector.
        /// O ChestBuilder usa para sortear qual tabela atribuir ao baú.
        /// </summary>
        public IReadOnlyList<LootTable> AvailableLootTables => _availableLootTables;

        // ── Evento ────────────────────────────────────────────────────────

        /// <summary>Disparado quando um baú é devolvido à pool (já resetado).</summary>
        public event Action OnChestReturned;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_chestPrefab == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    "[ChestPool] Nenhum prefab configurado!", LogCategory.Interaction);
                return;
            }

            PrewarmPool();
        }

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Retira um baú da pool. Retorna null se não houver disponível.
        /// O baú está resetado (fechado) mas sem LootTable atribuída —
        /// o Builder é responsável por injetar a LootTable antes de posicioná-lo.
        /// </summary>
        public ChestInteractable Get()
        {
            if (!HasAvailable)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[ChestPool] Pool esgotada.", LogCategory.Interaction);
                return null;
            }

            var chest = _available.Pop();
            _active.Add(chest);
            chest.gameObject.SetActive(true);
            return chest;
        }

        /// <summary>
        /// Devolve um baú à pool: reseta o estado, troca a LootTable e
        /// move para a área de armazenamento.
        /// Chamado pelo ChestBuilder via callback após a área ser recarregada.
        /// </summary>
        public void Return(ChestInteractable chest)
        {
            if (chest == null) return;

            if (!_active.Contains(chest))
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[ChestPool] '{chest.name}' não está na lista de ativos.", LogCategory.Interaction);
                return;
            }

            _active.Remove(chest);

            // ── Reset do baú (fecha + limpa loot anterior) ────────────────
            chest.ResetChest();

            // ── Move para o storage off-map ───────────────────────────────
            StoreAt(chest, _available.Count);

            _available.Push(chest);
            chest.gameObject.SetActive(false);

            OnChestReturned?.Invoke();

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestPool] '{chest.name}' devolvido à pool ({_available.Count}/{_poolSize}).",
                LogCategory.Interaction);
        }

        // ── PreWarm ───────────────────────────────────────────────────────

        private void PrewarmPool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var position = StoragePositionFor(i);
                var go = Instantiate(_chestPrefab, position, Quaternion.identity, transform);
                go.name = $"PooledChest_{i:D2}";

                var chest = go.GetComponent<ChestInteractable>();
                if (chest == null)
                {
                    LoggerService.PrintLogMessage(LogLevel.Error,
                        $"[ChestPool] '{go.name}' não possui ChestInteractable.", LogCategory.Interaction);
                    Destroy(go);
                    continue;
                }

                go.SetActive(false);
                _available.Push(chest);
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestPool] Pool inicializada com {_available.Count} baús.", LogCategory.Interaction);
        }

        // ── Storage helpers ───────────────────────────────────────────────

        private Vector3 StoragePositionFor(int index)
        {
            int col = index % 2;
            int row = index / 2;
            return _storageOrigin + new Vector3(col * _storageSpacing, 0f, row * _storageSpacing);
        }

        private void StoreAt(ChestInteractable chest, int index)
        {
            chest.transform.position = StoragePositionFor(index);
            chest.transform.rotation = Quaternion.identity;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.7f, 0.1f, 0.4f);
            for (int i = 0; i < _poolSize; i++)
                Gizmos.DrawWireCube(StoragePositionFor(i), Vector3.one * 0.8f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_storageOrigin, 0.4f);
        }
#endif
    }
}
