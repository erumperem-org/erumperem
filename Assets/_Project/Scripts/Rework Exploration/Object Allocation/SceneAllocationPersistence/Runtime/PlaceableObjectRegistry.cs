using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using SceneAllocation;

namespace Core.SceneAllocation
{
    /// <summary>
    /// One registry entry: a unique id paired with a PlaceableObjectData
    /// reference. The id lives here, not on PlaceableObjectData itself,
    /// since that type is part of the pre-existing Scene Object Allocation
    /// System and has no id field of its own.
    /// </summary>
    [Serializable]
    public struct PlaceableObjectEntry
    {
        [SerializeField] private string _id;
        [SerializeField] private PlaceableObjectData _data;

        public string Id => _id;
        public PlaceableObjectData Data => _data;
    }

    /// <summary>
    /// Maps a unique string id to a PlaceableObjectData asset, so save data
    /// can reference "which object type was placed" without serializing an
    /// object reference directly. Mirrors ItemRegistry/CoinRegistry's role,
    /// adapted to a type that doesn't carry its own id.
    ///
    /// Create via: Assets → Create → Scene Allocation → Placeable Object Registry
    /// </summary>
    [CreateAssetMenu(menuName = "Scene Allocation/Placeable Object Registry", fileName = "PlaceableObjectRegistry")]
    public sealed class PlaceableObjectRegistry : ScriptableObject
    {
        [Tooltip("Every PlaceableObjectData used by any save-backed allocation. Each id must be unique.")]
        [SerializeField] private List<PlaceableObjectEntry> _entries = new();

        private Dictionary<string, PlaceableObjectData> _lookup;
        private Dictionary<PlaceableObjectData, string> _reverseLookup;

        public IReadOnlyList<PlaceableObjectEntry> Entries => _entries;

        private void OnEnable() => BuildLookups();

        /// <summary>Resolves an id to its PlaceableObjectData — used when loading a save.</summary>
        public PlaceableObjectData Resolve(string id)
        {
            if (_lookup == null) BuildLookups();
            return _lookup.TryGetValue(id, out var data) ? data : null;
        }

        /// <summary>Resolves a PlaceableObjectData back to its id — used when writing a save after a fresh allocation.</summary>
        public string ResolveId(PlaceableObjectData data)
        {
            if (_reverseLookup == null) BuildLookups();
            return _reverseLookup.TryGetValue(data, out var id) ? id : null;
        }

        private void BuildLookups()
        {
            _lookup = new Dictionary<string, PlaceableObjectData>(_entries.Count);
            _reverseLookup = new Dictionary<PlaceableObjectData, string>(_entries.Count);

            foreach (var entry in _entries)
            {
                if (entry.Data == null) continue;

                if (string.IsNullOrEmpty(entry.Id))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[PlaceableObjectRegistry] Entry for '{entry.Data.name}' has an empty id — skipped.",
                        LogCategory.Inventory);
                    continue;
                }

                if (_lookup.ContainsKey(entry.Id))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[PlaceableObjectRegistry] Duplicate id: '{entry.Id}' — skipped.",
                        LogCategory.Inventory);
                    continue;
                }

                _lookup[entry.Id] = entry.Data;
                _reverseLookup[entry.Data] = entry.Id;
            }
        }
    }
}
