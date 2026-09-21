using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Shop.UI
{
    /// <summary>
    /// Camada de apresentação para <see cref="SkillLevelUpShopButton"/>.
    /// Não contém regra de negócio: apenas lê o estado do botão de compra
    /// e atualiza a UI, delegando o clique para TryPurchase().
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class SkillLevelUpButtonView : MonoBehaviour
    {
        [Header("Lógica")]
        [SerializeField] private SkillLevelUpShopButton _shopButton;

        [Header("Visualização")]
        [SerializeField] private TMP_Text _priceText;
        [SerializeField] private TMP_Text _levelText;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _button;

        private void Reset() => _button = GetComponent<Button>();

        private void Awake()
        {
            if (!_button) _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
        }

        private void OnEnable()
        {
            _shopButton.OnPurchaseSucceeded += RefreshUI;
            _shopButton.OnPurchaseFailed += RefreshUI;
            _shopButton.OnExhausted += RefreshUI;
            RefreshUI();
        }

        private void OnDisable()
        {
            _shopButton.OnPurchaseSucceeded -= RefreshUI;
            _shopButton.OnPurchaseFailed -= RefreshUI;
            _shopButton.OnExhausted -= RefreshUI;
        }

        private void OnDestroy() => _button.onClick.RemoveListener(OnClick);

        private void OnClick() => _shopButton.TryPurchase();

        private void RefreshUI()
        {
            if (_shopButton.IsExhausted)
            {
                _button.interactable = false;

                if (_priceText)
                    _priceText.text = "MAX";

                if (_levelText)
                    _levelText.text = $"Level {_shopButton.MaxLevel}/{_shopButton.MaxLevel}";

                if (_icon)
                {
                    _icon.sprite = null;
                    _icon.enabled = false;
                }

                return;
            }

            _shopButton.TryGetCurrentTier(out var currency, out var price);

            if (_icon)
            {
                _icon.sprite = currency.Sprite;
                _icon.enabled = _icon.sprite != null;
            }

            if (_priceText)
                _priceText.text = price.ToString();

            if (_levelText)
                _levelText.text = $"Level {_shopButton.CurrentLevel}/{_shopButton.MaxLevel}";

            _button.interactable = true;
        }
    }

    /// <summary>
    /// Placeholder: ajuste/remova conforme a interface ICoin real do projeto
    /// já exponha (ou não) um ícone de moeda.
    /// </summary>
    public interface ICoinIcon
    {
        Sprite Icon { get; }
    }
}
