using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Services.DebugUtilities;
using UnityEngine;
using SceneAllocation;

namespace Core.Chests
{
    /// <summary>
    /// Creates a fixed chest pool once (via SceneObjectAllocationSystem),
    /// sized for the largest possible value across all tier ranges in
    /// <see cref="_chestCountRangeByTier"/>, and afterwards only REPOSITIONS
    /// and activates/deactivates existing instances on every reallocation —
    /// never destroys/recreates them. Active count varies by tier, rolled
    /// within a configurable min-max range each time. Raises OnChestCreated
    /// only during the initial pool creation.
    ///
    /// Depends on PlaceableObjectData / AllocationResult /
    /// SceneObjectAllocationSystem, assumed to already exist in the project
    /// (see the Scene Object Allocation System README) — not redefined here.
    /// </summary>
    public sealed class ChestAllocationSystem : MonoBehaviour
    {
        [Serializable]
        public struct ChestCountRange
        {
            [SerializeField] private int _min;
            [SerializeField] private int _max;

            public int Min => _min;
            public int Max => _max;

            /// <summary>Rolls a value within [Min, Max], inclusive. Clamped defensively in case Min > Max.</summary>
            public int Resolve()
            {
                int min = Mathf.Min(_min, _max);
                int max = Mathf.Max(_min, _max);
                return UnityEngine.Random.Range(min, max + 1);
            }
        }

        [Tooltip("Must implement ICorruptionTierSource. Optional — see CurrentTier / SetManualTestTier.")]
        [SerializeField] private MonoBehaviour _corruptionTierSource;

        [SerializeField] private SceneObjectAllocationSystem _objectAllocationSystem;
        [SerializeField] private List<PlaceableObjectData> _chestPool;
        [SerializeField] private List<Transform> _allPositions;

        [Tooltip("Index = tier (0 to 4). Number of ACTIVE chests at that tier is rolled within this range on every (re)allocation.")]
        [SerializeField] private ChestCountRange[] _chestCountRangeByTier = new ChestCountRange[5];

        private readonly List<GameObject> _chestPoolInstances = new();
        private bool _poolCreated;
        private int _manualTestTier;

        public event Action<Chest> OnChestCreated;

        /// <summary>
        /// Raised every time chests are (re)positioned — on the initial
        /// placement AND on every subsequent Reallocate() call — with the
        /// list of chests that ended up active. Consumed by
        /// ChestLootReevaluationSystem to assign fresh loot (which also
        /// resets each chest's consumed state) whenever positions change.
        /// </summary>
        public event Action<IReadOnlyList<Chest>> OnChestsRepositioned;

        public int ActiveChestCount { get; private set; }
        public bool IsPoolCreated => _poolCreated;

        private IObjectAllocationSystem Allocator => _objectAllocationSystem;
        private ICorruptionTierSource TierSource => _corruptionTierSource as ICorruptionTierSource;

        /// <summary>
        /// Resolves the tier to use: the real ICorruptionTierSource when assigned,
        /// otherwise the manual test tier (defaults to 0). This lets the system be
        /// exercised in the editor/Play Mode without a real corruption source wired up.
        /// </summary>
        public int CurrentTier => TierSource?.CurrentTier ?? _manualTestTier;

        private void Start() => Initialize();

        /// <summary>
        /// Creates the chest pool (once) and applies the initial tier-based
        /// positioning. Public and async void so it can be triggered either
        /// automatically by Start() or manually — e.g. from an editor button —
        /// without requiring the caller to await a Task.
        /// </summary>
        public async void Initialize()
        {
            if (_poolCreated)
            {
                Log(LogLevel.Warning, "Initialize called but the pool already exists — ignored.");
                return;
            }

            if (!Validate()) return;

            // Forces a real yield so this method can never complete fully
            // synchronously within the same Start() call — see ChestLootReevaluationSystem
            // for why that race condition mattered.
            await Task.Yield();

            await CreatePoolOnceAsync();
            ApplyTierBasedPositions();
        }

        /// <summary>
        /// Entry point for the external event (not yet defined) that
        /// requests repositioning of the already-existing chests. Each call
        /// re-rolls the active count within the current tier's range.
        /// </summary>
        public void Reallocate()
        {
            if (!_poolCreated)
            {
                Log(LogLevel.Warning, "Reallocate called before pool creation — call Initialize() first.");
                return;
            }

            ApplyTierBasedPositions();
        }

