using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
using Core.Storage;

namespace Core.Inventory
{
    /// <summary>
    /// Fixed-size inventory (N slots, represented as a 1D array; the N×N
    /// visualization is handled by a separate layer). The same class serves
    /// both the losable and the permanent inventory — differentiation is
    /// done via <see cref="InventoryColor"/> and by external orchestration
    /// (see InventoryManager).
    /// </summary>
    public sealed class InventorySystem : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Number of slots. Adjustable for testing the mechanic.")]
        [SerializeField] private int _size = 9;

        [Tooltip("Absolute visual identifier for this inventory (used by the view and in events).")]
        [SerializeField] private Color _inventoryColor = Color.white;

        private InventorySlot[] _slots;

        public event Action<InventorySlotChangedEventArgs> OnInventoryChanged;

        /// <summary>
        /// Raised whenever AddAsMuchAsPossible could not insert every requested unit
        /// (full inventory, per-slot cap, max total instances, etc.).
        /// Carries how many units were requested, accepted and rejected.
        /// </summary>
        public event Action<InventoryOverflowEventArgs> OnItemRejected;

        public int Size => _slots?.Length ?? 0;
        public Color InventoryColor => _inventoryColor;
        public IReadOnlyList<InventorySlot> Slots => _slots;

        private void Awake() => EnsureAllocated();

        private void EnsureAllocated()
        {
            if (_slots == null || _slots.Length != _size)
                _slots = new InventorySlot[_size];
        }

        // ── Public API ───────────────────────────────────────────────

        /// <summary>
        /// Attempts to add as much as possible of <paramref name="amount"/> units
        /// of <paramref name="item"/>, respecting IStorageStrategy. Returns the
        /// quantity actually added (may be less than requested). If fewer units
        /// than requested were added, <see cref="OnItemRejected"/> is raised.
        /// </summary>
        public int AddAsMuchAsPossible(IIITem item, int amount)
        {
            EnsureAllocated();
            if (item == null || amount <= 0) return 0;

            int requested = amount;
            var strategy = item.StorageStrategy;
            int remaining = ClampByMaxTotalInstances(item, amount, strategy);

            if (remaining <= 0)
            {
                RaiseOverflow(item, requested, 0);
                return 0;
            }

            int added = 0;

            // 1) Fill existing slots of the same item, if it can share a slot.
            if (strategy.CanShareSlot)
            {
                for (int i = 0; i < _slots.Length && remaining > 0; i++)
                {
                    if (_slots[i].IsEmpty || !SameItem(_slots[i].Item, item)) continue;

                    int cap = strategy.MaxPerSlot ?? int.MaxValue;
                    int space = cap - _slots[i].Quantity;
                    if (space <= 0) continue;

                    int toAdd = Mathf.Min(space, remaining);
                    _slots[i].Quantity += toAdd;
                    remaining -= toAdd;
                    added += toAdd;
                }
            }

            // 2) Use empty slots for the remainder.
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;

                int cap = strategy.CanShareSlot ? (strategy.MaxPerSlot ?? remaining) : 1;
                int toAdd = Mathf.Min(cap, remaining);

                _slots[i].Item = item;
                _slots[i].Quantity = toAdd;
                remaining -= toAdd;
                added += toAdd;
            }

            if (added > 0)
                RaiseChanged(item, added, InventoryChangeType.Inserted);

            if (added < requested)
                RaiseOverflow(item, requested, added);

            return added;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> units of <paramref name="item"/>,
        /// scanning slots in index order. Returns the quantity actually removed.
        /// </summary>
        public int TryRemoveItem(IIITem item, int amount)
        {
            EnsureAllocated();
            if (item == null || amount <= 0) return 0;

            int remaining = amount;
            int removed = 0;

            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty || !SameItem(_slots[i].Item, item)) continue;

                int toRemove = Mathf.Min(_slots[i].Quantity, remaining);
                _slots[i].Quantity -= toRemove;
                remaining -= toRemove;
                removed += toRemove;

