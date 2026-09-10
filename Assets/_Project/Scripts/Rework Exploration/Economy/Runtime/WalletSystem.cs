using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Economy.Currency
{
    /// <summary>
    /// Keeps each ICoin's balance in memory during the session. Does not
    /// persist on its own — see WalletSaveSystem. Naming revised for
    /// semantic coherence: Deposit/TrySpend, replacing the earlier "AddCoins"
    /// (which literally mirrored item vocabulary).
    /// </summary>
    public sealed class WalletSystem : MonoBehaviour
    {
        private readonly Dictionary<ICoin, int> _balances = new();

        /// <summary>Raised whenever a balance changes (deposit or spend).</summary>
        public event Action<ICoin, int> OnBalanceChanged;

        public IReadOnlyDictionary<ICoin, int> Balances => _balances;

        public int GetBalance(ICoin coin) =>
            coin != null && _balances.TryGetValue(coin, out var amount) ? amount : 0;

        /// <summary>Deposits balance for a specific coin.</summary>
        public void Deposit(ICoin coin, int amount)
        {
            if (coin == null || amount <= 0) return;

            _balances.TryGetValue(coin, out var current);
            _balances[coin] = current + amount;

            OnBalanceChanged?.Invoke(coin, _balances[coin]);
        }

        /// <summary>Batch deposit — same input shape used by AddItems on the inventory.</summary>
        public void Deposit(IReadOnlyDictionary<ICoin, int> amounts)
        {
            if (amounts == null) return;
            foreach (var (coin, amount) in amounts)
                Deposit(coin, amount);
        }

        /// <summary>
        /// Attempts to spend <paramref name="amount"/> units of <paramref name="coin"/>.
        /// Returns false without mutating state if the balance is insufficient.
        /// </summary>
        public bool TrySpend(ICoin coin, int amount)
        {
            if (coin == null || amount <= 0) return false;

            if (!_balances.TryGetValue(coin, out var current) || current < amount)
                return false;

            _balances[coin] = current - amount;
            OnBalanceChanged?.Invoke(coin, _balances[coin]);
            return true;
        }

        /// <summary>
        /// Restores in-memory state from data loaded from disk.
        /// Called by WalletSaveSystem during load.
        /// </summary>
        public void RestoreState(IReadOnlyDictionary<ICoin, int> loadedBalances)
        {
            _balances.Clear();
            if (loadedBalances == null) return;

            foreach (var (coin, amount) in loadedBalances)
                _balances[coin] = amount;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[WalletSystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
