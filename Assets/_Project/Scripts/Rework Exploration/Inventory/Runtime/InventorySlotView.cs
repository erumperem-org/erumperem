using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Visual representation of a single inventory slot: an icon (item
    /// sprite) and a quantity label, filled by InventoryGridView.
    ///
    /// It still only DISPLAYS content — it never reads or mutates the
    /// inventory. The only thing it knows besides its index is WHICH
    /// InventorySystem it belongs to (<see cref="Owner"/>), so a click can be
    /// reported as "slot N of inventory X" (used by the item-use flow to
    /// remove the item from the exact inventory it came from).
    ///
    /// InventoryGridView must call <see cref="SetOwner"/> right after
    /// <see cref="SetSlotIndex"/>. If it doesn't, Owner falls back to the
    /// first InventorySystem found in a parent.
    /// </summary>
    public sealed class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _quantityText;
        [SerializeField] private Button _button;

        private InventorySystem _owner;

        public int SlotIndex { get; private set; }

        /// <summary>The inventory this slot displays (explicit via SetOwner, else a parent lookup).</summary>
        public InventorySystem Owner
        {
            get
            {
                if (_owner == null)
                    _owner = GetComponentInParent<InventorySystem>();
                return _owner;
            }
        }

        /// <summary>Raised with the slot index when this specific view is clicked (unchanged contract).</summary>
        public event Action<int> OnClicked;

        /// <summary>
        /// Raised for a click on ANY slot view, carrying the view itself so
        /// listeners can read Owner + SlotIndex. Static so a single listener
        /// (ItemUseFlowController) needs no reference to every slot.
        /// </summary>
        public static event Action<InventorySlotView> AnySlotClicked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AnySlotClicked = null;

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

        public void SetOwner(InventorySystem owner) => _owner = owner;

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

        private void HandleClicked()
        {
            OnClicked?.Invoke(SlotIndex);
            AnySlotClicked?.Invoke(this);
        }
    }
}
