# Torch System

A Unity system that controls the overall "torches lit/unlit" state in the game,
with disk persistence and per-torch activation/deactivation of objects.

## File Structure

```
Assets/
├── Scripts/
│   ├── TorchManager.cs      # Central controller
│   └── Torch.cs              # Individual torch
└── Editor/
    ├── TorchManagerEditor.cs # Test buttons in TorchManager's Inspector
    └── TorchEditor.cs        # Test buttons in Torch's Inspector
```

> **Important:** the files `TorchManagerEditor.cs` and `TorchEditor.cs` **must**
> live inside a folder named `Editor` at any level of the project
> (e.g. `Assets/Editor/`). Editor scripts use the `UnityEditor` API, which
> doesn't exist in final builds — if they're left outside the `Editor` folder,
> Unity will try to include them in the build and compilation will fail.

---

## Overview

- **`TorchManager`**: a single object in the scene (singleton) that holds the
  overall state (`isTorchLit`), exposes the `OnTorchStateChange` event, and
  knows how to save/load that state to a file.
- **`Torch`**: a component placed on each torch in the scene. Each torch has
  its own list of objects to activate/deactivate and reacts automatically
  whenever `TorchManager` changes state.

The flow is: **something calls `TorchManager.SetTorchState(bool)` → the event
fires → each subscribed `Torch` reacts by activating/deactivating its own
objects → the new state is saved to disk.**

---

## TorchManager.cs

### Responsibilities
- Keep track of the current state (`isTorchLit`), **defaulting to `false` (unlit)**.
- Expose the `OnTorchStateChange(bool)` event for anything that wants to react to changes.
- Save and load that state to a JSON file, asynchronously.

### Configurable fields (Inspector)
| Field | Description |
|---|---|
| `saveDirectory` | Folder (relative to `Application.persistentDataPath`) where the save file lives. |
| `saveFileName` | Name of the save file (e.g. `torch_state.json`). |
| `isTorchLit` | Current state, visible/editable in the Inspector for debugging. |

### Main methods
- `SetTorchState(bool lit)` — changes the state, fires the event, and saves to disk.
- `Task SaveTorchStateAsync()` — orchestrates saving (delegates to the file-writing method).
- `Task LoadTorchStateAsync()` — reads the saved file and applies the state. If the
  file doesn't exist, is empty, or is invalid, it defaults to **unlit**.

### Saved file format
```json
{
  "isTorchLit": true
}
```

### The "null file" rule
If the save file doesn't exist (e.g. the game's first run), `LoadTorchStateAsync()`
doesn't throw: it simply defaults to `isTorchLit = false` and notifies subscribers
with that default value.

---

## Torch.cs

### Responsibilities
- Hold a list of `GameObject`s (`controlledObjects`): activated when the torch
  is **lit**, deactivated when it's **unlit**.
- Subscribe to the `TorchManager.OnTorchStateChange` event as soon as it's enabled (`OnEnable`).
- Immediately apply `TorchManager`'s current state upon subscribing (so objects
  don't sit in the wrong state until the next change).

### Execution order caution
Since `Torch` depends on `TorchManager.Instance`, there's a risk that
`TorchManager` hasn't run its `Awake()` yet when the torch tries to subscribe.
To handle this, `Torch.cs` uses a coroutine (`WaitForManagerRoutine`) that waits
for `Instance` to stop being `null` before subscribing, so this is handled
automatically — you don't need to worry about the order in which objects appear
in the scene.

### Test methods
- `TestActivate()` / `TestDeactivate()` — apply only **this torch's** objects,
  without depending on `TorchManager` being present or changing the overall
  game state. Useful for testing the list configuration in isolation.

---

## Inspector Test Buttons

When you add `TorchManager` or `Torch` to an object, the Inspector (in **Play
Mode**) shows extra buttons below the regular fields:

**TorchManager:**
- `Acender Tochas` / `Apagar Tochas` (Light Torches / Extinguish Torches) — calls
  `SetTorchState`, fires the event for every torch in the scene, and saves to disk.
- `Salvar Estado (Save)` / `Carregar Estado (Load)` (Save State / Load State) —
  tests persistence in isolation, without changing the in-memory current state
  (in the Save case) or overwriting it with whatever is in the file (in the
  Load case).

**Torch:**
- `Testar: Ativar` / `Testar: Desativar` (Test: Activate / Test: Deactivate) —
  tests only this specific torch's object list, without touching `TorchManager`.

Buttons are disabled outside of Play Mode, with a warning explaining why (the
system depends on `MonoBehaviour.Awake()`/`Start()`, which only run while the
game is running).

Besides the buttons, the same tests are available through the component's
**context menu** (the gear icon in the corner of the component in the
Inspector, or right-click on the component's name) — useful if you'd rather
not use the custom Editors.

---

## How to Use in Your Project

1. Create an empty `GameObject` in the scene (e.g. `[TorchManager]`) and add the
   `TorchManager` component. There should be **only one** per scene.
2. Configure `saveDirectory` and `saveFileName` if you want to change the defaults.
3. On each torch in the scene, add the `Torch` component.
4. Fill in `controlledObjects` with the objects that specific torch should
   control (light, fire particle, sound, sprite, etc).
5. Call `TorchManager.Instance.SetTorchState(true/false)` from wherever makes
   sense in your game (a puzzle button, a trigger, a collected item, etc).

---

## Possible Future Extensions
- Swap `JsonUtility` for another save format (binary, encrypted, cloud save).
- Expose a `Task SetTorchStateAsync(bool lit)` version in case you need to await
  the save finishing before continuing a flow (e.g. before a scene change).
- Add support for multiple independent "groups" of torches, in case the game
  needs more than one overall state at the same time.