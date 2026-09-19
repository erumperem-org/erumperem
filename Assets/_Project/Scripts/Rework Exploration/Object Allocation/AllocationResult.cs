using System.Collections.Generic;
using UnityEngine;

namespace SceneAllocation
{
    /// <summary>
    /// Information about a single object that was successfully placed in the scene.
    /// </summary>
    public sealed class PlacedObjectInfo
    {
        /// <summary>The instantiated GameObject.</summary>
        public GameObject Instance { get; }

        /// <summary>The ScriptableObject definition used to create this instance.</summary>
        public PlaceableObjectData SourceData { get; }

        /// <summary>The position (Transform) this instance was allocated to.</summary>
        public Transform Position { get; }

        public PlacedObjectInfo(GameObject instance, PlaceableObjectData sourceData, Transform position)
        {
            Instance = instance;
            SourceData = sourceData;
            Position = position;
        }
    }

    /// <summary>
    /// Summary of a single call to <see cref="SceneObjectAllocationSystem.AllocateObjectsAsync"/>.
    /// </summary>
    public sealed class AllocationResult
    {
        /// <summary>All objects that were successfully instantiated and placed.</summary>
        public List<PlacedObjectInfo> PlacedObjects { get; } = new List<PlacedObjectInfo>();

        /// <summary>How many positions were originally requested to be filled.</summary>
        public int RequestedCount { get; set; }

        /// <summary>How many positions were actually filled.</summary>
        public int PlacedCount => PlacedObjects.Count;

        /// <summary>True when every requested position received an object.</summary>
        public bool WasFullyAllocated => PlacedCount >= RequestedCount;
    }
}
