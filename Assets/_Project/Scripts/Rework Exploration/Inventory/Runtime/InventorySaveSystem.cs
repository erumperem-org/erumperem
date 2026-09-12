using System;
using System.IO;
using System.Threading.Tasks;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.Inventory
{
    /// <summary>
    /// Persists/restores an InventorySystem to/from its own JSON file
    /// (one file per inventory — the losable and permanent inventories
    /// are independent).
    /// </summary>
    public sealed class InventorySaveSystem : MonoBehaviour
    {
        [SerializeField] private InventorySystem _inventory;
        [SerializeField] private NewItemRegistry _NewItemRegistry;
        [SerializeField] private string _fileName = "inventory_default.json";

        private string FullPath => Path.Combine(Application.persistentDataPath, _fileName);

        public async void SaveAsync()
        {
            if (!Validate()) return;

            var data = new InventorySaveData { Size = _inventory.Size };

            for (int i = 0; i < _inventory.Slots.Count; i++)
            {
                var slot = _inventory.Slots[i];
                if (slot.IsEmpty) continue;

                data.Slots.Add(new InventorySaveData.SlotEntry
                {
                    StorageableId = slot.Item.StorageableId,
                    Quantity = slot.Quantity
                });
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);

            try
            {
                await File.WriteAllTextAsync(FullPath, json);
                Log(LogLevel.Debug, $"Inventory saved to '{FullPath}'.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save inventory: {ex.Message}");
            }
        }

        public async Task LoadAsync()
        {
            if (!Validate()) return;

            if (!File.Exists(FullPath))
            {
                Log(LogLevel.Debug, "No inventory save found — keeping current state.");
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(FullPath);
                var data = JsonUtility.FromJson<InventorySaveData>(json);

                if (data.Size != _inventory.Size)
                    Log(LogLevel.Warning, $"Saved size ({data.Size}) differs from current ({_inventory.Size}) — loading anyway.");

                foreach (var entry in data.Slots)
                {
                    var item = _NewItemRegistry.Resolve(entry.StorageableId);
                    if (item == null)
                    {
                        Log(LogLevel.Warning, $"StorageableId '{entry.StorageableId}' not resolved — skipped.");
                        continue;
                    }

                    _inventory.AddAsMuchAsPossible(item, entry.Quantity);
                }

                Log(LogLevel.Debug, $"Inventory loaded: {data.Slots.Count} entrie(s).");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to load inventory: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes this inventory's save file. Entry point for the README
        /// directive: always delete the save before resizing in testing.
        /// </summary>
        public void DeleteSave()
        {
            if (!File.Exists(FullPath))
            {
                Log(LogLevel.Debug, "No save to delete.");
                return;
            }

            try
            {
                File.Delete(FullPath);
                Log(LogLevel.Debug, $"Save '{FullPath}' deleted.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to delete save: {ex.Message}");
            }
        }

        private bool Validate()
        {
            if (_inventory == null) { Log(LogLevel.Error, "InventorySystem not assigned."); return false; }
            if (_NewItemRegistry == null) { Log(LogLevel.Error, "NewItemRegistry not assigned."); return false; }
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[InventorySaveSystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
