using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Economy.Currency;

namespace Core.Shop
{
    /// <summary>
    /// Sells skill tree points following a global price progression: fully
    /// consumes one currency's price range before moving on to the next, in
    /// the order configured in <see cref="_priceTiers"/>. Becomes permanently
    /// unavailable once every tier of every currency has been sold. Has
    /// persistent state (the current tier index).
    /// </summary>
    public sealed class SkillPointShopButton : MonoBehaviour
    {
        [Serializable]
        public sealed class CurrencyPriceRange
        {
            [Tooltip("Must implement ICoin.")]
            [SerializeField] private ScriptableObject _currencyAsset;

            [Tooltip("E.g.: 100, 200, 300")]
            [SerializeField] private int[] _prices = { 100, 200, 300 };

            public ICoin Currency => _currencyAsset as ICoin;
            public IReadOnlyList<int> Prices => _prices;
        }

        [Header("References")]
        [SerializeField] private WalletSystem _wallet;

        [Tooltip("Class that receives the purchase notification. Must implement ISkillPointGrantable.")]
        [SerializeField] private MonoBehaviour _grantableTarget;

        [Header("Price Progression (order = consumption order)")]
        [SerializeField] private List<CurrencyPriceRange> _priceTiers = new();

        // ── Persistent state ─────────────────────────────────────────
        [SerializeField, HideInInspector] private int _globalTierIndex;

        public event Action OnPurchaseSucceeded;
        public event Action OnPurchaseFailed;
        public event Action OnExhausted;

        public bool IsExhausted => _globalTierIndex >= TotalTierCount;
        public int GlobalTierIndex => _globalTierIndex; // exposed for the save system

        private int TotalTierCount
        {
            get
            {
                int total = 0;
                foreach (var tier in _priceTiers) total += tier.Prices.Count;
                return total;
            }
        }

        /// <summary>Used by the save system to restore the index without going through the purchase flow.</summary>
        public void RestoreState(int globalTierIndex) => _globalTierIndex = Mathf.Max(0, globalTierIndex);

        public bool TryGetCurrentTier(out ICoin currency, out int price) => TryResolveCurrentTier(out currency, out price);

        public bool TryPurchase()
        {
            if (IsExhausted)
            {
                OnPurchaseFailed?.Invoke();
                return false;
            }

            if (!TryResolveCurrentTier(out ICoin currency, out int price))
            {
                OnPurchaseFailed?.Invoke();
                return false;
            }

            if (!_wallet.TrySpend(currency, price))
            {
                OnPurchaseFailed?.Invoke();
                return false;
            }

            _globalTierIndex++;

            if (_grantableTarget is ISkillPointGrantable grantable)
                grantable.GrantSkillPoint();
            else
                Log(LogLevel.Error, "_grantableTarget does not implement ISkillPointGrantable.");

            OnPurchaseSucceeded?.Invoke();

            if (IsExhausted)
                OnExhausted?.Invoke();

            return true;
        }

        private bool TryResolveCurrentTier(out ICoin currency, out int price)
        {
            int index = _globalTierIndex;

            foreach (var tier in _priceTiers)
            {
                if (index < tier.Prices.Count)
                {
                    currency = tier.Currency;
                    price = tier.Prices[index];
                    return currency != null;
                }
                index -= tier.Prices.Count;
            }

            currency = null;
            price = 0;
            return false;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[SkillPointShopButton:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