        public void SetManualTestTier(int tier)
        {
            _manualTestTier = Mathf.Max(0, tier);
            Log(LogLevel.Debug, $"Manual test tier set to {_manualTestTier} (used only while no ICorruptionTierSource is assigned).");
        }

        // ── Pool creation (once) ─────────────────────────────────────

        private async Task CreatePoolOnceAsync()
        {
            if (_poolCreated) return;

            int maxPoolSize = ResolveMaxPossibleCount();
            var initialPositions = PickRandomSubset(_allPositions, maxPoolSize);

            AllocationResult result = await Allocator.AllocateObjectsAsync(_chestPool, initialPositions);

            foreach (var placed in result.PlacedObjects)
            {
                _chestPoolInstances.Add(placed.Instance);

                var chest = placed.Instance.GetComponent<Chest>();
                if (chest == null)
                {
                    Log(LogLevel.Error, $"Prefab '{placed.SourceData.name}' has no Chest component.");
                    continue;
                }

                OnChestCreated?.Invoke(chest);
            }

            _poolCreated = true;
            Log(LogLevel.Debug, $"Chest pool created: {_chestPoolInstances.Count} instance(s).");
        }

        /// <summary>
        /// The pool must be large enough to cover the highest possible roll
        /// across every tier's range — otherwise a high roll on a high-Max
        /// tier could exceed the number of instances actually created.
        /// </summary>
        private int ResolveMaxPossibleCount()
        {
            int max = 0;
            foreach (var range in _chestCountRangeByTier)
                max = Mathf.Max(max, range.Max);

            return Mathf.Min(max, _allPositions.Count);
        }

        // ── Repositioning (repeatable) ───────────────────────────────

        private void ApplyTierBasedPositions()
        {
            int tier = CurrentTier;
            int desiredCount = ResolveCountForTier(tier);
            var chosenPositions = PickRandomSubset(_allPositions, desiredCount);
            var activeChests = new List<Chest>(desiredCount);

            for (int i = 0; i < _chestPoolInstances.Count; i++)
            {
                var instance = _chestPoolInstances[i];
                if (instance == null) continue;

                bool shouldBeActive = i < desiredCount;
                instance.SetActive(shouldBeActive);

                if (shouldBeActive)
                {
                    var target = chosenPositions[i];

                    // Only the position is updated here. Setting rotation from the
                    // target marker as well (as an earlier version did via
                    // SetPositionAndRotation) would overwrite the random rotation
                    // originally rolled per-instance by SceneObjectAllocationSystem
                    // during CreatePoolOnceAsync, discarding that randomization on
                    // every single reallocation.
                    instance.transform.position = target.position;

                    var chest = instance.GetComponent<Chest>();
                    if (chest != null) activeChests.Add(chest);
                }
            }

            ActiveChestCount = desiredCount;
            Log(LogLevel.Debug, $"Chests repositioned: {desiredCount} active at tier {tier}.");

            OnChestsRepositioned?.Invoke(activeChests);
        }

        private int ResolveCountForTier(int tier)
        {
            if (tier < 0 || tier >= _chestCountRangeByTier.Length)
            {
                Log(LogLevel.Warning, $"Tier {tier} outside the count range lookup — using 0.");
                return 0;
            }

            int rolled = _chestCountRangeByTier[tier].Resolve();
            return Mathf.Min(rolled, _chestPoolInstances.Count);
        }

        private List<Transform> PickRandomSubset(List<Transform> source, int count)
        {
            var pool = new List<Transform>(source);
            var chosen = new List<Transform>(count);

            while (chosen.Count < count && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                chosen.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return chosen;
        }

        private bool Validate()
        {
            if (_objectAllocationSystem == null) { Log(LogLevel.Error, "SceneObjectAllocationSystem not assigned."); return false; }
            if (_chestPool == null || _chestPool.Count == 0) { Log(LogLevel.Error, "Chest pool is empty."); return false; }
            if (_allPositions == null || _allPositions.Count == 0) { Log(LogLevel.Error, "Available positions are empty."); return false; }
            if (_chestCountRangeByTier == null || _chestCountRangeByTier.Length == 0) { Log(LogLevel.Error, "_chestCountRangeByTier is empty."); return false; }

            if (TierSource == null)
                Log(LogLevel.Warning, "No ICorruptionTierSource assigned — using manual test tier as fallback.");

            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ChestAllocationSystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}