                if (_slots[i].Quantity <= 0)
                    _slots[i].Clear();
            }

            if (removed > 0)
                RaiseChanged(item, removed, InventoryChangeType.Removed);

            return removed;
        }

        /// <summary>
        /// Reads one specific slot without mutating state. Returns false for
        /// an invalid index or an empty slot.
        /// </summary>
        public bool TryGetSlot(int slotIndex, out IIITem item, out int quantity)
        {
            EnsureAllocated();

            if (slotIndex < 0 || slotIndex >= _slots.Length || _slots[slotIndex].IsEmpty)
            {
                item = null;
                quantity = 0;
                return false;
            }

            item = _slots[slotIndex].Item;
            quantity = _slots[slotIndex].Quantity;
            return true;
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> units from ONE specific
        /// slot — only if that slot still holds <paramref name="expectedItem"/>
        /// (guards against a stale selection). Returns the quantity actually
        /// removed (0 if the slot changed). Raises the same Removed event as
        /// <see cref="TryRemoveItem"/>, so InventoryManager's auto-refill
        /// keeps working.
        /// </summary>
        public int TryRemoveFromSlot(int slotIndex, IIITem expectedItem, int amount)
        {
            EnsureAllocated();
            if (expectedItem == null || amount <= 0) return 0;
            if (slotIndex < 0 || slotIndex >= _slots.Length) return 0;
            if (_slots[slotIndex].IsEmpty || !SameItem(_slots[slotIndex].Item, expectedItem)) return 0;

            int toRemove = Mathf.Min(_slots[slotIndex].Quantity, amount);
            _slots[slotIndex].Quantity -= toRemove;

            if (_slots[slotIndex].Quantity <= 0)
                _slots[slotIndex].Clear();

            RaiseChanged(expectedItem, toRemove, InventoryChangeType.Removed);
            return toRemove;
        }

        /// <summary>
        /// Fills a specific slot (must be empty), respecting the item's
        /// per-slot cap. Used by InventoryManager's auto-refill.
        /// </summary>
        public int TryFillSpecificSlot(int slotIndex, IIITem item, int amount)
        {
            EnsureAllocated();
            if (item == null || amount <= 0) return 0;
            if (slotIndex < 0 || slotIndex >= _slots.Length) return 0;
            if (!_slots[slotIndex].IsEmpty) return 0;

            var strategy = item.StorageStrategy;
            int cap = strategy.MaxPerSlot ?? amount;
            int toAdd = Mathf.Min(cap, amount);

            _slots[slotIndex].Item = item;
            _slots[slotIndex].Quantity = toAdd;

            RaiseChanged(item, toAdd, InventoryChangeType.Inserted);
            return toAdd;
        }

        /// <summary>
        /// Checks, without mutating state, whether <paramref name="amount"/> units
        /// of <paramref name="item"/> would fit right now. Used by the shop to
        /// validate atomically before spending currency.
        /// </summary>
        public bool CanFit(IIITem item, int amount)
        {
            EnsureAllocated();
            if (item == null || amount <= 0) return false;

            var strategy = item.StorageStrategy;

            int allowedByTotal = ClampByMaxTotalInstances(item, amount, strategy);
            if (allowedByTotal < amount) return false;

            int capacity = 0;

            if (strategy.CanShareSlot)
            {
                foreach (var slot in _slots)
                {
                    if (slot.IsEmpty || !SameItem(slot.Item, item)) continue;
                    int cap = strategy.MaxPerSlot ?? int.MaxValue;
                    capacity += Mathf.Max(0, cap - slot.Quantity);
                }
            }

            foreach (var slot in _slots)
            {
                if (!slot.IsEmpty) continue;
                capacity += strategy.CanShareSlot ? (strategy.MaxPerSlot ?? amount) : 1;
                if (capacity >= amount) break;
            }

            return capacity >= amount;
        }

        public IEnumerable<int> GetEmptySlotIndices()
        {
            EnsureAllocated();
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].IsEmpty) yield return i;
        }

        /// <summary>First occupied slot, in slot-index order.</summary>
        public bool TryGetFirstOccupiedSlot(out IIITem item, out int quantity)
        {
            EnsureAllocated();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty) continue;
                item = _slots[i].Item;
                quantity = _slots[i].Quantity;
                return true;
            }

            item = null;
            quantity = 0;
            return false;
        }

        /// <summary>
        /// Resizes the inventory. Items outside the new size are NOT carried
        /// over or spilled anywhere — see the README directive about deleting
        /// the save before resizing (dedicated editor button).
        /// </summary>
        public void Resize(int newSize)
        {
            newSize = Mathf.Max(0, newSize);
            var resized = new InventorySlot[newSize];
            int copyCount = Mathf.Min(newSize, _slots?.Length ?? 0);

            for (int i = 0; i < copyCount; i++)
                resized[i] = _slots[i];

            _slots = resized;
            _size = newSize;

            Log(LogLevel.Warning, $"Inventory resized to {newSize} slot(s). " +
                                   "Items outside the new size were discarded — delete the save before resizing in production.");
        }

        /// <summary>True enquanto uma transferência entre inventários está em andamento.
        /// Listeners (ex.: auto-refill do InventoryManager) devem ignorar eventos nesse período.</summary>
        public bool IsTransferInProgress { get; private set; }

        /// <summary>
        /// Move até <paramref name="amount"/> unidades do slot para outro inventário.
        /// Insere no destino primeiro; se a remoção falhar, faz rollback.
        /// Retorna a quantidade efetivamente movida.
        /// </summary>
        public int TransferTo(int slotIndex, InventorySystem target, int amount)
        {
            if (target == null || ReferenceEquals(target, this) || amount <= 0) return 0;
            if (!TryGetSlot(slotIndex, out var item, out int quantity)) return 0;

            amount = Mathf.Min(amount, quantity);

            IsTransferInProgress = true;
            target.IsTransferInProgress = true;
            try
            {
                int added = target.AddAsMuchAsPossible(item, amount);
                if (added <= 0) return 0;

                int removed = TryRemoveFromSlot(slotIndex, item, added);
                if (removed < added)
                {
                    target.TryRemoveItem(item, added - removed); // rollback
                    Log(LogLevel.Error, $"Transfer rollback: added {added}, removed {removed}.");
                }

                return removed;
            }
            finally
            {
                IsTransferInProgress = false;
                target.IsTransferInProgress = false;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static bool SameItem(IIITem a, IIITem b) =>
            a != null && b != null && a.StorageableId == b.StorageableId;

        private int ClampByMaxTotalInstances(IIITem item, int amount, IStorageStrategy strategy)
        {
            if (strategy.MaxTotalInstances is not int max) return amount;

            int current = 0;
            foreach (var slot in _slots)
                if (!slot.IsEmpty && SameItem(slot.Item, item))
                    current += slot.Quantity;

            return Mathf.Max(0, Mathf.Min(amount, max - current));
        }

        private void RaiseChanged(IIITem item, int quantity, InventoryChangeType type) =>
            OnInventoryChanged?.Invoke(new InventorySlotChangedEventArgs(item, quantity, _inventoryColor, type));

        private void RaiseOverflow(IIITem item, int requested, int accepted) =>
            OnItemRejected?.Invoke(new InventoryOverflowEventArgs(item, requested, accepted, _inventoryColor));

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[InventorySystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}

namespace Core.Inventory
{
    /// <summary>
    /// Raised when an inventory could not accept all the requested units
    /// of an item (full inventory, per-slot cap, max total instances, etc.).
    /// </summary>
    public readonly struct InventoryOverflowEventArgs
    {
        public IIITem Item { get; }
        public int Requested { get; }
        public int Accepted { get; }
        public int Rejected => Requested - Accepted;
        public Color InventoryColor { get; }

        public InventoryOverflowEventArgs(IIITem item, int requested, int accepted, Color inventoryColor)
        {
            Item = item;
            Requested = requested;
            Accepted = accepted;
            InventoryColor = inventoryColor;
        }
    }
}