using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Visual representation of a single inventory slot: an icon (item
    /// sprite) and a quantity label, filled by InventoryGridView. Raises
    /// OnClicked with its own slot index when clicked — it holds no
    /// reference to InventorySystem or the item itself, it only displays
    /// what it's told to display.
    /// </summary>
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private Button _button;

        public int SlotIndex { get; private set; }
        public event Action<int> OnClicked;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClicked);
        }

        public void SetSlotIndex(int index) => SlotIndex = index;

        /// <summary>Displays the given icon/quantity, or renders as empty when icon is null.</summary>
        public void SetContent(Sprite icon, int quantity)
        {
            bool hasContent = icon != null && quantity > 0;

            if (_icon != null)
            {
                _icon.sprite = icon;
                _icon.enabled = hasContent;
            }

            if (_quantityText != null)
                _quantityText.text = hasContent ? quantity.ToString() : string.Empty;
        }

        private void HandleClicked() => OnClicked?.Invoke(SlotIndex);
    }
}