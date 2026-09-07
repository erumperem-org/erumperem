using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Economy.Currency.UI
{
    /// <summary>
    /// Displays a fixed set of currencies as icon + amount pairs. Reacts to
    /// WalletSystem.OnBalanceChanged to keep the displayed amount in sync,
    /// without polling every frame. Coins not included in _displaySlots are
    /// simply never rendered by this view.
    /// </summary>
    public sealed class WalletView : MonoBehaviour
    {
        [SerializeField] private WalletSystem _wallet;
        [SerializeField] private List<CoinDisplaySlot> _displaySlots = new();

        private void OnEnable()
        {
            if (_wallet == null)
            {
                Log(LogLevel.Error, "WalletSystem not assigned.");
                return;
            }

            _wallet.OnBalanceChanged += HandleBalanceChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            if (_wallet != null)
                _wallet.OnBalanceChanged -= HandleBalanceChanged;
        }

        /// <summary>Repopulates every slot's icon and amount from the wallet's current state.</summary>
        public void RefreshAll()
        {
            foreach (var slot in _displaySlots)
                RefreshSlot(slot);
        }

        private void HandleBalanceChanged(ICoin coin, int newBalance)
        {
            foreach (var slot in _displaySlots)
            {
                if (slot.Coin == null || slot.Coin.StorageableId != coin.StorageableId) continue;
                ApplyAmount(slot, newBalance);
            }
        }

        private void RefreshSlot(CoinDisplaySlot slot)
        {
            if (!slot.IsValid)
            {
                Log(LogLevel.Warning, "Skipping invalid display slot (missing coin/icon/text reference).");
                return;
            }

            slot.Icon.sprite = slot.Coin.Sprite;
            ApplyAmount(slot, _wallet.GetBalance(slot.Coin));
        }

        private void ApplyAmount(CoinDisplaySlot slot, int amount) =>
            slot.AmountText.text = amount.ToString();

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[WalletView:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}