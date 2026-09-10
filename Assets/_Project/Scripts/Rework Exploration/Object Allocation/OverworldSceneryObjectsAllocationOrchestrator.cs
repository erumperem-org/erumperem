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
    public class OverworldSceneryObjectsAllocationOrchestrator : MonoBehaviour
    {
        [Header("System")]
        [SerializeField] private SceneObjectAllocationSystem allocationSystem;

        [Header("Scenery Objects")]
        [SerializeField] private List<PlaceableObjectData> sceneryPool;
        [SerializeField] private List<Transform> sceneryPositions;

        private async void Start()
        {
            await RunSceneSetupAsync();
        }

        /// <summary>
        /// Runs allocation for every scenery object in a single step, since
        /// there's no ordering dependency between them - unlike a multi-step
        /// setup (e.g. trees before rocks), one await is enough here.
        /// </summary>
        private async Task RunSceneSetupAsync()
        {
            Debug.Log("[Orchestrator] Allocating scenery objects...");
            AllocationResult sceneryResult = await allocationSystem.AllocateObjectsAsync(sceneryPool, sceneryPositions);
            Debug.Log($"[Orchestrator] Scenery objects placed: {sceneryResult.PlacedCount}/{sceneryResult.RequestedCount}");

            Debug.Log("[Orchestrator] Scene setup complete.");
        }
    }
}