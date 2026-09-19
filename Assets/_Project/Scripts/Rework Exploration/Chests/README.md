# Chests System

## Purpose

Three cooperating pieces: a passive `Chest` container, a system that
dynamically discovers chests and reevaluates their loot per corruption
tier, and a system that positions/repositions a fixed pool of chest
instances (reusing the project's existing Scene Object Allocation System).
A view layer reacts to the chest's own events for color and (eventually)
animation.

Depends on: **Storage** (`IStorageable`), **Items** (`IIITem`,
`ItemRarity`, `RarityColorPalette`), **Rewards** (`LootTable`,
`RewardGeneratorService`), and the project's pre-existing **Scene Object
Allocation System** (`PlaceableObjectData`, `AllocationResult`,
`SceneObjectAllocationSystem`, `IObjectAllocationSystem` — **not defined in
this package**, assumed to already exist in the target project exactly as
described in its own README).

## Files

### `Chest`

Passive container — **does not** call `RewardGeneratorService` and **does
not** route granted content to `WalletSystem`/`InventorySystem`. Both of
those are explicitly out of scope for this component.

- `AssignLoot(IReadOnlyDictionary<IStorageable,int>)` always **clones** the
  given dictionary (the chest must never reference the original — the
  table is consumed by the chest). Resets `_consumed` to `false` and fires
  `OnChestStateChanged(ChestState.Closed)`.
- `AssignLoot` also immediately fires `OnBestItemRarityRevealed`, using
  `ItemRarity.Common` as a fallback when the content has no `IIITem` (e.g.
  coin-only content) — this lets the view preview a color before the chest
  is even opened.
- `Interact()` grants the entire content at once (**no draw, no chance
  evaluated here** — that already happened upstream), clears the content,
  fires `OnChestStateChanged(ChestState.Open)`, and fires
  `OnBestItemRarityRevealed` again **only if** an `IIITem` was present
  (**no `Common` fallback on this path** — see Known limitations).
- `DebugContents` exposes a read-only snapshot for editor/debugging only.

### `ChestState`

Two values: `Closed`, `Open`. Presentation-only signal, no gameplay logic.

### `ChestLootReevaluationSystem`

- Discovers chests **dynamically**: scene scan on `Start()`
  (`FindObjectsByType<Chest>`, public as `DiscoverChestsInScene()` for
  manual re-scans) **and** listens to `ChestAllocationSystem.OnChestCreated`
  for chests created after the initial scan.
- Also listens to `ChestAllocationSystem.OnChestsRepositioned` — every
  reposition reassigns loot to the repositioned chests, which (via
  `Chest.AssignLoot`) **resets their consumed state**, i.e. relocating a
  chest always re-closes it with fresh loot, even if it had already been
  opened.
- Has its **own** set of per-tier table pools (`ChestTierTablePool[]`) —
  **not shared** with `CorruptionRewardDispenser`'s tables (Rewards
  system). Each tier can offer **multiple possible tables**; one is picked
  at random per chest, per assignment.
- Each chest gets an **independent** roll (`RewardGeneratorService.Generate`)
  — chests sharing a tier do not receive identical results.
- `CurrentTier` resolves to the real `ICorruptionTierSource` when assigned,
  falling back to a manual test tier (`SetManualTestTier`) otherwise — lets
  the system run without a real corruption source wired up.

### `ChestAllocationSystem`

- Creates a **fixed pool once** (`Initialize()`, public, `async void`),
  sized for the largest possible roll across every tier's count range.
  Never destroys/recreates chest instances afterward.
- `Reallocate()` only **repositions** (and activates/deactivates) existing
  instances. **Only the position is reassigned on reposition — rotation is
  left untouched**, preserving the random rotation `SceneObjectAllocationSystem`
  originally applied per-instance during pool creation. An earlier version
  used `SetPositionAndRotation`, which discarded that randomization on
  every reallocation; this was corrected.
- Active chest count per tier is a **range** (`ChestCountRange`, min–max),
  re-rolled on every `Reallocate()` call — not a fixed value.
- `OnChestCreated` fires only during the **initial** pool creation, never
  on subsequent repositions.
