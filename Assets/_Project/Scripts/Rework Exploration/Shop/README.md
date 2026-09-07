# Shop System

## Purpose

Two independent, self-contained purchase buttons: one that sells a
concrete item into the permanent inventory, and one that sells skill tree
points following a global, multi-currency tiered price progression.

Depends on: **Economy** (`WalletSystem`, `ICoin`), **Inventory**
(`InventorySystem`), **Items** (`IIITem`).

## Files

### `ItemShopButton`

Sells a specific item into the permanent inventory, in a buyer-chosen
quantity. **Stateless** — no persistence of its own; every purchase is
validated and executed atomically at click time.

- **All-or-nothing per requested quantity**: only executes if there is
  enough currency AND enough inventory space for the *entire* requested
  amount. The buyer chooses how much to buy (when more than one unit is
  possible); the system does not auto-fill "as much as it can".
- Validates atomically via `InventorySystem.CanFit` **before** spending
  currency, to avoid debiting the wallet and then failing to add the item.
  A safety-net refund exists in case of a theoretical mismatch between the
  check and the actual add (`AddAsMuchAsPossible` returning less than
  `CanFit` predicted).
- Exposes `OnPurchaseSucceeded` / `OnPurchaseFailed` events.

### `SkillPointShopButton`

Sells skill tree points following a **global** price progression across
multiple currencies: fully consumes one currency's price range
(`[100, 200, 300, ...]`, configurable per currency) before moving on to the
next currency in the configured order. Example: `100A, 200A, 300A, 100B,
200B, 300B, ...` — not independent per-currency progressions.

- Becomes **permanently unavailable** once every tier of every currency has
  been sold (`IsExhausted`).
- On a successful purchase, calls a **parameterless** method
  (`ISkillPointGrantable.GrantSkillPoint()`) on a class configured via the
  Inspector — same "empty context" convention used across `IIITem`'s
  effect methods.
- **Has persistent state** (`GlobalTierIndex`) — different from
  `ItemShopButton`, this button's progress must survive across sessions to
  remain coherent (you should never be able to re-buy an already-sold
  tier by reloading).

## Editor-only additions (not in the originally shipped package)

| File | Responsibility |
|---|---|
| `ItemShopButtonEditor.cs` | Play Mode purchase testing with a configurable test quantity; shows total cost. |
| `SkillPointShopButtonEditor.cs` | Play Mode purchase testing; shows current global tier index, exhausted state, and current price/currency. |
| `SkillPointShopSaveSystemEditor.cs` | Buttons: **Save State**, **Load State**, **Delete Save**. |

## Directives

- `ItemShopButton` must remain stateless. If a future requirement needs
  per-button purchase history or limits, that is a different, explicitly
  new persistent component — not a retrofit onto this one.
- Never bypass `CanFit` before spending currency in any future purchase
  flow added to this system; the atomic check-then-spend order is what
  prevents currency loss on a failed add.

## Known limitations / open points

- `SkillPointShopButton.TryGetCurrentTier` is public specifically to
  support the editor's price/currency preview — keep it in sync if the
  tier-resolution logic (`TryResolveCurrentTier`) ever changes shape.
- No refund/rollback path exists for `SkillPointShopButton` if
  `ISkillPointGrantable.GrantSkillPoint()` throws after currency has
  already been spent and the tier index incremented — currently assumed to
  never fail once wired up correctly.
