using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Services.DebugUtilities;
using UnityEngine;
using SceneAllocation;

namespace Core.SceneAllocation
{
    /// <summary>
    /// Save/load wrapper around the pre-existing SceneObjectAllocationSystem.
    /// On Initialize(): if no save file exists (or it's empty), runs the
    /// real allocation routine (AllocateObjectsAsync — random, balanced
    /// selection) and persists the result. If a save file exists, skips the
    /// selection routine entirely and reconstructs the exact same
    /// placements (prefab, position, scale, rotation) directly from disk.
    ///
    /// Save, Load and Delete are also exposed as independent operations
    /// (not just the combined Initialize() flow), for manual/editor use.
    ///
    /// Depends on SceneObjectAllocationSystem / PlaceableObjectData /
    /// AllocationResult / IObjectAllocationSystem, assumed to already exist
    /// in the project — not redefined here. Also depends on the project's
    /// SaveSlotAccess/CurrentSlotData singleton for resolving the save path.
    /// </summary>
    public sealed class SceneObjectAllocationSaveSystem : MonoBehaviour
    {
        private struct CurrentPlacement
        {
            public int PositionIndex;
            public PlaceableObjectData Data;
            public GameObject Instance;
        }

        [SerializeField] private SceneObjectAllocationSystem _allocationSystem;
        [SerializeField] private PlaceableObjectRegistry _registry;
        [SerializeField] private List<PlaceableObjectData> _objectPool;
        [SerializeField] private List<Transform> _availablePositions;

        [Tooltip("Parent for every instance this system spawns, both freshly allocated and restored from save. Optional — leave null for no parenting.")]
        [SerializeField] private Transform _instancesParent;

        [SerializeField] private string _fileName = "scene_allocation.json";

        private readonly List<CurrentPlacement> _currentPlacements = new();

        public bool IsInitialized { get; private set; }
        public int SpawnedCount => _currentPlacements.Count;

        private IObjectAllocationSystem Allocator => _allocationSystem;

        private async void Start() => await Initialize();

        // ── Combined startup flow ────────────────────────────────────

        /// <summary>
        /// Runs the load-or-allocate routine. Safe to call manually in
        /// addition to the automatic Start() call.
        /// </summary>
        public async Task Initialize()
        {
            if (IsInitialized)
            {
                Log(LogLevel.Warning, "Initialize called but this system is already initialized — ignored.");
                return;
            }

            if (!Validate()) return;

            var saveData = TryReadSaveDataFromDisk();

            if (saveData != null && saveData.Placements.Count > 0)
            {
                LoadFromData(saveData);
                Log(LogLevel.Debug, $"Restored {_currentPlacements.Count} instance(s) from save — allocation routine skipped.");
            }
            else
            {
                await RunAllocationAndSave();
            }

            IsInitialized = true;
        }

        // ── Independent operations ───────────────────────────────────

        /// <summary>
        /// Saves the CURRENT scene state (whatever is presently spawned,
        /// including any manual changes made after load/allocation) to the
        /// save file, overwriting it.
        /// </summary>
        public void Save()
        {
            if (_currentPlacements.Count == 0)
            {
                Log(LogLevel.Warning, "Save called with nothing currently placed — writing an empty save file.");
            }

            var saveData = BuildSaveDataFromCurrentPlacements();
            WriteSaveDataToDisk(saveData);
            Log(LogLevel.Debug, $"Saved {saveData.Placements.Count} placement(s) to disk.");
        }

        /// <summary>
        /// Loads from the save file, destroying whatever is currently
        /// spawned first. Does NOT fall back to running the allocation
        /// routine if the file is missing/empty — use Initialize() for
        /// that combined behavior. Logs and does nothing if there's
        /// nothing to load.
        /// </summary>
        public void Load()
        {
            var saveData = TryReadSaveDataFromDisk();

            if (saveData == null || saveData.Placements.Count == 0)
            {
                Log(LogLevel.Warning, "Load called but no valid save data was found — nothing changed.");
                return;
            }

            ClearCurrentPlacements();
            LoadFromData(saveData);
            IsInitialized = true;

            Log(LogLevel.Debug, $"Loaded {_currentPlacements.Count} instance(s) from save.");
        }

        /// <summary>Deletes the save file from disk only. Does not touch currently spawned instances.</summary>
        public void Delete()
        {
            string path = GetSavePath();
            if (path == null) return;

            if (!File.Exists(path))
            {
                Log(LogLevel.Debug, "No save file to delete.");
                return;
            }

            try
            {
                File.Delete(path);
                Log(LogLevel.Debug, $"Save '{path}' deleted.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to delete save file: {ex.Message}");
            }
        }

        /// <summary>Destroys every spawned instance AND deletes the save file, resetting this system entirely.</summary>
        public void ResetAndDeleteSave()
        {
            ClearCurrentPlacements();
            IsInitialized = false;
            Delete();
        }

        // ── Diagnostics (for editor display) ─────────────────────────

        /// <summary>Absolute path to the save file, or null if no save slot is currently selected.</summary>
        public string GetSavePath()
        {
            var slotData = SaveSlotAccess.Data;

            if (slotData == null || !slotData.HasSlotSelected)
            {
                Log(LogLevel.Error, "No save slot selected (SaveSlotAccess.Data is null or HasSlotSelected is false) — cannot resolve a save path.");
                return null;
            }

            return Path.Combine(slotData.SlotDirectory, _fileName);
        }

