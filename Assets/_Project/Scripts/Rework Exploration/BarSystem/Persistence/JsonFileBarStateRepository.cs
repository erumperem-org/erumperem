using System.IO;
using UnityEngine;
using BarSystem.Core;
using Services.DebugUtilities;
using SceneAllocation;

namespace BarSystem.Persistence
{
    /// <summary>
    /// Reference implementation of IBarStateRepository: one .json file
    /// per bar, saved inside the currently selected save slot's directory
    /// (SaveSlotAccess.Data.SlotDirectory), under a sub-folder. Each Id
    /// becomes a separate file, avoiding write conflicts between bars.
    ///
    /// The folder is resolved on every call, so the repository follows
    /// slot changes and can be constructed before a slot is selected.
    /// If no slot is selected, Save does nothing, Load returns null and
    /// HasSavedState returns false (an error is logged).
    /// </summary>
    public class JsonFileBarStateRepository : IBarStateRepository
    {
        private readonly string _folderName;

        public JsonFileBarStateRepository(string folderName = "BarSystemSaves")
        {
            _folderName = folderName;
        }

        public void Save(BarSaveData data)
        {
            if (data == null) return;

            string path = GetPath(data.Id);
            if (path == null) return; // error already logged

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(path, json);
            }
            catch (System.Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save bar '{data.Id}': {ex.Message}");
            }
        }

        public BarSaveData Load(string id)
        {
            string path = GetPath(id);
            if (path == null || !File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<BarSaveData>(json);
            }
            catch (System.Exception ex)
            {
                Log(LogLevel.Error, $"Failed to load bar '{id}': {ex.Message}");
                return null;
            }
        }

        public bool HasSavedState(string id)
        {
            string path = GetPath(id);
            return path != null && File.Exists(path);
        }

        /// <summary>
        /// Absolute path of the bar's file inside the selected slot, or
        /// null if no slot is selected.
        /// </summary>
        private string GetPath(string id)
        {
            var slotData = SaveSlotAccess.Data;

            if (slotData == null || !slotData.HasSlotSelected)
            {
                Log(LogLevel.Error,
                    "No save slot selected (SaveSlotAccess.Data is null or HasSlotSelected is false) — cannot resolve a save path.");
                return null;
            }

            return Path.Combine(slotData.SlotDirectory, _folderName, $"{id}.json");
        }

        private static void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[JsonFileBarStateRepository] {msg}", LogCategory.Inventory);
    }
}