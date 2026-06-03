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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventorySlotView : MonoBehaviour
{
    [Header("Referências (prefab)")]
    [SerializeField] private Image    _icon;
    [SerializeField] private TMP_Text _itemName;
    [SerializeField] private TMP_Text _amount;
    [SerializeField] private Button   _button;

    /// <summary>
    /// Disparado quando o jogador clica no slot.
    /// O assinante recebe o IItem vinculado.
    /// </summary>
    public event Action<IItem> OnSlotClicked;

    private IItem _item;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        _button?.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        _button?.onClick.RemoveListener(HandleClick);
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Preenche o slot com os dados do item.
    /// Chamado pelo InventoryPanelView ao gerar ou atualizar cada linha.
    /// </summary>
    public void Bind(IItem item, int quantity)
    {
        _item = item;

        _itemName.text = item is UnityEngine.Object obj ? obj.name : item.GetType().Name;

        if (_icon != null)
        {
            _icon.sprite  = item.Sprite;
            _icon.enabled = item.Sprite != null;
        }

        if (_amount != null)
        {
            bool showAmount = item.storageMode is StorageMode.Stackable or StorageMode.Unlimited;
            _amount.gameObject.SetActive(showAmount);
            if (showAmount)
                _amount.text = quantity > 1 ? $"x{quantity}" : "x1";
        }
    }

    // ── Privado ───────────────────────────────────────────────────────────

    private void HandleClick() => OnSlotClicked?.Invoke(_item);
}