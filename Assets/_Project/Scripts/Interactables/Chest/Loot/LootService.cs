using System.Collections.Generic;
using Core.Exploration.Interactables.Chest;
using Services.DebugUtilities;

namespace Services.Loot
{
    /// <summary>
    /// Implementação concreta de <see cref="ILootService"/>.
    ///
    /// Responsabilidades:
    ///   - Validar os parâmetros recebidos antes de delegar.
    ///   - Delegar o sorteio à <see cref="LootTable"/> (que encapsula roleta e ranges).
    ///   - Registrar logs de auditoria sem que o baú precise conhecê-los.
    ///
    /// Não contém lógica de sorteio — essa responsabilidade permanece na LootTable.
    ///
    /// Uso (instância manual, sem container de DI):
    /// <code>
    ///     private readonly ILootService _lootService = new LootService();
    /// </code>
    /// </summary>
    public sealed class LootService : ILootService
    {
        /// <inheritdoc/>
        public IReadOnlyDictionary<IStorageable, int> GenerateLoot(
            LootTable lootTable,
            LootRequestContext context)
        {
            // ── Validação de contexto ──────────────────────────────────────
            if (context == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    "[LootService] LootRequestContext não pode ser null. Retornando loot vazio.");
                return EmptyResult();
            }

            // ── LootTable ausente ──────────────────────────────────────────
            if (lootTable == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[LootService] '{context.ChestName}' não possui LootTable atribuída. " +
                    "Retornando loot vazio.", LogCategory.Interaction);
                return EmptyResult();
            }

            // ── Delegação do sorteio à LootTable ──────────────────────────
            var result = lootTable.Generate();

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[LootService] '{context.ChestName}' gerou {result.Count} tipo(s) de item " +
                $"via '{lootTable.name}'.", LogCategory.Interaction);

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────

        private static IReadOnlyDictionary<IStorageable, int> EmptyResult() =>
            new Dictionary<IStorageable, int>();
    }
}
