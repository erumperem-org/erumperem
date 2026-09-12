// ============================================================
// InventorySlotView.cs
// ============================================================
// View de um slot de inventário gerado dinamicamente.
// Recebe os dados via Bind() — não busca nada por conta própria.
// Notifica quem a criou via OnSlotClicked quando o botão for
// pressionado.
//
// Hierarquia esperada no prefab:
//   InventorySlot (este componente + Button)
//   ├── Icon      (UnityEngine.UI.Image)
//   ├── ItemName  (TMP_Text)
//   └── Amount    (TMP_Text)   ← oculto para Unique/SingleSlot
// ============================================================

using System;
using Core.Exploration.Items;
using Erumperem.Combat.Authoring;
using Game.Core.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventorySlotView : MonoBehaviour
{
    [Header("Referências (prefab)")]
    [SerializeField] private Image _icon;
    [SerializeField] private Image _rarityBackground;
    [SerializeField] private TMP_Text _itemName;
    [SerializeField] private TMP_Text _amount;
    [SerializeField] private Button _executeButton;
    [SerializeField] private Button _discardButton;
    public PlayerInventorySystem inventorySystem;
    public GameObject options;
    /// <summary>
    /// Disparado quando o jogador clica no slot.
    /// O assinante recebe o IItem vinculado.
    /// </summary>
    public event Action<IItem> OnSlotClicked;

    private IItem _item;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        this.GetComponent<Button>().onClick.AddListener(ShowOptions);
        _executeButton?.onClick.AddListener(HandleClick);
        _discardButton?.onClick.AddListener(HandleDiscard);
    }

    private void OnDestroy()
    {
        this.GetComponent<Button>().onClick.RemoveListener(ShowOptions);
        _executeButton?.onClick.RemoveListener(HandleClick);
        _discardButton?.onClick.RemoveListener(HandleDiscard);
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Preenche o slot com os dados do item.
    /// Chamado pelo InventoryPanelView ao gerar ou atualizar cada linha.
    /// </summary>
    public void Bind(IItem item, int quantity, PlayerInventorySystem inventorySystem)
    {
        _item = item;
        this.inventorySystem = inventorySystem;
        _itemName.text = item is UnityEngine.Object obj ? obj.name : item.GetType().Name;

        if (_icon != null)
        {
            _icon.sprite = item.Sprite;
            _icon.enabled = item.Sprite != null;
        }

        ApplyRarityBackground(item);

        if (_amount != null)
        {
            bool showAmount = item.storageMode is StorageMode.Stackable or StorageMode.Unlimited;
            _amount.gameObject.SetActive(showAmount);
            if (showAmount)
                _amount.text = quantity > 1 ? $"x{quantity}" : "x1";
        }
    }

    private void ApplyRarityBackground(IItem item)
    {
        if (_rarityBackground == null)
        {
            return;
        }

        if (item is CombatItemAsset combatItem)
        {
            var rgb = CombatItemRarityColors.ResolveBackgroundRgb(combatItem.Rarity);
            _rarityBackground.enabled = true;
            _rarityBackground.color = new Color((float)rgb.Red, (float)rgb.Green, (float)rgb.Blue, 1f);
            return;
        }

        _rarityBackground.enabled = false;
    }

    // ── Privado ───────────────────────────────────────────────────────────

    private void HandleClick()
    {
        _item.ExecuteItemEffect();
        inventorySystem.RemoveItem(_item);
    }

    private void HandleDiscard()
    {
        inventorySystem.RemoveItem(_item);
    }

    private void ShowOptions()
    {
        options.SetActive(true);
    }
}