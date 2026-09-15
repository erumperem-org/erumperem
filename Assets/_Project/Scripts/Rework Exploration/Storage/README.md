# Storage System

## Purpose

Foundational layer shared by every other system in this project. Defines
what it means for something to be "storable" (`IStorageable`) and how
multiple units of a storable thing behave when placed into a container
(`IStorageStrategy`) — without any container implementation knowing about
concrete storage rules via switch statements on an enum.

This system has no dependencies on any other system in the project. Every
other system (Items, Economy, Inventory, Shop, Rewards, Chests) depends on it.

## Files

| File | Responsibility |
|---|---|
| `IStorageable.cs` | Base contract for anything storable: `StorageStrategy`, `StorageableId`, `Description`. |
| `IStorageStrategy.cs` | Policy contract: `CanShareSlot`, `MaxPerSlot`, `MaxTotalInstances`. |
| `StackableStorageStrategy.cs` | Multiple units share a slot, up to a configurable cap. |
| `UniqueStorageStrategy.cs` | Only one instance may exist in the whole container. |
| `SingleSlotStorageStrategy.cs` | Never stacks — each unit gets its own slot — but multiple instances may coexist. |
| `UnlimitedStorageStrategy.cs` | No slot or quantity limit. Typical use: simplified systems (e.g. coins). |
| `StorageStrategyTestbed.cs` | Editor-only harness to inspect a strategy's resolved data and simulate additions, without needing a real item or inventory. |

## Editor-only additions (not in the originally shipped package)

| File | Responsibility |
|---|---|
| `StorageStrategyDrawer.cs` | `[CustomPropertyDrawer]` for any `[SerializeReference] IStorageStrategy` field — renders a type-selection dropdown populated via `TypeCache`. Without this, `[SerializeReference]` fields show up empty with no way to pick a concrete type. |
| `StorageableIdGenerator.cs` | Shared utility (used by `ItemRegistryEditor` and `CoinRegistryEditor`) that auto-generates `"{PREFIX}_{8-char uppercase hex}"` ids for any registry entry with an empty `StorageableId`. **Never overwrites an existing id** — doing so would break save data referencing it. |

## Design rationale

`IStorageStrategy` replaced an earlier `StorageMode` enum
(`Unique / Stackable / SingleSlot / Unlimited`) that had three problems:
non-standard naming, semantic overlap between `Stackable` and `Unlimited`,
and reliance on a switch statement in the consuming system for every mode.
The Strategy pattern removes all three: each mode is a class carrying its
own data, and adding a new mode never requires touching an inventory or
wallet implementation.

## Directives

- **Never overwrite an existing `StorageableId`** when auto-generating ids
  (via `StorageableIdGenerator`). Overwriting would silently break any save
  file that already references that id.

## Known limitations / open points

- None specific to this system. It is intentionally minimal and stable —
  new storage modes should be added as new `IStorageStrategy`
  implementations, never by modifying the interface itself.