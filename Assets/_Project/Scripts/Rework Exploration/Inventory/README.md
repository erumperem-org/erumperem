# Inventory System

## Purpose

A fixed-size, N-slot inventory (represented internally as a 1D array; N×N
visualization is a separate concern), used identically for both a losable
and a permanent inventory instance. A manager orchestrates migration
between the two, and a UI layer displays slots and item details.

Depends on: **Storage** (`IStorageable`, `IStorageStrategy`), **Items**
(`IIITem`).

## Files

| File | Responsibility |
|---|---|
| `InventoryChangeType.cs` | `Inserted` / `Removed`. |
| `InventorySlot.cs` | Storage unit: `Item` + `Quantity`. Empty when `Item` is null or `Quantity <= 0`. |
| `InventorySlotChangedEventArgs.cs` | Event payload: item, quantity, inventory color, change type. Raised once per **distinct item type** affected by an operation — not once per slot. |
| `InventorySystem.cs` | Core fixed-size inventory. See API below. |
| `InventorySaveData.cs` | Serializable DTO for JSON persistence. |
| `InventorySaveSystem.cs` | Persists/restores an `InventorySystem` to/from its own JSON file, resolving ids via `ItemRegistry`. |
| `InventoryManager.cs` | Orchestrates losable ↔ permanent migration (see Migration rules below). |

## `InventorySystem` API

| Method | Behavior |
|---|---|
| `AddAsMuchAsPossible(item, amount)` | Adds as much as fits, respecting `IStorageStrategy`. Returns actual amount added (may be partial). |
| `TryRemoveItem(item, amount)` | Removes up to `amount`, scanning slots in index order. Returns actual amount removed. |
| `TryFillSpecificSlot(index, item, amount)` | Fills one specific empty slot, respecting per-slot cap. Used by `InventoryManager`'s auto-refill. |
| `CanFit(item, amount)` | **Read-only check** — does not mutate state. Used by the Shop system to validate atomically before spending currency. |
| `Resize(newSize)` | Resizes the backing array. **Items outside the new size are discarded — not carried over anywhere.** See Directives. |

## Two inventories, one class

The losable and permanent inventories are **the same `InventorySystem`
class**, differentiated only by their `InventoryColor` (used in the
change-event payload for view differentiation) and by external
orchestration (`InventoryManager`) — not by subclassing.

## Migration rules (`InventoryManager`)

- **Losable → Permanent** (`RequestFullMigration()`, triggered by an
  external event not yet defined): attempts to migrate **everything** that
  fits, iterating the losable inventory in **slot-index order** (priority
  = sequential index, not rarity or insertion time). **Partial stack
  splits are allowed** — if only part of a stack fits, that part moves and
  the rest remains in the losable inventory.
- **Permanent auto-refill**: whenever *any* slot in the permanent
  inventory empties (regardless of the reason — use, sale, drop), the
  **first occupied slot** (by index) in the losable inventory is pulled
  in, moving **only as much as fits in the freed slot's capacity** (same
  partial-split logic).
- These two behaviors are on **separate, independent triggers** — full
  migration is not the same event as a single-slot auto-refill.

## UI Layer (built after this section's original `.zip`; not yet packaged)

| File | Responsibility |
|---|---|
| `InventorySlotView.cs` | One slot's visual: icon (item sprite) + quantity text. Holds no inventory/item reference — purely display, raises `OnClicked(slotIndex)`. |
| `InventoryGridView.cs` | Spawns one `InventorySlotView` per slot (rebuilt on `Resize`, since slot count is dynamic), syncs on `InventorySystem.OnInventoryChanged`, forwards clicks to `SelectedItemPanelView`. |
| `SelectedItemPanelView.cs` | Shows the clicked item's `DisplayName` (added to `IIITem` in the Items system specifically for this), `Description`, and `Sprite`, with **Use** / **Discard** / **Cancel** actions. |

### Selected item panel behavior

- **Use**: calls `IIITem.ExecuteItemEffect()`, then removes exactly **1**
  unit from the source inventory.
- **Discard**: removes exactly **1** unit at a time, by design — **multi-item
  discard (discarding n at once) is a planned future addition, not yet
  implemented.**
- **Cancel**: clears the selection without touching the inventory.
- All three buttons (Use, Discard, Cancel) **appear together** on
  selection and **disappear together** — clicking any one of the three
  hides all three and clears the current selection.

## Editor-only additions

| File | Responsibility |
|---|---|
| `InventorySystemEditor.cs` | Resize testing field/button, with an inline README-style warning about deleting the save first (see Directives). |
| `InventorySaveSystemEditor.cs` | Buttons: **Save Inventory**, **Load Inventory**, **Delete Save**. |
| `InventoryManagerEditor.cs` | **Request Full Migration** button; live per-slot content dump for both the losable and permanent inventories. |
| `InventoryTestbed.cs` | Single-file, editor-only test harness (mirrors `WalletTestbed`) with **Add**/**Remove** buttons for a reference item and amount. |
| `InventoryGridViewEditor.cs` | **Rebuild Slots** / **Refresh All** buttons (Play Mode). |
| `SelectedItemPanelViewEditor.cs` | Lets you assign a test item + inventory and **Simulate Show** / **Simulate Hide** without needing to click an actual slot (Play Mode). |

## Directives

- **Always delete an inventory's save file before resizing it in testing.**
  `Resize` does not spill or preserve out-of-bounds items anywhere — there
  is no automatic recovery path. This is documented as a manual-process
  directive, not enforced in code, and is surfaced as an inline warning in
  `InventorySystemEditor`.
- Discard is one-unit-at-a-time until multi-item discard is explicitly
  implemented — do not silently expand `HandleDiscard` to remove more than
  1 without also updating this directive and the panel's confirmation UX.

## Known limitations / open points

- `InventorySlotChangedEventArgs` carries an **aggregated** quantity per
  distinct item type per operation, not a per-slot event. `InventoryGridView`
  therefore refreshes **all** slots on any change rather than the single
  affected slot — acceptable given typical inventory grid sizes, but not
  the most granular possible update.
- Multi-item discard (`Discard n at once`) is intentionally unimplemented.
- The UI layer (`InventorySlotView`, `InventoryGridView`,
  `SelectedItemPanelView`) was built after this section's original `.zip`
  — needs to be added to the package.
