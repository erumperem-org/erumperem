using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core.Exploration.Items;
using Core.Economy.Currency;

namespace Core.Shop
{
    /// <summary>
    /// Single reusable purchase-confirmation pop-up, Witcher 3 style: a slider
    /// picks the quantity (1..max), Confirm executes the purchase through the
    /// ItemShopButton that requested it, Cancel just closes the panel.
    ///
    /// Wiring: on enable it subscribes to ItemShopButton's static
    /// OnPurchaseRequested event, so a single instance of this component
    /// serves every shop button in the game — including ones instantiated
    /// after this pop-up already exists — with no per-button setup.
    /// </summary>
    public sealed class PurchasePopupController : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Root GameObject to show/hide. If left empty, this.gameObject is used.")]
        [SerializeField] private GameObject _root;

        [Header("Controls")]
        [SerializeField] private Slider _quantitySlider;
        [SerializeField] private TMP_Text _quantityLabel;
        [SerializeField] private TMP_Text _totalCostLabel;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        private ItemShopButton _source;
        private int _unitPrice;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_root == null) _root = gameObject;

            _confirmButton.onClick.AddListener(OnConfirm);
            _cancelButton.onClick.AddListener(Close);
            _quantitySlider.onValueChanged.AddListener(OnSliderChanged);
            ItemShopButton.OnPurchaseRequested += Open;
            _root.SetActive(false);
        }

        private void OnDestroy()
        {
            _confirmButton.onClick.RemoveListener(OnConfirm);
            _cancelButton.onClick.RemoveListener(Close);
            _quantitySlider.onValueChanged.RemoveListener(OnSliderChanged);
            ItemShopButton.OnPurchaseRequested -= Open;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>
        /// Opens the pop-up for a given purchase request. Matches the signature of
        /// ItemShopButton.OnPurchaseRequested, so it can be subscribed directly.
        /// </summary>
        public void Open(ItemShopButton source, IIITem item, ICoin currency, int unitPrice, int maxQuantity, int initialQuantity)
        {
            _source = source;
            _unitPrice = unitPrice;

            _quantitySlider.wholeNumbers = true;
            _quantitySlider.minValue = 1;
            _quantitySlider.maxValue = Mathf.Max(1, maxQuantity);
            _quantitySlider.SetValueWithoutNotify(Mathf.Clamp(initialQuantity, 1, (int)_quantitySlider.maxValue));

            RefreshLabels();
            _root.SetActive(true);
        }

        // ── Internals ──────────────────────────────────────────────────────

        private void OnSliderChanged(float _) => RefreshLabels();

        private void RefreshLabels()
        {
            int quantity = (int)_quantitySlider.value;
            if (_quantityLabel != null) _quantityLabel.text = quantity.ToString();
            if (_totalCostLabel != null) _totalCostLabel.text = (quantity * _unitPrice).ToString();
        }

        private void OnConfirm()
        {
            int quantity = (int)_quantitySlider.value;
            _source?.TryPurchase(quantity);
            Close();
        }

        private void Close()
        {
            _source = null;
            _root.SetActive(false);
        }
    }
}