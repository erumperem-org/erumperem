using System;
using Services.DebugUtilities;
using UnityEngine;
using UnityEngine.UI;
using Core.Exploration.Items;
using Core.Economy.Currency;
using Core.Inventory;

namespace Core.Shop
{
    /// <summary>
    /// Sells a specific item into the permanent inventory. Stateless — no
    /// persistence of its own: every purchase is validated and executed
    /// atomically at click time.
    ///
    /// Clicking the button no longer buys immediately: it computes how many
    /// units the player could currently afford AND fit, and raises
    /// <see cref="OnPurchaseRequested"/> so a pop-up (e.g. PurchasePopupController)
    /// can let the player pick the final quantity via a slider before calling
    /// <see cref="TryPurchase"/> itself.
    /// </summary>
    public sealed class ItemShopButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WalletSystem _wallet;
        [SerializeField] private InventorySystem _permanentInventory;
        [SerializeField] private Button _button;

        [Header("Offer")]
        [Tooltip("Must implement IIITem.")]
        [SerializeField] private ScriptableObject _itemAsset;
        [Tooltip("Must implement ICoin.")]
        [SerializeField] private ScriptableObject _currencyAsset;
        [SerializeField] private int _unitPrice = 10;

        [Header("Purchase")]
        [Tooltip("Suggested starting quantity when the pop-up opens (clamped to what's actually purchasable).")]
        [SerializeField, Min(1)] private int _quantity = 1;

        /// <summary>
        /// Raised when ANY ItemShopButton in the game is clicked and its offer is
        /// currently purchasable at least once. Static on purpose: a single pop-up
        /// controller subscribes once and serves every shop button — existing ones
        /// and any instantiated later — without needing a scene lookup.
        /// Parameters: (source, item, currency, unitPrice, maxQuantity, initialQuantity).
        /// </summary>
        public static event Action<ItemShopButton, IIITem, ICoin, int, int, int> OnPurchaseRequested;

        public event Action<IIITem, int> OnPurchaseSucceeded;
        public event Action OnPurchaseFailed;

        public IIITem Item => _itemAsset as IIITem;
        public ICoin Currency => _currencyAsset as ICoin;
        public int UnitPrice => _unitPrice;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake() => _button.onClick.AddListener(OnClick);

        private void OnDestroy() => _button.onClick.RemoveListener(OnClick);

        private void OnClick()
        {
            if (!Validate(1, out var item, out var currency))
            {
                OnPurchaseFailed?.Invoke();
                return;
            }

            int max = GetMaxPurchasable();
            if (max <= 0)
            {
                OnPurchaseFailed?.Invoke();
                return;
            }

            int initial = Mathf.Clamp(_quantity, 1, max);
            OnPurchaseRequested?.Invoke(this, item, currency, _unitPrice, max, initial);
        }

        /// <summary>
        /// Read-only affordability check for a specific quantity, exposed so the UI
        /// layer can update a button's interactive state without needing its own
        /// reference to WalletSystem.
        /// </summary>
        public bool CanAfford(int quantity) =>
            quantity > 0 && _wallet != null && Currency != null && _wallet.GetBalance(Currency) >= _unitPrice * quantity;

        /// <summary>
        /// Largest quantity currently purchasable, i.e. the smaller of
        /// "how many the wallet can afford" and "how many the inventory can fit".
        /// Returns 0 if the offer isn't purchasable at all right now.
        /// </summary>
        public int GetMaxPurchasable()
        {
            if (!Validate(1, out var item, out var currency)) return 0;

            int affordable = _unitPrice > 0
                ? currency != null ? _wallet.GetBalance(currency) / _unitPrice : 0
                : int.MaxValue;

            if (affordable <= 0) return 0;

            int fittable = GetMaxFittable(item, affordable);
            return Mathf.Max(0, Mathf.Min(affordable, fittable));
        }

        /// <summary>
        /// InventorySystem only exposes a boolean CanFit(item, quantity) check, so we
        /// binary-search the largest quantity that still fits, capped at <paramref name="upperBound"/>
        /// (there's no point searching past what the wallet can afford anyway).
        /// If InventorySystem grows a direct "how many can fit" query, prefer that
        /// over this search.
        /// </summary>
        private int GetMaxFittable(IIITem item, int upperBound)
        {
            if (upperBound <= 0) return 0;
            if (!_permanentInventory.CanFit(item, 1)) return 0;

            int lo = 1, hi = upperBound;
            while (lo < hi)
            {
                int mid = lo + (hi - lo + 1) / 2;
                if (_permanentInventory.CanFit(item, mid)) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>
        /// Attempts to buy <paramref name="quantity"/> units. All-or-nothing:
        /// only executes if there is enough currency AND enough inventory
        /// space for the entire requested quantity. This is what the pop-up's
        /// confirm button should call once the player picks a quantity on the slider.
        /// </summary>
        public bool TryPurchase(int quantity)
        {
            if (!Validate(quantity, out var item, out var currency))
            {
                OnPurchaseFailed?.Invoke();
                return false;
            }

            int totalCost = _unitPrice * quantity;

            if (_wallet.GetBalance(currency) < totalCost || !_permanentInventory.CanFit(item, quantity))
            {
                OnPurchaseFailed?.Invoke();
                return false;
            }

            if (!_wallet.TrySpend(currency, totalCost))
            {
                OnPurchaseFailed?.Invoke();
                return false;
            }

            int added = _permanentInventory.AddAsMuchAsPossible(item, quantity);

            if (added < quantity)
            {
                // Safety net: CanFit already guaranteed this, but if something
                // changed between the check and the execution, refund the shortfall.
                _wallet.Deposit(currency, _unitPrice * (quantity - added));
                Log(LogLevel.Warning, "Mismatch between CanFit and AddAsMuchAsPossible — partial refund applied.");
            }

            OnPurchaseSucceeded?.Invoke(item, added);
            return added == quantity;
        }

        private bool Validate(int quantity, out IIITem item, out ICoin currency)
        {
            item = Item;
            currency = Currency;

            if (quantity <= 0) return false;
            if (item == null) { Log(LogLevel.Error, "Invalid or unassigned offer item."); return false; }
            if (currency == null) { Log(LogLevel.Error, "Invalid or unassigned offer currency."); return false; }
            if (_wallet == null || _permanentInventory == null) { Log(LogLevel.Error, "References not assigned."); return false; }

            return true;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ItemShopButton:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}