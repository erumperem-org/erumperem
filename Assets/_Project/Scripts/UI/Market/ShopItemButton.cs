using Core.Exploration.Items;
using Core.Exploration.Items.Currencies;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Botão de loja. Ao ser clicado, verifica se o jogador possui
/// <see cref="_price"/> unidades de <see cref="_currency"/> e,
/// em caso positivo, debita a moeda e adiciona <see cref="_item"/>
/// ao inventário.
/// </summary>
public sealed class ShopItemButton : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private ScriptableObject _item;

    [Header("Preço")]
    [SerializeField] private AnomalousArtifact _currency;
    [SerializeField, Min(1)] private int _price = 1;
    [Header("Visualização")]
    [SerializeField] private TMPro.TMP_Text _name;
    [SerializeField] private TMPro.TMP_Text _description;
    [SerializeField] private Image _spriteItem;
    [SerializeField] private TMPro.TMP_Text _quanity;
    [SerializeField] private Image _spriteCurrency;

    [Header("Dependências")]
    [SerializeField] private PlayerInventorySystem _inventorySystem;
    [SerializeField] private Button _button;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (_item is IStorageable item)
        {
            _button.onClick.AddListener(OnClick);
            _quanity.text = _price.ToString();
            _name.text = item.GetType().Name.ToString();
            if (item is IItem item1)
            {
                _spriteItem.sprite = item1.Sprite;
            }
            if (item is AnomalousArtifact anomalousArtifact)
            {
                _spriteItem.sprite = anomalousArtifact.Sprite;
            }
            _description.text = item.Description.ToString();
            _spriteCurrency.sprite = _currency.Sprite;
            RefreshInteractable();
        }
    }

    private void OnDestroy()
    {
        _button.onClick.RemoveListener(OnClick);
        _inventorySystem.OnItemAdded -= OnInventoryChanged;
        _inventorySystem.OnItemRemoved -= OnInventoryChanged;
    }

    private void OnEnable()
    {
        _inventorySystem.OnItemAdded += OnInventoryChanged;
        _inventorySystem.OnItemRemoved += OnInventoryChanged;
        RefreshInteractable();
    }

    private void OnDisable()
    {
        _inventorySystem.OnItemAdded -= OnInventoryChanged;
        _inventorySystem.OnItemRemoved -= OnInventoryChanged;
    }

    // ── Compra ────────────────────────────────────────────────────────────

    private void OnClick()
    {
        if (!CanAfford()) return;

        if (_item is IStorageable storageable)
        {
            _inventorySystem.RemoveItems(new() { { _currency, _price } });
            _inventorySystem.AddItems(new() { { storageable, 1 } });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool CanAfford() =>
        _inventorySystem.GetAmount(_currency) >= _price;

    private void OnInventoryChanged(IStorageable item, int _)
    {
        if (item == _currency)
            RefreshInteractable();
    }

    private void RefreshInteractable() =>
        _button.interactable = CanAfford();
}