- `OnChestsRepositioned` fires on every positioning pass (initial and
  subsequent), carrying the list of chests that ended up active — this is
  what `ChestLootReevaluationSystem` listens to.
- `CurrentTier` has the same manual-test-tier fallback as
  `ChestLootReevaluationSystem`.
- `Initialize()` forces a real `await Task.Yield()` before doing any work.
  This closes a race condition: if the underlying allocation completes
  fully synchronously (e.g. `spreadAcrossFrames = false`), `OnChestCreated`
  /`OnChestsRepositioned` could fire before `ChestLootReevaluationSystem`
  had subscribed to them, depending on script execution order — silently
  leaving every chest without loot.

### `ChestView`

- Listens to two of the chest's own events: `OnBestItemRarityRevealed`
  (drives a `Renderer`'s material color via the shared, project-wide
  `RarityColorPalette` from the Items system) and `OnChestStateChanged`
  (intended to drive an Animator trigger).
- **Animation wiring is intentionally left commented out** — fields and
  the trigger-call structure are sketched in comments, to be implemented
  once the Animator Controller and its parameter names are finalized.

## Editor-only additions

| File | Responsibility |
|---|---|
| `ChestEditor.cs` | Shows consumed/has-content state, **lists the actual chest content** (`DebugContents`), and an **Interact** button. |
| `ChestLootReevaluationSystemEditor.cs` | Diagnostics: per-tier table pool size (flags `EMPTY — will fail` visibly, even outside Play Mode); known chest count; manual test tier input + Apply; **Discover Chests In Scene** and **Force Reevaluate All** buttons. |
| `ChestAllocationSystemEditor.cs` | Pool-created state, active chest count, manual test tier input + Apply, **Initialize** (disabled once the pool exists) and **Reallocate** (disabled until it does) buttons. |
| `ChestViewEditor.cs` | Flags missing `Renderer`/`RarityColorPalette` references; a rarity dropdown + **Apply Color Preview** button that works even outside Play Mode (via reflection into the private event handler); **Simulate Open/Closed** buttons (Play Mode only). |

## Directives

- **Chest loot tables should not be coin-only.** There is no code-level
  check enforcing this (coins are always consumed by their own system, by
  design) — it is a documentation directive: a coin-only table means
  `Interact()`'s rarity reveal has nothing to report (see Known
  limitations for the `AssignLoot` vs `Interact` asymmetry this creates).
- Never call `SetPositionAndRotation` (or otherwise touch rotation) inside
  `ApplyTierBasedPositions` — only position should be reassigned on
  reposition, to preserve `SceneObjectAllocationSystem`'s original random
  rotation.
- Never remove the `await Task.Yield()` at the start of
  `ChestAllocationSystem.Initialize()` — it is what guarantees
  `ChestLootReevaluationSystem` has finished subscribing before any
  creation/reposition event fires, regardless of script execution order.

## Known limitations / open points

- **`AssignLoot` vs `Interact` rarity-fallback asymmetry**: `AssignLoot`
  always fires `OnBestItemRarityRevealed` (falling back to `ItemRarity.Common`
  for coin-only content), but `Interact()` does **not** — it only fires the
  event when an actual `IIITem` is present, otherwise just logging a
  warning. Practical effect: a coin-only chest's preview color (`Common`,
  set on assignment) never updates again after it is opened, since
  `Interact()` never re-fires the event in that case. Not yet corrected —
  flagged for a decision on whether `Interact()` should mirror the same
  `Common` fallback for consistency.
- `ICorruptionTierSource` remains an integration stub — no real corruption
  source has been connected yet.
- The external event that should call `ChestAllocationSystem.Reallocate()`
  has not been defined; it remains a public method waiting to be wired up.
- `ChestAllocationSystem` depends on `PlaceableObjectData` /
  `AllocationResult` / `SceneObjectAllocationSystem` /
  `IObjectAllocationSystem`, none of which are defined in this package —
  only their README-documented shape is assumed. `_objectAllocationSystem`
  is serialized as the concrete `SceneObjectAllocationSystem` type rather
  than the `IObjectAllocationSystem` interface, since Unity cannot
  serialize an interface field directly without an additional wrapper.
