using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Core.Economy.Currency.UI
{
    /// <summary>
    /// Binds a single displayed currency to its icon and amount text.
    /// One entry per coin the wallet view should render.
    /// </summary>
    [System.Serializable]
    public sealed class CoinDisplaySlot
    {
        [Tooltip("Must implement ICoin.")]
        [SerializeField] private ScriptableObject _coinAsset;

        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _amountText;

        public ICoin Coin => _coinAsset as ICoin;
        public Image Icon => _icon;
        public TMP_Text AmountText => _amountText;

        public bool IsValid => Coin != null && _icon != null && _amountText != null;
    }
}