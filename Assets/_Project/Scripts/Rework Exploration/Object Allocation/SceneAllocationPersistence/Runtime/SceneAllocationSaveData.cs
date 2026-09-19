using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.SceneAllocation
{
    /// <summary>
    /// Serializable snapshot of one allocation pass: which
    /// PlaceableObjectData (by registry id) ended up at which position
    /// (by index into the original availablePositions list), with the
    /// exact scale/rotation that was rolled at allocation time — so a load
    /// reproduces the scene exactly, without re-rolling anything.
    /// </summary>
    [Serializable]
    public sealed class SceneAllocationSaveData
    {
        [Serializable]
        public struct PlacedEntry
        {
            /// <summary>Index into the availablePositions list passed to the allocator — the unique identifier for a position.</summary>
            public int PositionIndex;
            public string PlaceableObjectId;
            public Vector3 Scale;
            public Vector3 EulerRotation;
        }

        public List<PlacedEntry> Placements = new();
    }
}
