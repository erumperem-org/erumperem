# Character Stats Items System

## Purpose

Implements the two concrete item categories from the combat item design
doc: **status-modifying items** (Health/Defense/Critical potions, charms,
and the five thematic multi-status items) and **skill tree reset items**.

Depends on: **Storage** (`IStorageable`), **Items** (`ItemDefinition`,
`IIITem`).

## Why this doesn't hook into a "concrete gameplay system"

Status items don't call any inventory/combat/buff system directly — they
permanently rewrite a character's **base stats**, persisted to disk. Any
system that computes a character's effective stats is expected to read
this data when it needs it; this system only owns the storage and the
mutation, not the consumption.

## Files

| File | Responsibility |
|---|---|
| `StatType.cs` | `Health` / `Defense` / `Critical`. |
| `StatModifierType.cs` | `Absolute` (flat delta) / `Relative` (percentage of current value, `n/100`). |
| `StatModifierEntry.cs` | One stat change: `StatType` + `StatModifierType` + `Magnitude`. Individual-status items configure exactly one; thematic items configure several. |
| `CharacterStatsData.cs` | In-memory `StatType → float` view for a single character, unpacked from the shared file. |
| `CharacterStatsFileData.cs` | JSON DTO for the **single shared file** — a flat list of characters, each with a flat list of stats (JsonUtility cannot serialize `Dictionary` directly). |
| `CharacterStatsRepository.cs` | Static service: `LoadAll`, `SaveAll`, `DeleteFile`, `GetStatsForCharacter` (the "unpack by id" operation), and `ApplyModifiers` (the read-modify-write cycle items call). |
| `CharacterIdentityResolver.cs` | Returns a **placeholder character id**. The real lookup is commented out inside the method, ready to swap in later. |
| `SkillTreeResetScope.cs` | `General` / `CharacterSpecific`, matching "Reset Geral" vs. "Reset — BuckWyatt/Wulfric/Matsuda". |
| `StatusModifierItem.cs` | Concrete `ItemDefinition`. `ExecuteItemEffect()` resolves the (placeholder) character id and calls `CharacterStatsRepository.ApplyModifiers`. |
| `SkillTreeResetItem.cs` | Concrete `ItemDefinition`. `ExecuteItemEffect()` is **intentionally empty** — the real skill tree call is written as a commented-out `TODO` inside the method body. |

## Editor-only additions

| File | Responsibility |
|---|---|
| `ItemEffectTestbed.cs` | Generic **Execute Effect** button for any `IIITem` — works for both `StatusModifierItem` and `SkillTreeResetItem`, since both go through the same interface method. |
| `CharacterStatsTestbed.cs` | **Dump Stats For ID**, **Apply Test Modifier** (single ad-hoc modifier, no item asset needed), and **Delete Stats File** buttons. |

## Single shared file, cumulative writes

All status items persist to **one** JSON file (`character_stats.json`),
containing every character's stats — not one file per character. Every
`ApplyModifiers` call is a full read-modify-write cycle: load the entire
file, find (or create) the target character's entry, apply each modifier
against the **currently stored value**, save the entire file back.

Example: a Health Potion with an `Absolute` entry of `+1`, used three times
on a character starting at `Health = 0`, produces `1`, then `2`, then `3` —
each use reads the latest persisted value before adding, exactly as
specified.

`Relative` modifiers apply a percentage of the current value
(`current + current * (magnitude / 100)`); a negative magnitude decreases
it — this is how "subtractions" and "percentage-based" changes are both
covered by the same two-operation model (`Absolute` covers flat
add/subtract, `Relative` covers percentage-based increase/decrease).

## Magnitude values are not hardcoded

The design doc uses qualitative intensity symbols (`+`, `++`, `+++`,
`++++`) without concrete numbers. This package does **not** invent balance
values — `StatModifierEntry.Magnitude` is authored per item asset in the
Inspector. The 37 concrete items from the doc (Health Potion Small/Medium/
Big, Vampírico Small/Big/Extreme, etc.) are meant to be created as
`StatusModifierItem` / `SkillTreeResetItem` assets by the designer,
choosing the actual numbers per intensity tier — this package provides the
two generic, reusable classes, not 37 pre-filled assets.

## Directives

- **Never hardcode intensity-to-number mappings in code.** If a future
  requirement needs a shared intensity scale (e.g. "++" always equals
  exactly double "+"), that belongs in a separate, explicit balancing
  utility — not baked into `StatModifierEntry` or `StatusModifierItem`.
- **Do not implement the real character id lookup by editing
  `StatusModifierItem` directly.** Change `CharacterIdentityResolver`
  only — every status item already depends on it indirectly.
- **Do not implement the real skill tree reset logic by editing
  `SkillTreeResetItem`'s empty body ad hoc.** The commented-out `TODO`
  inside `ExecuteItemEffect()` documents the intended call shape; replace
  the comment with a real call once the skill tree system exists, rather
  than writing new, differently-shaped logic.

## Known limitations / open points

- `CharacterIdentityResolver` always returns the same placeholder id —
  every status item currently modifies the same character's data,
  regardless of who is actually playing. This is deliberate per your
  instruction and awaits the real identity system.
- `CharacterStatsRepository` uses **synchronous** file I/O
  (`File.ReadAllText`/`File.WriteAllText`), unlike the async pattern used
  by `WalletSaveSystem`/`InventorySaveSystem` — necessary because
  `IIITem.ExecuteItemEffect()` is a synchronous `void` method with no
  async variant. Each item use is its own full read-modify-write cycle;
  using several status items in the same frame triggers that many
  sequential file operations, not one batched write. `ApplyModifiers`
  already accepts a list of modifiers, so batching multiple changes into
  a single call (and therefore a single file write) is possible if this
  ever becomes a concern.
- `SkillTreeResetItem`'s `TargetCharacterId` field carries the same
  placeholder-id caveat as `CharacterIdentityResolver` — it is plain
  authored data (a string), not resolved through any lookup yet.
