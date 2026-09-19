using System;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
using Core.Economy.Currency;
using Core.Inventory;

namespace Core.Shop
{
    /// <summary>
    /// Sells a specific item into the permanent inventory, in a
    /// buyer-chosen quantity. Stateless — no persistence of its own:
    /// every purchase is validated and executed atomically at click time.
    /// </summary>
    public sealed class ItemShopButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WalletSystem _wallet;
        [SerializeField] private InventorySystem _permanentInventory;

        [Header("Offer")]
        [Tooltip("Must implement IIITem.")]
        [SerializeField] private ScriptableObject _itemAsset;
        [Tooltip("Must implement ICoin.")]
        [SerializeField] private ScriptableObject _currencyAsset;
        [SerializeField] private int _unitPrice = 10;

        public event Action<IIITem, int> OnPurchaseSucceeded;
        public event Action OnPurchaseFailed;

        public IIITem Item => _itemAsset as IIITem;
        public ICoin Currency => _currencyAsset as ICoin;
        public int UnitPrice => _unitPrice;

        /// <summary>
        /// Attempts to buy <paramref name="quantity"/> units. All-or-nothing:
        /// only executes if there is enough currency AND enough inventory
        /// space for the entire requested quantity.
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
