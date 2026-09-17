# Panel Visibility System

## Purpose

Coordinates panel open/close behaviour (accordion-style: opening one panel
closes any other open panel) and HUD visibility (HUD active exactly when
no panel is active), without panel scripts holding direct references to
each other. Coordination happens entirely through one static,
central event.

Depends on: **Input Action Triggers** (`InputActionPanelToggleTrigger`
binds input the same way `InputActionButtonTrigger`/`InputActionToggleTrigger`
do) and `UnityEngine.InputSystem` transitively through that one file.

## Files

| File | Responsibility |
|---|---|
| `PanelVisibilityController.cs` | **Static** event bus: `event Action<GameObject, bool> OnPanelVisibilityChanged`, and `NotifyVisibilityChanged(panel, isActive)` to broadcast a change. Does not itself call `SetActive` on anything — callers apply the state change first, then notify. |
| `InputActionPanelToggleTrigger.cs` | Input-bound panel toggle. Toggles its own target panel on input, broadcasts the change, and listens to the same broadcast to auto-close its panel when a *different* panel opens. |
| `PanelVisibilityListener.cs` | Same auto-close listening behaviour, but with **no input binding** — exposes public `Open()` / `Close()` / `Toggle()` for something else (a UI Button, `InputActionButtonTrigger`, etc.) to drive it. |
| `HudVisibilityController.cs` | Keeps a HUD `GameObject` active exactly when the tracked set of active panels is empty. Seeds its initial state by scanning an explicit `_panels` list in `Awake` (see below) rather than inferring it purely from broadcasts. |
| `PanelVisibilityControllerTestbed.cs` | Editor-only harness to manually broadcast a test `(panel, isActive)` event, without any real trigger wired up. |

Every `MonoBehaviour` above includes its `Editor` class in the same file
(`#if UNITY_EDITOR`), each with Play Mode buttons to simulate/inspect
behaviour directly.

## Why the controller is `static`, not a scene singleton

`PanelVisibilityController` is a static class, not a `MonoBehaviour`
singleton (unlike, e.g., the project's `SaveSlotAccess` pattern). This was
a deliberate choice: a singleton would require every subscriber's
`OnEnable` to run *after* the singleton's own `Awake` in the same frame —
an ordering Unity does not guarantee across GameObjects in the same scene.
A static event has no such dependency; anything can subscribe the moment
it exists, regardless of scene/script load order.

## Input bind/unbind lifecycle bug (and how it was found/fixed)

This is worth understanding before touching either `InputActionPanelToggleTrigger`
or `HudVisibilityController` again.

**The bug**: binding an input action's `.performed` handler in `OnEnable`
and unbinding in `OnDisable` is the correct, standard pattern — *except*
when the component also deactivates its own GameObject as part of its own
behaviour. In that specific case: input fires → the component closes its
own panel (`SetActive(false)`) → Unity immediately calls `OnDisable` on
that now-inactive GameObject → the handler unsubscribes → nothing is left
to ever reactivate the panel via input again, since the very thing that
would have re-triggered it just unsubscribed itself. One toggle-off and
the binding is gone for good.

**First fix applied (`InputActionPanelToggleTrigger`)**: moved the input
bind/unbind from `OnEnable`/`OnDisable` to `Awake`/`OnDestroy`, which
survives the component's own `SetActive` calls. This was later
**intentionally reverted** back to `OnEnable`/`OnDisable` at the person's
explicit request (they were testing whether the same bug would be caught
a second time elsewhere) — so as of the current version,
`InputActionPanelToggleTrigger` binds input in `OnEnable`/`OnDisable`
again, on purpose.

**Second occurrence (`HudVisibilityController`)**: the *same* underlying
bug — not the input-specific part, but the general shape of it — existed
in `HudVisibilityController`'s original `OnEnable`/`OnDisable`
subscription to `PanelVisibilityController.OnPanelVisibilityChanged`. If
this controller ever lives on the same GameObject as the HUD it controls
(`_hud` pointing at `this.gameObject`, or a parent that gets deactivated
alongside it), deactivating the HUD would unsubscribe the very listener
needed to later reactivate it once every panel closes — permanently
stuck off. **Fixed** by moving that subscription to `Awake`/`OnDestroy`.

The general lesson, not specific to input: **never bind/unbind an event
in `OnEnable`/`OnDisable` on a component whose own behaviour includes
deactivating the GameObject it lives on.** Use `Awake`/`OnDestroy` for
that specific shape of component instead.

## HUD initial state: explicit `_panels` list, scanned in `Awake`

`HudVisibilityController` does not infer "no panels active" purely from
having received no broadcasts yet — that would silently assume every
panel starts inactive in the scene, which isn't guaranteed. Instead, it
takes an explicit `List<GameObject> _panels` (filled in the Inspector) and
scans each one's `activeSelf` in `Awake`, seeding `_activePanels`
correctly even if a panel was left active by mistake at scene load.

Note: `_panels` only seeds the *initial* state — broadcasts for any
`GameObject` (not just ones in `_panels`) are still accepted afterward by
`HandlePanelVisibilityChanged`. If stray broadcasts from unrelated objects
ever become a concern, restricting `HandlePanelVisibilityChanged` to only
`GameObject`s present in `_panels` is a small, contained change.

## Directives

- Never move `HudVisibilityController`'s event subscription back to
  `OnEnable`/`OnDisable` without re-confirming the controller can never
  live on a GameObject that gets deactivated alongside the HUD it manages.
- Never assume all panels start inactive — always populate `_panels` on
  `HudVisibilityController` and let it scan real state in `Awake`, rather
  than reintroducing an implicit assumption.
- All panel activation/deactivation should go through
  `PanelVisibilityListener` (`Open`/`Close`/`Toggle`) or
  `InputActionPanelToggleTrigger`'s own `Trigger()` — never call
  `panel.SetActive(...)` directly from elsewhere in the codebase. Anything
  that bypasses `PanelVisibilityController.NotifyVisibilityChanged` is
  invisible to `HudVisibilityController` and will desync the HUD.

## Known limitations / open points

- No code-level enforcement of the "always go through the controller"
  directive above — a stray direct `SetActive` call elsewhere in the
  project would silently desync `HudVisibilityController`'s tracked state
  from the actual scene state.
- The accordion behaviour ("opening one panel closes any other") is
  implemented independently in both `InputActionPanelToggleTrigger` and
  `PanelVisibilityListener` (near-duplicated `HandleOtherPanelVisibilityChanged`
  logic) rather than factored into one shared helper — acceptable at the
  current scale (two call sites), but worth revisiting if a third variant
  is ever added.