        public bool SaveFileExists()
        {
            string path = GetSavePath();
            return path != null && File.Exists(path);
        }

        /// <summary>Number of placements currently recorded in the save file on disk (0 if missing/empty), without affecting the live scene.</summary>
        public int GetSavedPlacementCountOnDisk()
        {
            var data = TryReadSaveDataFromDisk();
            return data?.Placements.Count ?? 0;
        }

        public long GetSaveFileSizeBytes()
        {
            string path = GetSavePath();
            if (path == null || !File.Exists(path)) return 0;

            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        // ── Fresh allocation path ────────────────────────────────────

        private async Task RunAllocationAndSave()
        {
            AllocationResult result = await Allocator.AllocateObjectsAsync(_objectPool, _availablePositions, _instancesParent);

            _currentPlacements.Clear();

            foreach (var placed in result.PlacedObjects)
            {
                int positionIndex = _availablePositions.IndexOf(placed.Position);

                if (positionIndex < 0)
                {
                    Log(LogLevel.Error, $"Placed position for '{placed.SourceData.name}' was not found in _availablePositions — its placement will NOT be tracked/saved.");
                    continue;
                }

                _currentPlacements.Add(new CurrentPlacement
                {
                    PositionIndex = positionIndex,
                    Data = placed.SourceData,
                    Instance = placed.Instance
                });
            }

            var saveData = BuildSaveDataFromCurrentPlacements();
            WriteSaveDataToDisk(saveData);

            Log(LogLevel.Debug, $"Allocation routine ran: {result.PlacedCount} instance(s) placed and saved.");
        }

        // ── Restore-from-save path ───────────────────────────────────

        private void LoadFromData(SceneAllocationSaveData saveData)
        {
            foreach (var entry in saveData.Placements)
            {
                if (entry.PositionIndex < 0 || entry.PositionIndex >= _availablePositions.Count)
                {
                    Log(LogLevel.Error, $"Saved position index {entry.PositionIndex} is out of range — entry skipped.");
                    continue;
                }

                var data = _registry.Resolve(entry.PlaceableObjectId);
                if (data == null || data.Prefab == null)
                {
                    Log(LogLevel.Error, $"Saved id '{entry.PlaceableObjectId}' could not be resolved to a PlaceableObjectData with a prefab — entry skipped.");
                    continue;
                }

                var position = _availablePositions[entry.PositionIndex];
                var instance = Instantiate(data.Prefab, position.position, Quaternion.Euler(entry.EulerRotation), _instancesParent);
                instance.transform.localScale = entry.Scale;

                _currentPlacements.Add(new CurrentPlacement
                {
                    PositionIndex = entry.PositionIndex,
                    Data = data,
                    Instance = instance
                });
            }
        }

        private void ClearCurrentPlacements()
        {
            foreach (var placement in _currentPlacements)
                if (placement.Instance != null) Destroy(placement.Instance);

            _currentPlacements.Clear();
        }

        // ── Save data <-> current placements ─────────────────────────

        private SceneAllocationSaveData BuildSaveDataFromCurrentPlacements()
        {
            var saveData = new SceneAllocationSaveData();

            foreach (var placement in _currentPlacements)
            {
                if (placement.Instance == null) continue;

                string id = _registry.ResolveId(placement.Data);
                if (string.IsNullOrEmpty(id))
                {
                    Log(LogLevel.Error, $"PlaceableObjectData '{placement.Data.name}' has no registry id — its placement will NOT be saved.");
                    continue;
                }

                saveData.Placements.Add(new SceneAllocationSaveData.PlacedEntry
                {
                    PositionIndex = placement.PositionIndex,
                    PlaceableObjectId = id,
                    Scale = placement.Instance.transform.localScale,
                    EulerRotation = placement.Instance.transform.eulerAngles
                });
            }

            return saveData;
        }

        // ── File I/O ─────────────────────────────────────────────────

        private SceneAllocationSaveData TryReadSaveDataFromDisk()
        {
            string path = GetSavePath();
            if (path == null || !File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;

                return JsonUtility.FromJson<SceneAllocationSaveData>(json);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to read save file — treating as empty: {ex.Message}");
                return null;
            }
        }

        private void WriteSaveDataToDisk(SceneAllocationSaveData saveData)
        {
            string path = GetSavePath();
            if (path == null) return; // error already logged by GetSavePath

            try
            {
                string json = JsonUtility.ToJson(saveData, prettyPrint: true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to write save file: {ex.Message}");
            }
        }

        // ── Validation ───────────────────────────────────────────────

        private bool Validate()
        {
            if (_allocationSystem == null) { Log(LogLevel.Error, "SceneObjectAllocationSystem not assigned."); return false; }
            if (_registry == null) { Log(LogLevel.Error, "PlaceableObjectRegistry not assigned."); return false; }
            if (_objectPool == null || _objectPool.Count == 0) { Log(LogLevel.Error, "Object pool is empty."); return false; }
            if (_availablePositions == null || _availablePositions.Count == 0) { Log(LogLevel.Error, "Available positions are empty."); return false; }
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[SceneObjectAllocationSaveSystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}