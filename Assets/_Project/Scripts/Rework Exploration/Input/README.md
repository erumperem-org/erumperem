# Input Action Triggers

## Purpose

Two generic, reusable components that bind a New Input System
`InputActionReference` to a target `GameObject`, invoking configurable
`UnityEvent`s on input — the input-driven equivalent of a UI `Button`'s
`onClick`. Neither component hardcodes what the "action" actually does;
that's wired per-instance in the Inspector, the same way a `Button`'s
`onClick` is.

Depends on: `UnityEngine.InputSystem` (New Input System).

## Files

| File | Responsibility |
|---|---|
| `InputActionButtonTrigger.cs` | Binds one `InputActionReference`, invokes a single `UnityEvent` (`OnTriggered`) every time the action performs. |
| `InputActionToggleTrigger.cs` | Binds one `InputActionReference`, alternates between two `UnityEvent`s (`OperationA` / `OperationB`) on each performed input — e.g. open on the 1st press, close on the 2nd, open again on the 3rd. |

Both components include their `Editor` class in the **same file** (guarded
by `#if UNITY_EDITOR`), per this project's convention for test-only
harnesses. Each editor adds a **Simulate Trigger** button (Play Mode only)
that calls the same public `Trigger()` method the real input would call —
useful for testing the wired-up behaviour without needing a physical
input device bound to the action.

## `_target`: contextual only

Both components take a `GameObject _target` field, but **neither uses it
directly** — it exists purely as a contextual reference for whoever wires
the `UnityEvent`s in the Inspector (e.g. dragging `_target` into an
`OnTriggered` slot and picking `GameObject.SetActive(bool)` from the
dropdown, the same way any `UnityEvent` is wired). If a future variant
needs the component to act on `_target` automatically without manual
wiring, that's a different, explicit component — not a change to these two.

## Bind/unbind lifecycle — read this before reusing either component

Both components bind the input action (`.Enable()` +
`.performed += handler`) in **`OnEnable`** and unbind
(`.performed -= handler` + `.Disable()`) in **`OnDisable`** — this is
correct and required for any instance that lives on a **persistent**
GameObject, separate from whatever it acts on.

**It becomes a bug if the component lives on the very GameObject it
deactivates.** If a script bound this way toggles off its own GameObject,
`OnDisable` fires immediately, unsubscribing the handler — and since
nothing else re-enables that GameObject, the input binding is
permanently lost after the very first toggle-off. This exact failure mode
was hit (and deliberately reproduced) while building the Panels system —
see that README's "Input bind/unbind lifecycle bug" section for the full
story and the two different fixes that came out of it (`Awake`/`OnDestroy`
binding for one component, keeping `OnEnable`/`OnDisable` for another, on
purpose, once the underlying cause was understood).

## Directives

- Never bind input in `OnEnable`/`OnDisable` on a component that also
  deactivates its own GameObject as part of its behaviour. Use
  `Awake`/`OnDestroy` for that specific case instead — see the Panels
  README for a concrete before/after example.
- Keep `_target` purely contextual. If a variant is ever added that acts
  on `_target` directly, name it distinctly (don't silently change the
  meaning of the existing `_target` field on these two components).

## Known limitations / open points

- Assumes the New Input System exclusively. Porting to the legacy Input
  Manager would require replacing the bind/unbind mechanism entirely
  (no `.performed` event, no `InputActionReference`).
- `InputActionToggleTrigger`'s alternation state (`_nextIsOperationA`) is
  purely in-memory — it resets to `_startOnOperationA` on every
  `Awake` (e.g. scene reload), regardless of what was actually shown last.