// ============================================================
// ChestBuilder.cs
// Namespace : Systems.Chest.Builder
// ============================================================
// Responsabilidade única: montar cada baú da pool —
// escolher LootTable, posicionar e registrar callback de retorno.
//
// Espelha o padrão de NpcEnemyBuilder:
//   • Conhece o tipo concreto (ChestInteractable) — não a pool.
//   • Injeta a LootTable via InjectLootTable() antes de ativar.
//   • O callback onReturnToPool devolve o baú à ChestPool.
// ============================================================

using Core.Exploration.Interactables.Chest;
using Services.DebugUtilities;
using Systems.Chest.Pool;
using UnityEngine;

namespace Systems.Chest.Builder
{
    public sealed class ChestBuilder : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Dependências")]
        [SerializeField] private ChestPool _pool;

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Aloca um baú da pool, sorteia uma LootTable da lista configurada
        /// e o posiciona no <paramref name="spawnPoint"/> informado.
        /// </summary>
        /// <returns>O ChestInteractable ativado, ou null se a pool estiver vazia.</returns>
        public ChestInteractable BuildAt(Vector3 spawnPoint, Quaternion rotation = default)
        {
            if (!ValidateDependencies()) return null;

            if (!_pool.HasAvailable)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[ChestBuilder] Pool esgotada.", LogCategory.Interaction);
                return null;
            }

            var chest = _pool.Get();
            if (chest == null) return null;

            // ── Sorteia uma LootTable da lista ────────────────────────────
            var lootTable = PickRandomLootTable();
            chest.InjectLootTable(lootTable);

            // ── Posiciona na cena ─────────────────────────────────────────
            chest.transform.position = spawnPoint;
            chest.transform.rotation = rotation == default ? Quaternion.identity : rotation;

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestBuilder] '{chest.name}' posicionado em {spawnPoint} " +
                $"com LootTable '{(lootTable != null ? lootTable.name : "nenhuma")}'.",
                LogCategory.Interaction);

            return chest;
        }

        /// <summary>
        /// Devolve o baú à pool (reset + nova LootTable na próxima alocação).
        /// Chamado pelo ChestAreaSpawner ao recarregar a área.
        /// </summary>
        public void ReturnToPool(ChestInteractable chest)
        {
            if (chest == null) return;
            _pool.Return(chest);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private LootTable PickRandomLootTable()
        {
            var tables = _pool.AvailableLootTables;

            if (tables == null || tables.Count == 0)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[ChestBuilder] Nenhuma LootTable configurada na pool. Baú ficará sem loot.",
                    LogCategory.Interaction);
                return null;
            }

            return tables[Random.Range(0, tables.Count)];
        }

        private bool ValidateDependencies()
        {
            if (_pool != null) return true;
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[ChestBuilder] ChestPool não configurada!", LogCategory.Interaction);
            return false;
        }
    }
}
