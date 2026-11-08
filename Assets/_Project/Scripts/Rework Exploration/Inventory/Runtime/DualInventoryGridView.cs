using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Mostra dois InventorySystem lado a lado, cada um no seu grid.
    /// Clicar num slot de um lado abre o TransferItemPanelView com o OUTRO
    /// inventário como destino da transferência.
    /// </summary>
    public sealed class DualInventoryGridView : MonoBehaviour
    {
        [Serializable]
        private sealed class Section
        {
            public InventorySystem inventory;
            public Transform container;

            [NonSerialized] private List<InventorySlotView> _views;
            public List<InventorySlotView> Views => _views ??= new List<InventorySlotView>();
        }

        [SerializeField] private Section _left = new();
        [SerializeField] private Section _right = new();
        [Tooltip("Prefab de slot compartilhado pelos dois grids (deve ter InventorySlotHoverTooltip).")]
        [SerializeField] private InventorySlotView _slotPrefab;
        [SerializeField] private TransferItemPanelView _actionPanel;

        private void OnEnable()
        {
            //if (!Validate()) return;

            _left.inventory.OnInventoryChanged += HandleLeftChanged;
            _right.inventory.OnInventoryChanged += HandleRightChanged;

            BuildSlots(_left);
            BuildSlots(_right);
            RefreshAll();
        }

        private void OnDisable()
        {
            if (_left.inventory != null) _left.inventory.OnInventoryChanged -= HandleLeftChanged;
            if (_right.inventory != null) _right.inventory.OnInventoryChanged -= HandleRightChanged;

            ClearSlots(_left);
            ClearSlots(_right);

            if (_actionPanel != null) _actionPanel.Hide();
        }

        public void RefreshAll()
        {
            Refresh(_left);
            Refresh(_right);
        }

        private bool Validate()
        {
            if (_left.inventory == null || _right.inventory == null)
            {
                Log(LogLevel.Error, "Left/Right InventorySystem not assigned.");
                return false;
            }

            if (_left.container == null || _right.container == null || _slotPrefab == null)
            {
                Log(LogLevel.Error, "Slot containers or slot prefab not assigned.");
                return false;
            }

            if (ReferenceEquals(_left.inventory, _right.inventory))
                Log(LogLevel.Warning, "Left and Right point to the same InventorySystem — transfers will be ignored.");

            return true;
        }

        private void BuildSlots(Section section)
        {
            ClearSlots(section);

            for (int i = 0; i < section.inventory.Size; i++)
            {
                var view = Instantiate(_slotPrefab, section.container);
                view.SetSlotIndex(i);
                view.SetOwner(section.inventory);
                view.OnClicked += slotIndex => HandleSlotClicked(section, slotIndex);
                section.Views.Add(view);
            }
        }

        private static void ClearSlots(Section section)
        {
            foreach (var view in section.Views)
                if (view != null) Destroy(view.gameObject);

            section.Views.Clear();
        }

        private static void Refresh(Section section)
        {
            var slots = section.inventory.Slots;

            for (int i = 0; i < section.Views.Count && i < slots.Count; i++)
            {
                var slot = slots[i];
                var sprite = slot.IsEmpty ? null : slot.Item.Sprite;
                var quantity = slot.IsEmpty ? 0 : slot.Quantity;
                section.Views[i].SetContent(sprite, quantity);
            }
        }

        private void HandleLeftChanged(InventorySlotChangedEventArgs _) => Refresh(_left);
        private void HandleRightChanged(InventorySlotChangedEventArgs _) => Refresh(_right);

        private void HandleSlotClicked(Section source, int slotIndex)
        {
            if (_actionPanel == null) return;

            var target = ReferenceEquals(source, _left) ? _right : _left;

            if (!source.inventory.TryGetSlot(slotIndex, out var item, out _))
            {
                _actionPanel.Hide();
                return;
            }

            _actionPanel.Show(item, source.inventory, slotIndex, target.inventory);
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[DualInventoryGridView:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}