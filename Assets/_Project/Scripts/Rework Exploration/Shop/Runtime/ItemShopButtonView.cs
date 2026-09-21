using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core.Exploration.Items;

namespace Core.Shop.UI
{
    /// <summary>
    /// Camada de apresentação para <see cref="ItemShopButton"/>.
    /// Não contém regra de negócio: exibe a oferta (nome, sprite do item,
    /// preço, sprite da moeda) a partir das interfaces IIITem/ICoin e
    /// delega o clique para TryPurchase().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ItemShopButtonView : MonoBehaviour
    {
        [Header("Lógica")]
        [SerializeField] private ItemShopButton _shopButton;

        [Header("Visualização")]
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private Image _itemSprite;
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private Image _currencySprite;

        private void Awake()
        {
            SetupStaticVisuals();
        }

        // ── Visualização ──────────────────────────────────────────────────

        private void SetupStaticVisuals()
        {
            var item = _shopButton.Item;
            var currency = _shopButton.Currency;

            if (_nameText) _nameText.text = item != null ? item.DisplayName : string.Empty;
            if (_priceText) _priceText.text = _shopButton.UnitPrice.ToString();

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