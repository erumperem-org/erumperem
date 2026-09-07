# Rewards System

## Purpose

A reusable reward-roll service, consumed independently by multiple systems
(Chests, corruption-tier-based dispensing) from their own, separate
`LootTable` sets. Deliberately a **new, parallel structure** — does not
share implementation with the project's pre-existing `LootService`/
`LootTable`/`ILootService` (still used by `InventoryLootGranter`, which is
out of scope for this system and untouched).

Depends on: **Storage** (`IStorageable`).

## Files

| File | Responsibility |
|---|---|
| `LootEntry.cs` | A single entry: a storable (item or coin), an **independent chance percentage** (0–100), and a **min–max quantity range**. |
| `LootTable.cs` | `ScriptableObject` holding a list of `LootEntry`. |
| `IDrawMethod.cs` | Contract for a draw algorithm over a `LootTable`. Kept as an interface so future draw modes can be added without touching `RewardGeneratorService` or `LootTable`. |
| `IndependentChanceDrawMethod.cs` | The only implemented draw mode: each entry is evaluated **in isolation** against its own chance. Any subset of entries can come out — including none or all of them. **No pity/guarantee system.** |
| `RewardGeneratorService.cs` | Plain C# service (not a `MonoBehaviour`) wrapping an `IDrawMethod`. `Generate(LootTable)` returns `IReadOnlyDictionary<IStorageable, int>` — generic on purpose; the caller decides how to route coins vs. items. |
| `CorruptionRewardDispenser.cs` | Consumer example: resolves a corruption value (0–200) into a tier (0–4) via the project's existing `CorruptionTierCalculator`, then rolls that tier's dedicated `LootTable`. |

## Editor-only additions (not in the originally shipped package)

| File | Responsibility |
|---|---|
| `CorruptionRewardDispenserEditor.cs` | Play Mode testing: type in a corruption value, click **Generate**, see the resolved rewards listed directly in the Inspector. |

## External dependency: `CorruptionTierCalculator`

This system calls `CorruptionTierCalculator.GetTier(double)` and, by
extension, depends on `CorruptionRules` (`MinCorruptionValue`,
`Tier0UpperInclusive` ... `Tier3UpperInclusive`). **Neither type is defined
in this package** — they are assumed to already exist in the target
project, exactly as provided by the project owner. If they live inside a
specific namespace in the real project, the `using` directives in
`CorruptionRewardDispenser.cs` (and any editor code referencing it) need to
be adjusted accordingly.

## "Baú" (chest) consumer note

Chests do **not** use `RewardGeneratorService` directly — a chest is
passive and only receives an already-resolved copy of a generated result.
The system that owns the chest-tier reevaluation logic is the one calling
`RewardGeneratorService.Generate(...)` on the chest's behalf. See the
**Chests** README for the full flow.

## Directives

- Draw mode is intentionally without any guarantee/pity mechanic. If a
  guarantee is ever required, it must be a **new** `IDrawMethod`
  implementation — never a modification to `IndependentChanceDrawMethod`
  that silently changes its documented behavior.
- Loot tables intended for systems that report "best item rarity" (see
  Chests) should not be composed of coins only — see the Chests README's
  directive on this.

## Known limitations / open points

- Only one `IDrawMethod` exists. The interface anticipates more (e.g.
  weighted single-pick, guaranteed-slot rolls) without requiring changes
  elsewhere, but none beyond `IndependentChanceDrawMethod` have been
  implemented yet.
- `RewardGeneratorService` has no built-in duplicate-source protection: if
  two different `LootTable` assets are rolled back-to-back into the same
  external aggregation, merging is entirely the caller's responsibility.
