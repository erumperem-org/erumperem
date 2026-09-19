using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Storage;

namespace Core.Rewards
{
    /// <summary>
    /// Consumer of RewardGeneratorService based on the corruption value
    /// (0-200), resolved into a tier (0-4) via the project's existing
    /// CorruptionTierCalculator. Each tier has its own independent
    /// LootTable. NOTE: CorruptionTierCalculator/CorruptionRules are
    /// assumed to already exist in the project and are not defined here.
    /// </summary>
    public sealed class CorruptionRewardDispenser : MonoBehaviour
    {
        [Tooltip("Index = tier (0 to 4). Must have exactly 5 entries.")]
        [SerializeField] private LootTable[] _tablesByTier = new LootTable[5];

        private readonly RewardGeneratorService _rewardGenerator = new();

        /// <summary>
        /// Rolls rewards based on the given corruption value. Returns a
        /// generic result — the caller decides how to route it (coins →
        /// WalletSystem, items → InventorySystem).
        /// </summary>
        public IReadOnlyDictionary<InterfaceStorageable, int> GenerateFromCorruption(double corruptionValue)
        {
            int tier = CorruptionTierCalculator.GetTier(corruptionValue);

            if (tier < 0 || tier >= _tablesByTier.Length || _tablesByTier[tier] == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    $"[CorruptionRewardDispenser] Missing table for tier {tier}.",
                    LogCategory.Inventory);
                return new Dictionary<InterfaceStorageable, int>();
            }

            return _rewardGenerator.Generate(_tablesByTier[tier]);
        }
    }
}
