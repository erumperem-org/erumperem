# Scene Allocation Persistence

## Purpose

Save/load layer on top of the pre-existing **Scene Object Allocation
System** (`SceneObjectAllocationSystem`, `PlaceableObjectData`,
`AllocationResult`, `IObjectAllocationSystem` — not redefined here, assumed
to already exist in the project). Follows the same model already used for
Items/Coins: a registry (with validation and editor id-generation) plus a
save system that either loads a prior result or runs the real routine.

## Files

| File | Responsibility |
|---|---|
| `PlaceableObjectEntry.cs` / `PlaceableObjectRegistry.cs` | Maps a unique string id to a `PlaceableObjectData` asset. The id lives in the registry entry, not on the asset itself, since `PlaceableObjectData` (external, pre-existing) has no id field of its own. |
| `PlaceableObjectRegistryValidator.cs` | Structural validation: missing data reference, empty id, duplicate id. No `UnityEditor` dependency. |
| `SceneAllocationSaveData.cs` | JSON DTO: a list of `{ PositionIndex, PlaceableObjectId, Scale, EulerRotation }`. |
| `SceneObjectAllocationSaveSystem.cs` | Orchestrates the load-or-allocate decision (see below) and owns the spawned instances. |

## Editor-only additions

| File | Responsibility |
|---|---|
| `PlaceableObjectRegistryEditor.cs` | **Validate Registry**, **List Resolvable Entries**, **Generate Missing IDs (PLACEABLE_...)** — same never-overwrite convention as `StorageableIdGenerator`. |
| `SceneObjectAllocationSaveSystemEditor.cs` | **Initialize** (disabled once already initialized) and **Reset And Delete Save** buttons; shows initialized state and spawned instance count. |

## Position identity: index-based

A `Transform` cannot be serialized to JSON, and scene object names are too
fragile to rely on (renaming breaks the save). Position identity is
therefore the **index of that `Transform` within the `_availablePositions`
list** passed to the save system — the same list order must be preserved
between the run that created the save and any run that loads it.

## The load-or-allocate decision

`Initialize()`:

1. If no save file exists, or it exists but is empty (no `Placements`
   entries), **runs the real allocation routine**
   (`AllocateObjectsAsync` — random, balanced selection exactly as
   documented in the original system) and **persists the result**: for
   each placed object, resolves its `PlaceableObjectData` back to a
   registry id (`ResolveId`), its position back to an index
   (`_availablePositions.IndexOf(...)`), and records the **exact** rolled
   scale/rotation from the instantiated transform.
2. If a non-empty save exists, **skips the allocation routine entirely**
   and reconstructs the scene directly: for each saved entry, resolves the
   id back to a `PlaceableObjectData` (`Resolve`), instantiates its prefab
   at `_availablePositions[PositionIndex]`, and applies the **exact**
   saved scale/rotation — no re-rolling, no re-balancing, bit-for-bit the
   same result as what was saved.

## Directives

- Never overwrite an existing registry entry id when auto-generating —
  same rule as `StorageableIdGenerator`, for the same reason (breaking
  saves that already reference that id).
- Never change the order or contents of `_availablePositions` between a
  save and its corresponding load without also invalidating (deleting)
  that save — position identity is purely index-based and has no
  cross-check against the actual `Transform` it refers to.
- If a `PlaceableObjectData` used in a fresh allocation has no
  corresponding registry entry, that specific placement is **not saved**
  (logged as an error) — it will be re-rolled on every subsequent load
  until a registry entry is added for it.

## Known limitations / open points

- `SceneObjectAllocationSaveSystem` depends on `SceneObjectAllocationSystem`
  / `PlaceableObjectData` / `AllocationResult` / `IObjectAllocationSystem`,
  none of which are defined in this package — only their README-documented
  shape is assumed, same caveat as `ChestAllocationSystem` in the Chests
  system.
- No migration path exists if `_availablePositions` changes shape (adding/
  removing/reordering markers) after a save was written — the directive
  above (delete the save first) is the only safeguard, not enforced in code.
- `ResolveId` requires exact object reference equality between the
  `PlaceableObjectData` used during allocation and the one registered —
  if the object pool ever references a different asset instance with the
  same visual content, it will not resolve to the same id.
