using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
using Core.Storage;

namespace Core.Chests
{
    /// <summary>
    /// Simple chest: receives an already-resolved copy of content (concrete
    /// coins/items, no chance left to evaluate) from an external system, and
    /// grants everything at once when interacted with. Does not use
    /// RewardGeneratorService and does not route the content to
    /// Wallet/Inventory — it only exposes the rarity of its best item and its
    /// open/closed state, for the view to react to.
    /// </summary>
    public sealed class Chest : MonoBehaviour
    {
        private Dictionary<InterfaceStorageable, int> _contents = new();
        private bool _consumed;

        /// <summary>Raised on interaction, with the rarity of the best IIITem in the content.</summary>
        public event Action<ItemRarity> OnBestItemRarityRevealed;

        /// <summary>
        /// Raised whenever the chest transitions between Closed and Open —
        /// purely a view-facing signal (e.g. to trigger an animation).
        /// </summary>
        public event Action<ChestState> OnChestStateChanged;

        public bool IsConsumed => _consumed;
        public bool HasContent => !_consumed && _contents.Count > 0;

        /// <summary>
        /// Read-only snapshot of the current content, for debugging/editor tooling.
        /// Never mutate through this — Chest owns its internal dictionary.
        /// </summary>
        public IReadOnlyDictionary<InterfaceStorageable, int> DebugContents => _contents;

        /// <summary>
        /// Assigns a NEW copy of content to this chest, replacing any
        /// previous unconsumed content. Always clones the given data — the
        /// caller must never assume the chest references the original dictionary.
        /// Also transitions the chest back to Closed, since receiving new
        /// loot always means the chest can be opened again.
        /// </summary>
        public void AssignLoot(IReadOnlyDictionary<InterfaceStorageable, int> loot)
        {
            _contents = loot != null
                ? new Dictionary<InterfaceStorageable, int>(loot)
                : new Dictionary<InterfaceStorageable, int>();

            _consumed = false;

            OnChestStateChanged?.Invoke(ChestState.Closed);
            ItemRarity? bestRarity = FindBestItemRarity();
            if (bestRarity.HasValue)
            {
                OnBestItemRarityRevealed?.Invoke(bestRarity.Value);
            }
            else
            {
                OnBestItemRarityRevealed?.Invoke(ItemRarity.Common);
            }
        }

        /// <summary>
        /// Executes the interaction: grants the content in full (no draw),
        /// consumes the content (cannot be reopened), exposes the rarity of
        /// the best item found, and signals the Open state transition.
        /// </summary>
        public void Interact()
        {
            if (_consumed)
            {
                Log(LogLevel.Debug, "Interaction ignored — chest already consumed.");
                return;
            }

            if (_contents.Count == 0)
            {
                Log(LogLevel.Warning, "Chest interacted with while empty (no content assigned).");
                _consumed = true;
                OnChestStateChanged?.Invoke(ChestState.Open);
                return;
            }

            ItemRarity? bestRarity = FindBestItemRarity();

            _consumed = true;
            _contents.Clear();

            OnChestStateChanged?.Invoke(ChestState.Open);

            if (bestRarity.HasValue)
            {
                OnBestItemRarityRevealed?.Invoke(bestRarity.Value);
            }
            else
            {
                // README directive: chest loot tables should not be coin-only.
                // If this happens, there is no item to report a rarity for.
                Log(LogLevel.Warning, "Chest content contained no IIITem — " +
                                       "check the LootTable (README directive: avoid coin-only tables).");
            }
        }

        private ItemRarity? FindBestItemRarity()
        {
            ItemRarity? best = null;

            foreach (var storageable in _contents.Keys)
            {
                if (storageable is not IIITem item) continue;
                if (best == null || item.Rarity > best.Value)
                    best = item.Rarity;
            }

            return best;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[Chest:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}