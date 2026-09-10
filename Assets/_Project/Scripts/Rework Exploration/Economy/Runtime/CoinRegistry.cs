using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Economy.Currency
{
    /// <summary>
    /// ScriptableObject that maps StorageableId → ICoin. Mirrors ItemRegistry,
    /// but focused exclusively on coins — kept separate for SRP (items and
    /// coins have distinct lifecycles and consumers).
    ///
    /// Create via: Assets → Create → Economy → Coin Registry
    /// </summary>
    [CreateAssetMenu(menuName = "Economy/Coin Registry", fileName = "CoinRegistry")]
    public sealed class CoinRegistry : ScriptableObject
    {
        [Tooltip("All coins in the project. Each one's StorageableId must be unique.")]
        [SerializeField] private List<ScriptableObject> _coins = new();

        private Dictionary<string, ICoin> _lookup;

        public IReadOnlyList<ScriptableObject> Coins => _coins;

        private void OnEnable() => BuildLookup();

        public ICoin Resolve(string storageableId)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(storageableId, out var coin) ? coin : null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, ICoin>(_coins.Count);
            foreach (var obj in _coins)
            {
                if (obj is not ICoin coin) continue;

                string id = coin.StorageableId;

                if (string.IsNullOrEmpty(id))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[CoinRegistry] '{obj.name}' has an empty StorageableId — skipped.",
                        LogCategory.Inventory);
                    continue;
                }

                if (_lookup.ContainsKey(id))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[CoinRegistry] Duplicate StorageableId: '{id}' ({obj.name}) — skipped.",
                        LogCategory.Inventory);
                    continue;
                }

                _lookup[id] = coin;
            }
        }
    }
}
