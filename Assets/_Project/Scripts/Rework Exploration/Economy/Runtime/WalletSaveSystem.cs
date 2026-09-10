using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Economy.Currency
{
    /// <summary>
    /// Persists and restores a WalletSystem's state to/from its own JSON file,
    /// resolving StorageableId ↔ ICoin via CoinRegistry.
    /// </summary>
    public sealed class WalletSaveSystem : MonoBehaviour
    {
        [SerializeField] private WalletSystem _wallet;
        [SerializeField] private CoinRegistry _coinRegistry;

        [Tooltip("Relative path inside the game's save directory.")]
        [SerializeField] private string _fileName = "wallet.json";

        private string FullPath => Path.Combine(Application.persistentDataPath, _fileName);

        public async void SaveAsync()
        {
            if (!Validate()) return;

            var data = new WalletSaveData();
            foreach (var (coin, amount) in _wallet.Balances)
            {
                data.Coins.Add(new WalletSaveData.CoinEntry
                {
                    StorageableId = coin.StorageableId,
                    Amount = amount
                });
            }

            string json = JsonUtility.ToJson(data, prettyPrint: true);

            try
            {
                await File.WriteAllTextAsync(FullPath, json);
                Log(LogLevel.Debug, $"Wallet saved to '{FullPath}'.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save wallet: {ex.Message}");
            }
        }

        public async Task LoadAsync()
        {
            if (!Validate()) return;

            if (!File.Exists(FullPath))
            {
                Log(LogLevel.Debug, "No wallet save found — starting empty.");
                return;
            }

            try
            {
                string json = await File.ReadAllTextAsync(FullPath);
                var data = JsonUtility.FromJson<WalletSaveData>(json);

                var resolved = new Dictionary<ICoin, int>();
                foreach (var entry in data.Coins)
                {
                    var coin = _coinRegistry.Resolve(entry.StorageableId);
                    if (coin == null)
                    {
                        Log(LogLevel.Warning, $"StorageableId '{entry.StorageableId}' not resolved in CoinRegistry — skipped.");
                        continue;
                    }
                    resolved[coin] = entry.Amount;
                }

                _wallet.RestoreState(resolved);
                Log(LogLevel.Debug, $"Wallet loaded: {resolved.Count} coin type(s).");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to load wallet: {ex.Message}");
            }
        }

        /// <summary>Deletes this wallet's save file.</summary>
        public void DeleteSave()
        {
            if (!File.Exists(FullPath))
            {
                Log(LogLevel.Debug, "No save file to delete.");
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
            if (_wallet == null) { Log(LogLevel.Error, "WalletSystem not assigned."); return false; }
            if (_coinRegistry == null) { Log(LogLevel.Error, "CoinRegistry not assigned."); return false; }
            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[WalletSaveSystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
