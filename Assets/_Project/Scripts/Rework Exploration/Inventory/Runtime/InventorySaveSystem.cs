using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
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
        private const string DefaultFileName = "inventory_default.json";
        private const string LosableFileName = "inventory_losable.json";
        private const string PermanentFileName = "inventory_permanent.json";
        private const int FileWriteMaxAttempts = 5;
        private const int FileWriteRetryDelayMilliseconds = 25;

        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileWriteLocks =
            new(StringComparer.OrdinalIgnoreCase);

        [SerializeField] private InventorySystem _inventory;
        [SerializeField] private NewItemRegistry _NewItemRegistry;
        [SerializeField] private string _fileName = DefaultFileName;

        private string FullPath => Path.Combine(Application.persistentDataPath, ResolveFileName());

        public async void SaveAsync()
        {
            if (!Validate())
            {
                return;
            }

            var data = new InventorySaveData { Size = _inventory.Size };

            for (int slotIndex = 0; slotIndex < _inventory.Slots.Count; slotIndex++)
            {
                var slot = _inventory.Slots[slotIndex];
                if (slot.IsEmpty)
                {
                    continue;
                }

                data.Slots.Add(new InventorySaveData.SlotEntry
                {
                    StorageableId = slot.Item.StorageableId,
                    Quantity = slot.Quantity
                });
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            var destinationPath = FullPath;

            try
            {
                await WriteTextWithRetryAsync(destinationPath, json);
                Log(LogLevel.Debug, $"Inventory saved to '{destinationPath}'.");
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Failed to save inventory: {exception.Message}");
            }
        }

        public async Task LoadAsync()
        {
            if (!Validate())
            {
                return;
            }

            var sourcePath = FullPath;
            if (!File.Exists(sourcePath))
            {
                Log(LogLevel.Debug, "No inventory save found — keeping current state.");
                return;
            }

            try
            {
                string json = await ReadTextWithRetryAsync(sourcePath);
                var data = JsonUtility.FromJson<InventorySaveData>(json);

                if (data.Size != _inventory.Size)
                {
                    Log(LogLevel.Warning,
                        $"Saved size ({data.Size}) differs from current ({_inventory.Size}) — loading anyway.");
                }

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
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Failed to load inventory: {exception.Message}");
            }
        }

        /// <summary>
        /// Deletes this inventory's save file. Entry point for the README
        /// directive: always delete the save before resizing in testing.
        /// </summary>
        public void DeleteSave()
        {
            var savePath = FullPath;
            if (!File.Exists(savePath))
            {
                Log(LogLevel.Debug, "No save to delete.");
                return;
            }

            try
            {
                File.Delete(savePath);
                Log(LogLevel.Debug, $"Save '{savePath}' deleted.");
            }
            catch (Exception exception)
            {
                Log(LogLevel.Error, $"Failed to delete save: {exception.Message}");
            }
        }

        private string ResolveFileName()
        {
            if (!string.Equals(_fileName, DefaultFileName, StringComparison.OrdinalIgnoreCase))
            {
                return _fileName;
            }

            var objectName = gameObject.name;
            if (objectName.IndexOf("Losable", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LosableFileName;
            }

            if (objectName.IndexOf("Permanent", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PermanentFileName;
            }

            return _fileName;
        }

        private static async Task WriteTextWithRetryAsync(string destinationPath, string content)
        {
            var fileWriteLock = GetFileWriteLock(destinationPath);
            await fileWriteLock.WaitAsync();

            try
            {
                for (var attemptIndex = 0; attemptIndex < FileWriteMaxAttempts; attemptIndex++)
                {
                    try
                    {
                        await WriteTextAtomicallyAsync(destinationPath, content);
                        return;
                    }
                    catch (IOException ioException) when (IsSharingViolation(ioException))
                    {
                        if (attemptIndex >= FileWriteMaxAttempts - 1)
                        {
                            throw;
                        }

                        await Task.Delay(FileWriteRetryDelayMilliseconds * (attemptIndex + 1));
                    }
                }
            }
            finally
            {
                fileWriteLock.Release();
            }
        }

        private static async Task<string> ReadTextWithRetryAsync(string sourcePath)
        {
            var fileWriteLock = GetFileWriteLock(sourcePath);
            await fileWriteLock.WaitAsync();

            try
            {
                for (var attemptIndex = 0; attemptIndex < FileWriteMaxAttempts; attemptIndex++)
                {
                    try
                    {
                        return await File.ReadAllTextAsync(sourcePath);
                    }
                    catch (IOException ioException) when (IsSharingViolation(ioException))
                    {
                        if (attemptIndex >= FileWriteMaxAttempts - 1)
                        {
                            throw;
                        }

                        await Task.Delay(FileWriteRetryDelayMilliseconds * (attemptIndex + 1));
                    }
                }

                throw new IOException(
                    $"Failed to read '{sourcePath}' after {FileWriteMaxAttempts} attempt(s).");
            }
            finally
            {
                fileWriteLock.Release();
            }
        }

        private static async Task WriteTextAtomicallyAsync(string destinationPath, string content)
        {
            var directoryPath = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            var temporaryPath = destinationPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, content);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(temporaryPath, destinationPath);
        }

        private static SemaphoreSlim GetFileWriteLock(string filePath)
        {
            return FileWriteLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));
        }

        private static bool IsSharingViolation(IOException ioException)
        {
            const int sharingViolationHResult = unchecked((int)0x80070020);
            return ioException.HResult == sharingViolationHResult
                || ioException.Message.IndexOf("Sharing violation", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool Validate()
        {
            if (_inventory == null)
            {
                Log(LogLevel.Error, "InventorySystem not assigned.");
                return false;
            }

            if (_NewItemRegistry == null)
            {
                Log(LogLevel.Error, "NewItemRegistry not assigned.");
                return false;
            }

            return true;
        }

        private void Log(LogLevel level, string message) =>
            LoggerService.PrintLogMessage(
                level,
                $"[InventorySaveSystem:{gameObject.name}] {message}",
                LogCategory.Inventory);
    }
}
