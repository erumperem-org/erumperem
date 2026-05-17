using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Exploration.Interactables.Chest
{
    /// <summary>
    /// Tabela de loot reutilizável entre múltiplos baús.
    /// Define os itens sorteáveis, seus pesos relativos, ranges de quantidade
    /// e a capacidade máxima de itens que o baú pode conter.
    ///
    /// Crie via: Assets > Create > Exploration > Loot Table
    /// </summary>
    [CreateAssetMenu(menuName = "Exploration/Loot Table", fileName = "LootTable")]
    public sealed class LootTable : ScriptableObject
    {
        [Tooltip("Lista de entradas sorteáveis. Pesos são normalizados automaticamente.")]
        [SerializeField] private List<LootEntry> entries = new();

        [Tooltip("Quantidade máxima de itens gerados por abertura do baú.")]
        [Min(1)]
        [SerializeField] private int maxChestCapacity = 5;

        // ─────────────────────────────────────────────────────────────
        // API pública
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gera a lista de itens do baú com base nos pesos e ranges configurados.
        /// O total de itens nunca excede <see cref="maxChestCapacity"/>.
        /// </summary>
        /// <returns>
        /// Dicionário de IStorageable → quantidade sorteada.
        /// Itens com quantidade zero são excluídos.
        /// </returns>
        public Dictionary<IStorageable, int> Generate()
        {
            var result      = new Dictionary<IStorageable, int>();
            var validEntries = GetValidEntries();

            if (validEntries.Count == 0)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[LootTable:{name}] Nenhuma entrada válida encontrada. Verifique os assets e pesos.");
                return result;
            }

            float totalWeight = ComputeTotalWeight(validEntries);
            int   remaining   = maxChestCapacity;

            while (remaining > 0 && validEntries.Count > 0)
            {
                LootEntry entry = PickEntry(validEntries, totalWeight);

                int quantity = Random.Range(entry.minQuantity, entry.maxQuantity + 1);
                quantity = Mathf.Min(quantity, remaining);

                if (result.TryGetValue(entry.Storageable, out int current))
                    result[entry.Storageable] = current + quantity;
                else
                    result[entry.Storageable] = quantity;

                remaining -= quantity;

                // Remove a entrada já sorteada para evitar duplicatas no mesmo sorteio.
                validEntries.Remove(entry);
                totalWeight = ComputeTotalWeight(validEntries);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        // Sorteio por peso relativo (roleta viciada)
        // ─────────────────────────────────────────────────────────────

        private LootEntry PickEntry(List<LootEntry> pool, float totalWeight)
        {
            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var entry in pool)
            {
                cumulative += entry.weight;
                if (roll <= cumulative)
                    return entry;
            }

            // Fallback seguro: retorna o último (cobre imprecisão de float)
            return pool[^1];
        }

        private static float ComputeTotalWeight(List<LootEntry> pool)
        {
            float total = 0f;
            foreach (var e in pool)
                total += e.weight;
            return total;
        }

        private List<LootEntry> GetValidEntries()
        {
            var valid = new List<LootEntry>();
            foreach (var entry in entries)
            {
                if (entry.IsValid)
                    valid.Add(entry);
                else
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[LootTable:{name}] Entrada inválida ignorada: asset='{entry.item.name ?? "null"}', " +
                        $"peso={entry.weight}, min={entry.minQuantity}, max={entry.maxQuantity}");
            }
            return valid;
        }

#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────
        // Validação no editor
        // ─────────────────────────────────────────────────────────────

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];

                // Garante que maxQuantity nunca seja menor que minQuantity no inspector.
                if (e.maxQuantity < e.minQuantity)
                {
                    e.maxQuantity = e.minQuantity;
                    entries[i]    = e;
                }

                // Avisa se o asset não implementa IStorageable.
                if (e.item != null && e.Storageable == null)
                {
                    Debug.LogWarning(
                        $"[LootTable:{name}] '{e.item.name}' não implementa IStorageable e será ignorado.", this);
                }
            }
        }
#endif
    }
}
