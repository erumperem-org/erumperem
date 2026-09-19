using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Rewards;

namespace Core.Chests
{
    public sealed class ChestLootReevaluationSystem : MonoBehaviour
    {
        [Tooltip("Must implement ICorruptionTierSource. Optional — see CurrentTier / SetManualTestTier.")]
        [SerializeField] private MonoBehaviour _corruptionTierSource;

        [Tooltip("Index = tier (0 to 4). Each tier can offer multiple possible tables — one is picked at random per chest. " +
                 "IMPORTANT: expand each element in the Inspector and add at least one LootTable, or loot assignment will fail for that tier.")]
        [SerializeField] private ChestTierTablePool[] _chestTablesByTier = new ChestTierTablePool[5];

        [SerializeField] private ChestAllocationSystem _allocationSystem;

        private readonly List<Chest> _knownChests = new();
        private readonly RewardGeneratorService _rewardGenerator = new();
        private int _manualTestTier;

        public IReadOnlyList<Chest> KnownChests => _knownChests;

        private ICorruptionTierSource TierSource => _corruptionTierSource as ICorruptionTierSource;

        public int CurrentTier => TierSource?.CurrentTier ?? _manualTestTier;

        private void Awake()
        {
            // Defensive fix: guarantees every slot is a real instance, even if
            // the array was never expanded/touched in the Inspector (which would
            // otherwise leave reference-type elements as genuine null, causing
            // AssignLootToChest to always short-circuit before ever calling
            // Chest.AssignLoot, for every tier and every chest).
            EnsurePoolArrayIsPopulated();
        }

        private void EnsurePoolArrayIsPopulated()
        {
            if (_chestTablesByTier == null || _chestTablesByTier.Length == 0)
            {
                Log(LogLevel.Error, "_chestTablesByTier is null or empty — no tier can ever produce loot. " +
                                     "Assign at least 5 elements (tiers 0-4) in the Inspector.");
                _chestTablesByTier = new ChestTierTablePool[5];
            }

            for (int i = 0; i < _chestTablesByTier.Length; i++)
            {
                if (_chestTablesByTier[i] == null)
                {
                    Log(LogLevel.Warning, $"Tier {i}'s table pool was null (never configured in the Inspector) — " +
                                           "replaced with an empty pool. Loot assignment will still fail for this tier " +
                                           "until you add at least one LootTable to it.");
                    _chestTablesByTier[i] = new ChestTierTablePool();
                }
            }
        }

        private void Start()
        {
            DiscoverChestsInScene();

            if (_allocationSystem != null)
            {
                _allocationSystem.OnChestCreated += HandleChestCreated;
                _allocationSystem.OnChestsRepositioned += HandleChestsRepositioned;
            }

            if (TierSource != null)
                TierSource.OnTierChanged += HandleTierChanged;
            else
                Log(LogLevel.Warning, "No ICorruptionTierSource assigned — using manual test tier as fallback.");

            ForceReevaluateAll(); // initial evaluation using the current (real or manual) tier
        }

        private void OnDestroy()
        {
            if (_allocationSystem != null)
            {
                _allocationSystem.OnChestCreated -= HandleChestCreated;
                _allocationSystem.OnChestsRepositioned -= HandleChestsRepositioned;
            }

            if (TierSource != null)
                TierSource.OnTierChanged -= HandleTierChanged;
        }

        public void DiscoverChestsInScene()
        {
            _knownChests.Clear();
            _knownChests.AddRange(FindObjectsByType<Chest>(FindObjectsSortMode.None));
            Log(LogLevel.Debug, $"{_knownChests.Count} chest(s) found in the scene (inactive chests are not included).");
        }

        private void HandleChestCreated(Chest chest)
        {
            if (chest == null) return;

            _knownChests.Add(chest);
            AssignLootToChest(chest, CurrentTier);
        }

        private void HandleChestsRepositioned(IReadOnlyList<Chest> chests)
        {
            int tier = CurrentTier;
            foreach (var chest in chests)
                AssignLootToChest(chest, tier);

            Log(LogLevel.Debug, $"Loot reassigned to {chests.Count} repositioned chest(s) at tier {tier}.");
        }

        private void HandleTierChanged(int _) => ForceReevaluateAll();

        public void ForceReevaluateAll()
        {
            _knownChests.RemoveAll(chest => chest == null); // prune destroyed references

            int tier = CurrentTier;
            Log(LogLevel.Debug, $"Reevaluating {_knownChests.Count} known chest(s) at resolved tier {tier}...");

            int successCount = 0;
            foreach (var chest in _knownChests)
                if (AssignLootToChest(chest, tier))
                    successCount++;

            Log(LogLevel.Debug, $"Loot reevaluation finished: {successCount}/{_knownChests.Count} chest(s) received loot at tier {tier}.");
        }

        public void SetManualTestTier(int tier)
        {
            _manualTestTier = Mathf.Max(0, tier);
            Log(LogLevel.Debug, $"Manual test tier set to {_manualTestTier} (used only while no ICorruptionTierSource is assigned).");
        }

        /// <summary>
        /// Returns how many LootTables are configured for each tier's pool —
        /// exposed for editor diagnostics, so misconfiguration (empty pools)
        /// is visible without digging through logs.
        /// </summary>
        public IReadOnlyList<int> GetTablePoolSizes()
        {
            var sizes = new List<int>(_chestTablesByTier.Length);
            foreach (var pool in _chestTablesByTier)
                sizes.Add(pool?.Tables.Count ?? 0);
            return sizes;
        }

        /// <summary>
        /// Attempts to assign loot to a single chest. Returns false (and logs
        /// exactly why) without ever calling Chest.AssignLoot if the tier's
        /// pool is missing or empty — this is the single choke point for
        /// every "chest never receives loot" symptom in this system.
        /// </summary>
        private bool AssignLootToChest(Chest chest, int tier)
        {
            if (chest == null) return false;

            if (tier < 0 || tier >= _chestTablesByTier.Length)
            {
                Log(LogLevel.Error, $"Resolved tier {tier} is out of range (pool array has {_chestTablesByTier.Length} slot(s)).");
                return false;
            }

            var pool = _chestTablesByTier[tier];
            if (pool == null || pool.Tables.Count == 0)
            {
                Log(LogLevel.Error, $"Tier {tier}'s table pool has no LootTable configured — " +
                                     $"chest '{chest.name}' will NOT receive loot. Add at least one LootTable in the Inspector for this tier.");
                return false;
            }

            var table = pool.PickRandom();
            var loot = _rewardGenerator.Generate(table);

            if (loot.Count == 0)
                Log(LogLevel.Warning, $"Generated an empty loot result for chest '{chest.name}' at tier {tier} " +
                                       $"(table '{table.name}' rolled with all entries failing their chance).");

            chest.AssignLoot(loot);
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ChestLootReevaluationSystem] {msg}", LogCategory.Inventory);
    }
}