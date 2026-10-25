using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
using Core.Economy.Currency;

namespace Core.Shop
{
    /// <summary>
    /// Botão de item com N ofertas (item X por preço Y). Ao ouvir
    /// RotatingShopEvents.OnAdvance passa para a próxima oferta; ao chegar na
    /// última, permanece nela. A compra em si é delegada ao ItemShopButton do
    /// mesmo GameObject (popup, CanFit e refund continuam iguais) e NÃO avança o estado.
    /// O índice atual é persistido via RotatingShopStateRegistry.
    /// </summary>
    [RequireComponent(typeof(ItemShopButton))]
    public sealed class RotatingItemShopButton : MonoBehaviour
    {
        [Serializable]
        public sealed class Offer
        {
            [Tooltip("Deve implementar IIITem.")]
            [SerializeField] private ScriptableObject _itemAsset;
            [Tooltip("Deve implementar ICoin.")]
            [SerializeField] private ScriptableObject _currencyAsset;
            [SerializeField, Min(0)] private int _unitPrice = 10;

            public ScriptableObject ItemAsset => _itemAsset;
            public ScriptableObject CurrencyAsset => _currencyAsset;
            public IIITem Item => _itemAsset as IIITem;
            public ICoin Currency => _currencyAsset as ICoin;
            public int UnitPrice => _unitPrice;
        }

        [Header("References")]
        [SerializeField] private ItemShopButton _shop;

        [Header("Persistence")]
        [Tooltip("ID ÚNICO e ESTÁVEL deste botão no save. Gerado automaticamente; não duplique entre botões.")]
        [SerializeField] private string _id;

        [Header("Event")]
        [Tooltip("Vazio = reage a qualquer RaiseAdvance. Preenchido = só reage ao canal igual (ou a RaiseAdvance sem canal).")]
        [SerializeField] private string _listenChannel;

        [Header("Offers (ordem = ordem de progressão)")]
        [SerializeField] private List<Offer> _offers = new();

        private int _index;

        public event Action OnOfferChanged;

        public int CurrentIndex => _index;
        public int OfferCount => _offers.Count;
        public bool IsLastOffer => _offers.Count == 0 || _index >= _offers.Count - 1;

        public Offer Current => _offers.Count > 0 ? _offers[Mathf.Clamp(_index, 0, _offers.Count - 1)] : null;
        public IIITem Item => Current?.Item;
        public ICoin Currency => Current?.Currency;
        public int UnitPrice => Current != null ? Current.UnitPrice : 0;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Reset() => _shop = GetComponent<ItemShopButton>();

        private void OnValidate()
        {
            if (!_shop) _shop = GetComponent<ItemShopButton>();
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(_id)) _id = Guid.NewGuid().ToString("N");
#endif
        }

        private void OnEnable()
        {
            RotatingShopEvents.OnAdvance += HandleAdvance;
            RotatingShopStateRegistry.OnStateLoaded += HandleStateLoaded;

            ReadIndexFromRegistry();
            ApplyOffer();
        }

        private void OnDisable()
        {
            RotatingShopEvents.OnAdvance -= HandleAdvance;
            RotatingShopStateRegistry.OnStateLoaded -= HandleStateLoaded;
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Avança para a próxima oferta (no-op se já estiver na última). Retorna true se avançou.</summary>
        public bool Advance()
        {
            if (_offers.Count == 0 || IsLastOffer) return false;

            _index++;
            RotatingShopStateRegistry.Instance?.SetIndex(_id, _index);
            ApplyOffer();
            return true;
        }

        // ── Internals ──────────────────────────────────────────────────────

        private void HandleAdvance(string channel)
        {
            bool matches = string.IsNullOrEmpty(channel)
                           || string.IsNullOrEmpty(_listenChannel)
                           || channel == _listenChannel;
            if (matches) Advance();
        }

        private void HandleStateLoaded()
        {
            ReadIndexFromRegistry();
            ApplyOffer();
        }

        private void ReadIndexFromRegistry()
        {
            int saved = 0;
            var registry = RotatingShopStateRegistry.Instance;
            if (registry != null) registry.TryGetIndex(_id, out saved);

            _index = _offers.Count > 0 ? Mathf.Clamp(saved, 0, _offers.Count - 1) : 0;
        }

        private void ApplyOffer()
        {
            var offer = Current;
            if (offer == null)
            {
                Log(LogLevel.Error, "No offers configured.");
                return;
            }

            if (offer.Item == null || offer.Currency == null)
                Log(LogLevel.Error, $"Offer {_index} has an invalid item or currency asset.");

            _shop.SetOffer(offer.ItemAsset, offer.CurrencyAsset, offer.UnitPrice);
            OnOfferChanged?.Invoke();
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[RotatingItemShopButton:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
