# Economy System

## Purpose

Defines currencies (`ICoin`), their ScriptableObject base class
(`CoinDefinition`), the registry used to resolve a persisted id back to a
concrete coin asset, the runtime wallet that tracks balances per currency,
its JSON persistence, and a UI view that displays a fixed set of currencies
as icon + amount pairs.

Depends on: **Storage** (`IStorageable`, `IStorageStrategy`).

## Files

| File | Responsibility |
|---|---|
| `ICoin.cs` | Contract for a currency: `Sprite`, plus everything from `IStorageable`. Structurally equivalent to `IIITem`, but with no use effect — coins are not "used" like items. |
| `CoinDefinition.cs` | Concrete (not abstract) `ScriptableObject` for any currency type. Unlike `ItemDefinition`, coins have no polymorphic use effect, so there is no need for subclassing. |
| `CoinRegistry.cs` | `ScriptableObject` mapping `StorageableId → ICoin`. Mirrors `ItemRegistry`, kept separate by SRP — items and coins have distinct lifecycles and consumers. |
| `CoinRegistryValidator.cs` | Structural validation (typing, empty/duplicate ids). No `UnityEditor` dependency — reusable from automated tests or CI. |
| `WalletSaveData.cs` | Serializable DTO (`{ StorageableId, Amount }` list) for JSON persistence. |
| `WalletSystem.cs` | Runtime balance tracker. `Deposit`/`TrySpend` API (see naming note below), `OnBalanceChanged` event, `RestoreState` for loading. |
| `WalletSaveSystem.cs` | Persists/restores a `WalletSystem` to/from its own JSON file, resolving `StorageableId ↔ ICoin` via `CoinRegistry`. |

## Editor-only additions (not in the originally shipped package)

| File | Responsibility |
|---|---|
| `CoinRegistryEditor.cs` | Buttons: **Validate Registry**, **List Resolvable Coins**, and **Generate Missing IDs (COIN_...)** (uses the shared `StorageableIdGenerator` from Storage). |
| `WalletSystemEditor.cs` | Play Mode-only debug view listing every coin's current balance. |
| `WalletSaveSystemEditor.cs` | Buttons: **Save Wallet**, **Load Wallet**, **Delete Save**. |
| `WalletTestbed.cs` | Single-file, editor-only test harness (`MonoBehaviour` + nested `Editor`) with **Deposit**/**Spend** buttons for a reference coin and amount. Not meant for production scenes. |

## UI additions (built while wiring up the Chests view; not yet part of the packaged zip)

| File | Responsibility |
|---|---|
| `CoinDisplaySlot.cs` | Serializable binding of one `ICoin` to an `Image` (icon) and a `TMP_Text` (amount). One entry per currency the view should render. |
| `WalletView.cs` | Displays a fixed, configurable set of `CoinDisplaySlot`s. Subscribes to `WalletSystem.OnBalanceChanged` to stay in sync without polling; coins not included in its slot list are simply never rendered. |
| `WalletViewEditor.cs` | Flags slots with missing references (coin/icon/text) directly in the Inspector, even outside Play Mode; **Refresh Now** button in Play Mode. |

## Naming note: `Deposit` / `TrySpend`

`WalletSystem` originally exposed an `AddCoins` method, mirroring the
inventory's `AddItems` vocabulary literally. It was renamed to
`Deposit`/`TrySpend` once the Shop system needed a debit operation —
`AddCoins`/`RemoveCoins` would have been semantically muddier than the
economy-appropriate `Deposit`/`TrySpend` pairing. `TrySpend` returns `false`
without mutating state if the balance is insufficient.

## Event coverage for view consumers

`WalletSystem.OnBalanceChanged` fires on **both** `Deposit` and `TrySpend` —
there is a single event covering additions and removals alike (it does not
distinguish "type" of change, only which coin changed and its new balance).
This was confirmed sufficient for `WalletView` without needing any change to
`WalletSystem` itself.

## Directives

- Never overwrite an existing `StorageableId` when auto-generating ids.
- `WalletView` compares coins by `StorageableId`, not object reference —
  keep this convention if extending or replacing the view, for consistency
  with `InventorySystem.SameItem` and the rest of the project.

## Known limitations / open points

- `CoinDisplaySlot` / `WalletView` / `WalletViewEditor` were introduced after
  this section's `.zip` was generated — need to be added to the package.
- `WalletView` only renders a fixed, Inspector-configured subset of
  currencies. A "show all currencies with balance > 0" dynamic view (with
  runtime-spawned slots) would be a different component if ever needed.
