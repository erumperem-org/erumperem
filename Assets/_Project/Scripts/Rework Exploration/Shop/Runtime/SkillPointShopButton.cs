using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Economy.Currency;
using Erumperem.Progression;

namespace Core.Shop
{
    /// <summary>
    /// Vende níveis de skill compartilhado seguindo uma progressão de preço
    /// global: consome completamente a faixa de preços de uma moeda antes de
    /// passar para a próxima, na ordem configurada em <see cref="_priceTiers"/>.
    /// Fica permanentemente indisponível quando todos os tiers de todas as
    /// moedas forem vendidos. Possui estado persistente (o índice de tier
    /// atual), exposto para ser salvo/restaurado por um sistema externo.
    /// </summary>
    public sealed class SkillLevelUpShopButton : MonoBehaviour
    {
        [Serializable]
        public sealed class CurrencyPriceRange
        {
            [Tooltip("Deve implementar ICoin.")]
            [SerializeField] private ScriptableObject _currencyAsset;

            [Tooltip("Ex.: 500, 1000, 1500, 2000")]
            [SerializeField] private int[] _prices = { 500, 1000, 1500, 2000 };

            public ICoin Currency => _currencyAsset as ICoin;
            public IReadOnlyList<int> Prices => _prices;
        }

        [Header("References")]
        [SerializeField] private WalletSystem _wallet;

        [SerializeField] private PlayerProgressionService _playerProgression;

        [Header("Price Progression (ordem = ordem de consumo)")]
        [Tooltip("Cada entrada corresponde a um tier de moeda (ex.: Rare, Epic, Legendary).")]
        [SerializeField] private List<CurrencyPriceRange> _priceTiers = new();

        [Header("Progressão")]
        [Tooltip("Multiplicador de pontos concedidos por nível comprado (equivale ao antigo 'pointsTogive').")]
        [SerializeField] private int _pointsPerLevel = 1;

        // ── Persistent state ─────────────────────────────────────────
        [SerializeField, HideInInspector] private int _globalTierIndex;

        public event Action OnPurchaseSucceeded;
        public event Action OnPurchaseFailed;
        public event Action OnExhausted;

        public bool IsExhausted => _globalTierIndex >= TotalTierCount;
        public int GlobalTierIndex => _globalTierIndex; // exposto para o sistema de save
        public int CurrentLevel => _globalTierIndex;    // mantém a semântica antiga (nível == índice global)
        public int MaxLevel => TotalTierCount;

        private int TotalTierCount
        {
            get
            {
                int total = 0;
                foreach (var tier in _priceTiers) total += tier.Prices.Count;
                return total;
            }
        }

        /// <summary>Usado pelo sistema de save para restaurar o índice sem passar pelo fluxo de compra.</summary>
        public void RestoreState(int globalTierIndex) =>
            _globalTierIndex = Mathf.Clamp(globalTierIndex, 0, TotalTierCount);

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

            GrantLevelUp(_globalTierIndex);

            OnPurchaseSucceeded?.Invoke();

            if (IsExhausted)
                OnExhausted?.Invoke();

            return true;
        }

        private void GrantLevelUp(int level)
        {
            _playerProgression.TrySetSharedSkillLevel(level * _pointsPerLevel);
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
            LoggerService.PrintLogMessage(level, $"[SkillLevelUpShopButton:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}