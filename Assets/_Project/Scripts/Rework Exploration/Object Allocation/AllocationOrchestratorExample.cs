using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneAllocation.Examples
{
    /// <summary>
    /// Minimal example of a "centralized service" that depends on
    /// IObjectAllocationSystem and guarantees ordering purely through await —
    /// exactly the integration pattern this package was designed for.
    ///
    /// This is example/demo code, not part of the core system: it is safe to
    /// delete or rewrite entirely to fit your project's own orchestration layer.
    /// </summary>
    public class AllocationOrchestratorExample : MonoBehaviour
    {
        [Header("System")]
        [SerializeField] private SceneObjectAllocationSystem allocationSystem;

        [Header("Step 1: Trees")]
        [SerializeField] private List<PlaceableObjectData> treePool;
        [SerializeField] private List<Transform> treePositions;

        [Header("Step 2: Rocks (placed only after all trees are done)")]
        [SerializeField] private List<PlaceableObjectData> rockPool;
        [SerializeField] private List<Transform> rockPositions;

        private async void Start()
        {
            await RunSceneSetupAsync();
        }

        /// <summary>
        /// Runs allocation steps strictly in order: rocks are only placed after
        /// every tree has finished being allocated, because each step is awaited.
        /// </summary>
        private async Task RunSceneSetupAsync()
        {
            Debug.Log("[Orchestrator] Allocating trees...");
            AllocationResult treeResult = await allocationSystem.AllocateObjectsAsync(treePool, treePositions);
            Debug.Log($"[Orchestrator] Trees placed: {treeResult.PlacedCount}/{treeResult.RequestedCount}");

            Debug.Log("[Orchestrator] Allocating rocks...");
            AllocationResult rockResult = await allocationSystem.AllocateObjectsAsync(rockPool, rockPositions);
            Debug.Log($"[Orchestrator] Rocks placed: {rockResult.PlacedCount}/{rockResult.RequestedCount}");

            Debug.Log("[Orchestrator] Scene setup complete.");
        }
    }
}
