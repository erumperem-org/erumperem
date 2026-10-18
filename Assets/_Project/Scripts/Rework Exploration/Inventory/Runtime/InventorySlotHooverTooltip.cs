using UnityEngine;
using UnityEngine.EventSystems;
using Core.Exploration.Items;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Anexar a todo slot. Em hover, se houver item no slot, mostra o
    /// ItemTooltipView compartilhado. Enquanto o ponteiro está sobre o slot,
    /// acompanha mudanças (item trocado, quantidade alterada, slot esvaziado).
    /// </summary>
    [RequireComponent(typeof(InventorySlotView))]
    public sealed class InventorySlotHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Atraso (segundos, tempo não escalado) antes de o popup aparecer.")]
        [SerializeField] private float _showDelay = 0.15f;

        private InventorySlotView _slotView;
        private RectTransform _rect;

        private bool _hovering;
        private bool _shown;
        private float _enterTime;
        private IIITem _lastItem;
        private int _lastQuantity;

        private void Awake()
        {
            _slotView = GetComponent<InventorySlotView>();
            _rect = (RectTransform)transform;
        }

        private void OnDisable() => StopHover();

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            _enterTime = Time.unscaledTime;
        }

        public void OnPointerExit(PointerEventData eventData) => StopHover();

        private void Update()
        {
            if (!_hovering) return;

            var tooltip = ItemTooltipView.Instance;
            var owner = _slotView.Owner;
            if (tooltip == null || owner == null) return;

            if (!owner.TryGetSlot(_slotView.SlotIndex, out var item, out int quantity))
            {
                // Slot vazio (ou esvaziou durante o hover).
                if (_shown) { tooltip.Hide(); _shown = false; }
                _lastItem = null;
                return;
            }

            if (!_shown && Time.unscaledTime - _enterTime < _showDelay) return;

            bool changed = !_shown
                           || quantity != _lastQuantity
                           || !Equals(item?.StorageableId, _lastItem?.StorageableId);
            if (!changed) return;

            tooltip.Show(item, quantity, _rect);
            _shown = true;
            _lastItem = item;
            _lastQuantity = quantity;
        }

        private void StopHover()
        {
            if (_shown)
            {
                var tooltip = ItemTooltipView.Instance;
                if (tooltip != null) tooltip.Hide();
            }

            _hovering = false;
            _shown = false;
            _lastItem = null;
            _lastQuantity = 0;
        }
    }
}