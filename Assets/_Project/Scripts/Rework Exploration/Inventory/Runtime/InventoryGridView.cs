using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Spawns one InventorySlotView per slot in the target InventorySystem,
    /// keeps them in sync with InventorySystem.OnInventoryChanged, and
    /// forwards slot clicks to a SelectedItemPanelView.
    /// </summary>
    public sealed class InventoryGridView : MonoBehaviour
    {
        [SerializeField] private InventorySystem _inventory;
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private Transform _slotContainer;
        [SerializeField] private SelectedItemPanelView _detailPanel;

        private readonly List<InventorySlotView> _slotViews = new();

        private void OnEnable()
        {
            if (_inventory == null)
            {
                Log(LogLevel.Error, "InventorySystem not assigned.");
                return;
            }

            _inventory.OnInventoryChanged += HandleInventoryChanged;
            BuildSlots();
            RefreshAll();
        }

        private void OnDisable()
        {
            if (_inventory != null)
                _inventory.OnInventoryChanged -= HandleInventoryChanged;

            ClearSlots();
        }

        /// <summary>Destroys and recreates every slot view to match the inventory's current size.</summary>
        public void BuildSlots()
        {
            ClearSlots();

            for (int i = 0; i < _inventory.Size; i++)
            {
                var slotView = Instantiate(_slotPrefab, _slotContainer);
                slotView.SetSlotIndex(i);
                slotView.OnClicked += HandleSlotClicked;
                _slotViews.Add(slotView);
            }
        }

        /// <summary>Repopulates every slot view's icon/quantity from the inventory's current state.</summary>
        public void RefreshAll()
        {
            for (int i = 0; i < _slotViews.Count && i < _inventory.Slots.Count; i++)
            {
                var slot = _inventory.Slots[i];
                var sprite = slot.IsEmpty ? null : slot.Item.Sprite;
                var quantity = slot.IsEmpty ? 0 : slot.Quantity;
                _slotViews[i].SetContent(sprite, quantity);
            }
        }

        private void ClearSlots()
        {
            foreach (var slotView in _slotViews)
            {
                if (slotView == null) continue;
                slotView.OnClicked -= HandleSlotClicked;
                Destroy(slotView.gameObject);
            }

            _slotViews.Clear();
        }

        private void HandleInventoryChanged(InventorySlotChangedEventArgs args) => RefreshAll();

        private void HandleSlotClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.Slots.Count) return;

            var slot = _inventory.Slots[slotIndex];
            if (slot.IsEmpty)
            {
                _detailPanel?.Hide();
                return;
            }

            _detailPanel?.Show(slot.Item, _inventory);
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[InventoryGridView:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}