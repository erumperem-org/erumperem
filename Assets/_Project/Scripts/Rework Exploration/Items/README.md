# Items System

## Purpose

Defines what a concrete, usable game item is: `IIITem` (see naming note
below), its ScriptableObject base class (`ItemDefinition`), its rarity for
presentation purposes, and the registry used to resolve a persisted id back
to a concrete item asset at runtime.

Depends on: **Storage** (`IStorageable`, `IStorageStrategy`).

## Naming note: `IIITem`

This interface is temporarily named `IIITem` instead of `IItem` because a
legacy `IItem` interface still exists elsewhere in the project and is
scheduled for future removal. Once that legacy interface is deleted, this
one should be renamed to the correct name, `IItem`. Every reference to this
system should use `IIITem` until that migration happens.

## Files

| File | Responsibility |
|---|---|
| `IIITem.cs` | Contract for a usable item: `Sprite`, `DisplayName`, `Rarity`, `ExecuteItemEffect()`, plus everything from `IStorageable`. |
| `ItemRarity.cs` | Presentation-only rarity enum (`Common` → `Legendary`). Declaration order matters — ordinal comparisons (e.g. "best item in a chest") rely on this sequence. Never influences `IStorageStrategy` or any system rule. |
| `ItemDefinition.cs` | Abstract `ScriptableObject` base class implementing `IIITem`. Concrete item types (potions, weapons, skill-tree items, etc.) inherit from this and override `ExecuteItemEffect()`. |
| `ItemRegistry.cs` | `ScriptableObject` mapping `StorageableId → IIITem`, used by save systems to resolve persisted ids back to assets. Scoped exclusively to items — coins have their own separate `CoinRegistry` (SRP). |
| `ItemRegistryValidator.cs` | Structural validation (typing, empty/duplicate ids). No `UnityEditor` dependency — reusable from automated tests or CI. |

## `StorageableId` vs `DisplayName`

`IIITem` (via `IStorageable`) carries two distinct string fields that must
not be confused:

- **`StorageableId`** — opaque, unique identifier used by `ItemRegistry`
  and save systems to resolve an item asset. Never shown to the player.
  Auto-generatable (see below) in the format `ITEM_{8-char uppercase hex}`.
- **`DisplayName`** — human-readable name shown in UI (inventory item
  panels, tooltips, etc.). Freely editable, not unique, never used for
  lookups. Added specifically to support the Inventory system's item
  detail panel, which previously had no player-facing name to show and
  was defaulting to `StorageableId` — a technical id, not appropriate for
  display.

## Editor-only additions (not in the originally shipped package)

| File | Responsibility |
|---|---|
| `ItemRegistryEditor.cs` | Buttons: **Validate Registry**, **List Resolvable Items**, and **Generate Missing IDs (ITEM_...)** (uses the shared `StorageableIdGenerator` from Storage). Only fills in `StorageableId` — never touches `DisplayName`. |
| `RarityColorPalette.cs` | `ScriptableObject` holding a single, project-wide `ItemRarity → Color` mapping. Any system that needs to visually represent rarity (chest view, tooltips, inventory borders, etc.) should reference the same shared asset instead of keeping a local color array. Introduced while building the Chests view. |

## Execution effect contexts are intentionally empty

`ExecuteItemEffect()` (and the two "capability" interfaces discussed for
future item types, `ISkillTreeItem.ApplyToSkillTree(string characterId)` and
`IStatModifierItem.ApplyStatModifiers()`) take **no external context
parameter by design**. Any dependency an effect needs (character identity,
save file paths, etc.) must be resolved internally by the implementation
(e.g. via a singleton), not passed in. This was a deliberate trade-off:
simpler interface, at the cost of implementations depending on globally
accessible state.

## Directives

- When adding a new concrete item type, inherit from `ItemDefinition`, not
  from `ScriptableObject` directly — this guarantees the storage/registry
  contract is honored consistently.
- Never assume a `RarityColorPalette` instance per-system; there should be
  exactly one shared asset per project.
- Never auto-generate or infer `DisplayName` from `StorageableId` (or vice
  versa) — they serve different audiences (system vs. player) and must be
  authored independently.

## Known limitations / open points

- The `IItem` legacy rename is a pending cleanup, not yet scheduled.
- `RarityColorPalette` was created after this section's original `.zip` was
  generated — needs to be added to the package.
- `DisplayName` was added retroactively; any `ItemDefinition` assets
  created before this change will have an empty `DisplayName` until
  manually filled in (no auto-generation is provided for it, by directive).