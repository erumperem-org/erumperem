using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.Inventory
{
    /// <summary>
    /// Orchestrates the relationship between the losable and the permanent
    /// inventory: full migration on demand (external specific event — see
    /// <see cref="RequestFullMigration"/>) and auto-refill of the permanent
    /// inventory whenever one of its slots empties out.
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour
    {
        [SerializeField] private InventorySystem _losable;
        [SerializeField] private InventorySystem _permanent;

        private void OnEnable()
        {
            if (_permanent != null)
                _permanent.OnInventoryChanged += HandlePermanentChanged;
        }

        private void OnDisable()
        {
            if (_permanent != null)
                _permanent.OnInventoryChanged -= HandlePermanentChanged;
        }

        /// <summary>
        /// Entry point for the external specific event that authorizes the
        /// losable → permanent migration. Should be called by whatever
        /// system raises that event (outside the inventory's scope).
        /// </summary>
        public void RequestFullMigration()
        {
            if (_losable == null || _permanent == null) return;

            // Snapshot by index — migration priority is sequential slot order in the losable inventory.
            var slots = _losable.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty) continue;

                int moved = _permanent.AddAsMuchAsPossible(slot.Item, slot.Quantity);
                if (moved > 0)
                    _losable.TryRemoveItem(slot.Item, moved);
            }

            Log(LogLevel.Debug, "Losable → permanent migration completed.");
        }

        private void HandlePermanentChanged(InventorySlotChangedEventArgs args)
        {
            if (args.ChangeType != InventoryChangeType.Removed) return;
            RefillEmptySlots();
        }

        private void RefillEmptySlots()
        {
            foreach (int emptySlotIndex in _permanent.GetEmptySlotIndices())
            {
                if (!_losable.TryGetFirstOccupiedSlot(out IIITem item, out int available))
                    break; // losable inventory is empty, nothing left to pull

                int moved = _permanent.TryFillSpecificSlot(emptySlotIndex, item, available);
                if (moved > 0)
                    _losable.TryRemoveItem(item, moved);
            }
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[InventoryManager:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
