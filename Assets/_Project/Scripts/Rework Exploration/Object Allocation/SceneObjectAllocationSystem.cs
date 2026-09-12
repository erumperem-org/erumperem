using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneAllocation
{
    /// <summary>
    /// Central system responsible for allocating (instantiating) objects defined
    /// by <see cref="PlaceableObjectData"/> assets into a set of available scene
    /// positions.
    ///
    /// Selection of which object type to place is weighted so that, over many
    /// allocations, every entry in the object pool is used a comparable number
    /// of times (see "Balancing Algorithm" in README.md), while each individual
    /// pick still remains random.
    ///
    /// Positions that have already received an object (in this call or in a
    /// previous one) are tracked persistently via <see cref="occupiedPositions"/>,
    /// so subsequent rounds never place a new object on top of one that's
    /// already there. Call <see cref="ResetOccupancy"/> when those positions
    /// become free again (e.g. scene teardown, pool recycling).
    ///
    /// The public entry point, <see cref="AllocateObjectsAsync"/>, is an async
    /// Task so that a centralized orchestration service can "await" it and
    /// guarantee ordered execution relative to other asynchronous scene-setup
    /// steps (e.g. "await allocatorA.AllocateObjectsAsync(...); await
    /// allocatorB.AllocateObjectsAsync(...);").
    /// </summary>
    public class SceneObjectAllocationSystem : MonoBehaviour, IObjectAllocationSystem
    {
        [Header("Balancing")]
        [Tooltip("0 = pure random selection (no balancing). Higher values push the " +
                 "system harder towards picking the least-used entries first. " +
                 "1 is a good default; try 0.5 for lighter balancing or 2 for near-round-robin.")]
        [Range(0f, 2f)]
        [SerializeField] private float balanceStrength = 1f;

        [Header("Async Behaviour")]
        [Tooltip("If true, the allocator yields control back to Unity every " +
                 "'placementsPerFrame' placements, spreading instantiation cost " +
                 "across frames instead of doing all of it in a single frame.")]
        [SerializeField] private bool spreadAcrossFrames = true;

        [Tooltip("Number of objects placed between yields, when spreadAcrossFrames is enabled.")]
        [Min(1)]
        [SerializeField] private int placementsPerFrame = 5;

        [Header("Parenting")]
        [Tooltip("Optional parent for all instantiated objects. If null, instances are placed at the scene root.")]
        [SerializeField] private Transform instancesParent;

        // Tracks how many times each PlaceableObjectData has been used across the
        // lifetime of this system instance. Drives the balancing weights.
        private readonly Dictionary<PlaceableObjectData, int> usageCounts = new Dictionary<PlaceableObjectData, int>();

        // Tracks which positions have already received an instance, across all
        // calls to AllocateObjectsAsync (not just within a single call). This is
        // what prevents a later round from allocating on top of a position that
        // was already filled by an earlier round.
        private readonly HashSet<Transform> occupiedPositions = new HashSet<Transform>();

        // A dedicated RNG instance (instead of UnityEngine.Random) so that weighted
        // selection math (System.Random.NextDouble) is simple to reason about and
        // is not implicitly tied to Unity's global random state/seed.
        private readonly System.Random rng = new System.Random();

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Clears all balancing statistics. Call this when you want the selection
        /// weighting to "forget" previous allocations (e.g. when starting a new
        /// level/scene with a fresh pool).
        /// </summary>
        public void ResetBalancing()
        {
            usageCounts.Clear();
        }

        /// <summary>
        /// Clears the record of which positions are considered occupied. Call this
        /// when the previously allocated instances have been removed/destroyed and
        /// their positions are free to receive new objects again (e.g. scene reset,
        /// pool recycling). Does not affect balancing statistics.
        /// </summary>
        public void ResetOccupancy()
        {
            occupiedPositions.Clear();
        }

        /// <summary>
        /// Marks a specific position as free again, without affecting any other
        /// occupied position. Useful when only a single instance was removed
        /// (e.g. destroyed by gameplay) and its slot should become available.
        /// </summary>
        public void ReleasePosition(Transform position)
        {
            if (position != null)
            {
                occupiedPositions.Remove(position);
            }
        }

        /// <summary>
        /// Read-only snapshot of current usage counts per object definition.
        /// Useful for debugging or displaying balance statistics in editor tooling.
        /// </summary>
        public IReadOnlyDictionary<PlaceableObjectData, int> GetUsageSnapshot()
        {
            return new Dictionary<PlaceableObjectData, int>(usageCounts);
        }

        /// <summary>
        /// Allocates objects from <paramref name="objectPool"/> into the given
        /// <paramref name="availablePositions"/>.
        ///
        /// Positions already marked as occupied (from this or any previous call)
        /// are skipped entirely — they are filtered out before allocation starts,
        /// so an already-filled position can never receive a second object.
        ///
        /// For each remaining position: an object definition is chosen via
        /// balanced-random weighted selection, instantiated, randomized
        /// (scale/rotation per its own configured ranges) and parented at that
        /// position. Positions are consumed one at a time and are never reused
        /// within the same call.
        /// </summary>
        /// <param name="objectPool">Pool of object definitions to choose from.</param>
        /// <param name="availablePositions">Positions (as Transforms) that can each receive one object.</param>
        /// <param name="cancellationToken">Optional token to cancel a long-running allocation between placements.</param>
        /// <returns>
        /// A Task&lt;AllocationResult&gt; that completes once every eligible position
        /// has been handled. Intended to be awaited by a centralized service to
        /// guarantee execution order.
        /// </returns>
        public async Task<AllocationResult> AllocateObjectsAsync(
            IReadOnlyList<PlaceableObjectData> objectPool,
            IReadOnlyList<Transform> availablePositions,
            CancellationToken cancellationToken = default)
        {
            var result = new AllocationResult { RequestedCount = availablePositions?.Count ?? 0 };

            if (objectPool == null || objectPool.Count == 0)
            {
                Debug.LogWarning("[SceneObjectAllocationSystem] Allocation aborted: object pool is empty or null.");
                return result;
            }

            if (availablePositions == null || availablePositions.Count == 0)
            {
                Debug.LogWarning("[SceneObjectAllocationSystem] Allocation aborted: no available positions were provided.");
                return result;
            }

            // Work on a private copy so remaining positions can be freely consumed
            // without mutating the caller's list. Null transforms and positions
            // already occupied by a previous round are filtered out up front.
            var remainingPositions = new List<Transform>(
                availablePositions.Where(p => p != null && !occupiedPositions.Contains(p)));

            if (remainingPositions.Count == 0)
            {
                Debug.LogWarning("[SceneObjectAllocationSystem] Allocation skipped: " +
                                  "all provided positions are already occupied.");
                return result;
            }

            int placedSinceLastYield = 0;

            while (remainingPositions.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 1. Randomly pick one of the still-available positions.
                int positionIndex = rng.Next(remainingPositions.Count);
                Transform targetPosition = remainingPositions[positionIndex];
                remainingPositions.RemoveAt(positionIndex);

                // 2. Pick which object type to place using balanced weighted selection.
                PlaceableObjectData chosenData = ChooseBalancedObject(objectPool);
                if (chosenData == null || chosenData.Prefab == null)
                {
                    Debug.LogWarning("[SceneObjectAllocationSystem] Skipped a position: " +
                                      "selected PlaceableObjectData has no prefab assigned.");
                    continue;
                }

                // 3. Instantiate and randomize the instance at the target position.
                GameObject instance = InstantiateAt(chosenData, targetPosition);

                // 4. Register usage for future balancing, mark the position as
                //    occupied so future calls skip it, and record the outcome.
                RegisterUsage(chosenData);
                occupiedPositions.Add(targetPosition);
                result.PlacedObjects.Add(new PlacedObjectInfo(instance, chosenData, targetPosition));

                // 5. Optionally yield to spread instantiation cost across frames while
                //    keeping the overall call awaitable/ordered for the caller.
                placedSinceLastYield++;
                if (spreadAcrossFrames && placedSinceLastYield >= placementsPerFrame)
                {
                    placedSinceLastYield = 0;
                    await Task.Yield();
                }
            }

            return result;
        }

        // ---------------------------------------------------------------
        // Internal helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Instantiates <paramref name="data"/>.Prefab at <paramref name="targetPosition"/>,
        /// applying a random scale and rotation drawn from the ranges configured
        /// on that ScriptableObject.
        /// </summary>
        private GameObject InstantiateAt(PlaceableObjectData data, Transform targetPosition)
        {
            GameObject instance = Instantiate(
                data.Prefab,
                targetPosition.position,
                data.GetRandomRotation(),
                instancesParent != null ? instancesParent : null);

            instance.transform.localScale = data.GetRandomScale();
            instance.name = $"{data.Prefab.name} (Allocated)";
            return instance;
        }

        /// <summary>
        /// Picks an entry from <paramref name="objectPool"/> using weighted random
        /// selection. The weight of each entry is inversely proportional to how
        /// often it has already been used, raised to <see cref="balanceStrength"/>.
        /// This keeps every individual pick random while steering the overall
        /// distribution towards balance across the whole pool.
        /// See README.md, section "Balancing Algorithm", for the full explanation
        /// and worked examples.
        /// </summary>
        private PlaceableObjectData ChooseBalancedObject(IReadOnlyList<PlaceableObjectData> objectPool)
        {
            // Make sure every pool entry has a usage record, so unseen entries
            // are treated as having a usage count of 0 (i.e. maximally favored).
            foreach (var data in objectPool)
            {
                if (data != null && !usageCounts.ContainsKey(data))
                {
                    usageCounts[data] = 0;
                }
            }

            var weights = new List<(PlaceableObjectData data, double weight)>(objectPool.Count);
            double totalWeight = 0;

            foreach (var data in objectPool)
            {
                if (data == null)
                {
                    continue;
                }

                int usage = usageCounts.TryGetValue(data, out int count) ? count : 0;

                // "+1" avoids division by zero and keeps the weight finite for
                // never-used entries; raising to balanceStrength controls how
                // aggressively usage differences are amplified.
                double weight = 1.0 / Math.Pow(usage + 1, balanceStrength);

                weights.Add((data, weight));
                totalWeight += weight;
            }

            if (weights.Count == 0)
            {
                return null;
            }

            double roll = rng.NextDouble() * totalWeight;
            double cumulative = 0;

            foreach (var (data, weight) in weights)
            {
                cumulative += weight;
                if (roll <= cumulative)
                {
                    return data;
                }
            }

            // Reached only due to floating point rounding at the very edge of the
            // cumulative range; returning the last candidate is a safe fallback.
            return weights[weights.Count - 1].data;
        }

        private void RegisterUsage(PlaceableObjectData data)
        {
            if (!usageCounts.ContainsKey(data))
            {
                usageCounts[data] = 0;
            }
            usageCounts[data]++;
        }
    }
}