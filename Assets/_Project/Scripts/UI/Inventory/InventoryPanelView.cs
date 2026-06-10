// ============================================================
// InventoryPanelView.cs
// ============================================================
// Painel de inventário dinâmico: popula os slots no OnEnable
// com o estado atual do PlayerInventorySystem e se mantém
// atualizado via eventos OnItemAdded / OnItemRemoved.
//
// Setup no Inspector:
//   _inventory   → PlayerInventorySystem da cena
//   _slotPrefab  → prefab com InventorySlotView
//   _content     → Transform do Content (filho do ScrollRect Viewport)
//   _actionPopup → ItemActionPopupView (inativo por padrão na hierarquia)
//
// Hierarquia de Canvas recomendada:
//   Canvas
//   └── InventoryPanel  [este componente + CanvasGroup]
//       ├── ScrollView  (ScrollRect)
//       │   └── Viewport (Mask + Image)
//       │       └── Content  (VerticalLayoutGroup + ContentSizeFitter)
//       └── ItemActionPopup  (ItemActionPopupView — começa inativo)
// ============================================================

using System.Collections.Generic;
using Core.Exploration.Items;
using Services.DebugUtilities;
using UnityEngine;

public sealed class InventoryPanelView : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Dependências")]
    [SerializeField] private PlayerInventorySystem _inventory;

    [Header("Prefab do slot")]
    [SerializeField] private GameObject _slotPrefab;

    [Header("Container (Content do ScrollRect)")]
    [SerializeField] private Transform _content;

    private readonly Dictionary<IItem, InventorySlotView> _slots = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (!ValidateDependencies()) return;

        _inventory.OnItemAdded   += HandleItemAdded;
        _inventory.OnItemRemoved += HandleItemRemoved;

        Populate();
    }

    private void OnDisable()
    {
        if (_inventory == null) return;

        _inventory.OnItemAdded   -= HandleItemAdded;
        _inventory.OnItemRemoved -= HandleItemRemoved;
    }

    // ── Handlers de evento do inventário ─────────────────────────────────

    private void HandleItemAdded(IStorageable storageable, int amount)
    {
        if (storageable is not IItem item) return;

        int total = _inventory.GetAmount(item);

        if (_slots.TryGetValue(item, out var existing))
        {
            existing.Bind(item, total, _inventory);
        }
        else
        {
            var slot = CreateSlot(item, total);
            if (slot != null) _slots[item] = slot;
        }
    }

    private void HandleItemRemoved(IStorageable storageable, int amount)
    {
        if (storageable is not IItem item) return;

        int remaining = _inventory.GetAmount(item);

        if (remaining <= 0)
        {
            if (_slots.TryGetValue(item, out var slot))
            {
                Destroy(slot.gameObject);
                _slots.Remove(item);
            }
        }
        else
        {
            if (_slots.TryGetValue(item, out var slot))
                slot.Bind(item, remaining, _inventory);
        }
    }

    // ── Populate ──────────────────────────────────────────────────────────

    private void Populate()
    {
        ClearSlots();

        foreach (var (storageable, quantity) in _inventory.GetAll())
        {
            if (storageable is not IItem item) continue;

            var slot = CreateSlot(item, quantity);
            if (slot != null) _slots[item] = slot;
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[InventoryPanelView] Populado com {_slots.Count} item(s).", LogCategory.Inventory);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private InventorySlotView CreateSlot(IItem item, int quantity)
    {
        var go   = Instantiate(_slotPrefab, _content);
        var slot = go.GetComponent<InventorySlotView>();

        if (slot == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[InventoryPanelView] Prefab não possui InventorySlotView.", LogCategory.Inventory);
            Destroy(go);
            return null;
        }

        slot.Bind(item, quantity, _inventory);
        slot.OnSlotClicked += HandleSlotClicked;

        return slot;
    }

    private void HandleSlotClicked(IItem item)
    {
       
    }

    private void ClearSlots()
    {
        foreach (var slot in _slots.Values)
        {
            if (slot == null) continue;
            slot.OnSlotClicked -= HandleSlotClicked;
            Destroy(slot.gameObject);
        }

        _slots.Clear();
    }

    private bool ValidateDependencies()
    {
        if (_inventory == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[InventoryPanelView] PlayerInventorySystem não configurado!", LogCategory.Inventory);
            return false;
        }
        if (_slotPrefab == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[InventoryPanelView] SlotPrefab não configurado!", LogCategory.Inventory);
            return false;
        }
        if (_content == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[InventoryPanelView] Content não configurado!", LogCategory.Inventory);
            return false;
        }
        return true;
    }
}