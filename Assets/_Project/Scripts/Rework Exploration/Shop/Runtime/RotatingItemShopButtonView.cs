using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Shop.UI
{
    /// <summary>
    /// Apresentação para RotatingItemShopButton. Substitui o ItemShopButtonView
    /// (que só lê a oferta uma vez); este atualiza a cada troca de oferta.
    /// </summary>
    public sealed class RotatingItemShopButtonView : MonoBehaviour
    {
        [Header("Lógica")]
        [SerializeField] private RotatingItemShopButton _rotatingButton;

        [Header("Visualização")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _itemSprite;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private Image _currencySprite;

        private void OnEnable()
        {
            _rotatingButton.OnOfferChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => _rotatingButton.OnOfferChanged -= Refresh;

        private void Refresh()
        {
            var item = _rotatingButton.Item;
            var currency = _rotatingButton.Currency;

            if (_nameText) _nameText.text = item != null ? item.DisplayName : string.Empty;
            if (_priceText) _priceText.text = _rotatingButton.UnitPrice.ToString();

            if (_itemSprite)
            {
                _itemSprite.sprite = item?.Sprite;
                _itemSprite.enabled = item?.Sprite != null;
            }

            if (_currencySprite)
            {
                _currencySprite.sprite = currency?.Sprite;
                _currencySprite.enabled = currency?.Sprite != null;
            }
        }
    }
}
