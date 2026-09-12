using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneAllocation
{
    /// <summary>
    /// Abstraction for an object allocation system. A centralized orchestration
    /// service should depend on this interface rather than on the concrete
    /// MonoBehaviour implementation, so that:
    ///   - execution order can be guaranteed purely through "await" calls;
    ///   - the system can be mocked/stubbed in edit-mode or play-mode tests.
    /// </summary>
    public interface IObjectAllocationSystem
    {
        /// <summary>
        /// Allocates objects from <paramref name="objectPool"/> into the given
        /// <paramref name="availablePositions"/> and returns once every position
        /// has been handled (filled or skipped). Safe to await from a caller
        /// that needs later steps to run only after allocation completes.
        /// </summary>
        Task<AllocationResult> AllocateObjectsAsync(
            IReadOnlyList<PlaceableObjectData> objectPool,
            IReadOnlyList<Transform> availablePositions,
            CancellationToken cancellationToken = default);
    }
}
