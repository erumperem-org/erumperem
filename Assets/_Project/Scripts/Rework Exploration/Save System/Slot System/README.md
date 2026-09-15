# Multi-Slot Save System — Unity / C#

A simple, decoupled system for managing **multiple save slots**, where each
slot corresponds to its own folder inside the device's
`Application.persistentDataPath`.

## Files

| File | Type | Role |
|---|---|---|
| `CurrentSlotData.cs` | `ScriptableObject` | Exposes the active slot for the current session (index + path). It's the "source of truth" other systems read from. |
| `SaveSlotManager.cs` | `MonoBehaviour` | Translates a slot number into a physical directory, creates the folder, and writes the result into `CurrentSlotData`. |
| `SaveSlotAccess.cs` | `MonoBehaviour` (singleton) | Global, read-only access point to the same `CurrentSlotData` asset — lets any code read `SaveSlotAccess.Data` by direct class reference, without dragging a scene reference into every consumer. |

---

## How it works

The flow is always the same, one-way:

Something calls SaveSlotManager.SelectSlot(N)
│
▼
Builds the path: Application.persistentDataPath/Slot 000
│
▼
Creates the folder on disk, if it doesn't exist yet
│
▼
Writes (index, path) into CurrentSlotData (ScriptableObject)
│
▼
Fires the CurrentSlotData.OnSlotChanged event
│
▼
Any save/load system, UI, autoload, etc. reads
CurrentSlotData.SlotDirectory to know WHERE to read/write


- `SaveSlotManager` is the **only** component that should call
  `currentSlotData.SetSlot(...)`. It's the "writer".
- `CurrentSlotData` is **read-only** for the rest of the game. Save
  systems, HUD, menu screens, etc. should only read
  `SlotIndex`, `SlotDirectory` and `HasSlotSelected` — never write to them
  directly.
- `SaveSlotAccess` is an optional, purely convenience layer on top of that
  read-only contract: it holds no logic of its own (no slot selection,
  creation, or deletion — that stays exclusive to `SaveSlotManager`), it
  just exposes the same `CurrentSlotData` asset through a static property
  so distant systems don't need a dragged-in Inspector reference just to
  read the current slot.
- This keeps the system decoupled: your save system (whatever
  serializes/deserializes the actual game data) doesn't need to know
  anything about slot logic — it just asks `CurrentSlotData` "what's the
  current folder?" and reads/writes files inside it.

### Format of the generated path

<Application.persistentDataPath>/Slot <N in 000 format>


The slot index is always formatted with 3 digits (`000`, `001`, `002`...),
using C#'s `slotIndex:000` format.

Examples (Windows):

C:\Users\User\AppData\LocalLow\CompanyName\GameName\Slot 000
C:\Users\User\AppData\LocalLow\CompanyName\GameName\Slot 001


The `"Slot "` prefix (with the trailing space already included) is
configurable in the Inspector (`slotFolderPrefix`), in case you prefer a
different name (`"Save "`, `"Profile "`, etc). The 3-digit formatting,
however, is fixed in code (`GetSlotPath`), so even if you change the
prefix the number will always come out as `000`, `001`, `002`...

---

## How to apply it to your project

1. **Create the ScriptableObject asset:**
   `Assets > Create > Save System > Current Slot Data`
   This generates a `.asset` file (e.g. `CurrentSlotData.asset`). **Only ONE
   instance of this asset should exist in the project** — it represents the
   global state of the active slot for the session.

2. **Add `SaveSlotManager`** to a GameObject that exists from game boot
   onward (e.g. a bootstrap/persistent object, or a `DontDestroyOnLoad` one).

3. **Drag the asset created in step 1** into the `Current Slot Data`
   field of `SaveSlotManager` in the Inspector.

4. **On "Continue" / "Choose Slot" screens:**
   - Use `SlotExistsOnDisk(N)` to know which slots already have a save, so
     you can show "New Game" or "Continue" on each button.
   - On confirmation, call `saveSlotManager.SelectSlot(N)`.

5. **In your save/load system** (not included here — this package only
   handles "which folder to use"), read the path like this:

```csharp
   [SerializeField] private CurrentSlotData currentSlotData;

   public void SaveGame(GameData data)
   {
       if (!currentSlotData.HasSlotSelected)
       {
           Debug.LogError("No slot selected.");
           return;
       }

       string filePath = Path.Combine(currentSlotData.SlotDirectory, "save.json");
       string json = JsonUtility.ToJson(data);
       File.WriteAllText(filePath, json);
   }
```

6. **(Optional) React to slot changes** by subscribing to the event:

```csharp
   private void OnEnable() => currentSlotData.OnSlotChanged += HandleSlotChanged;
   private void OnDisable() => currentSlotData.OnSlotChanged -= HandleSlotChanged;

   private void HandleSlotChanged(int index, string path)
   {
       // reload data, update UI, etc.
   }
```

7. **(Optional) Skip the dragged-in reference with `SaveSlotAccess`:**
   Add `SaveSlotAccess` to the same persistent GameObject as
   `SaveSlotManager` (or its own), and drag the **same** `CurrentSlotData`
   asset into its `Current Slot Data` field. From anywhere else in the
   code — no Inspector reference needed — you can then read:

```csharp
   var slotData = SaveSlotAccess.Data;

   if (slotData != null && slotData.HasSlotSelected)
   {
       string filePath = Path.Combine(slotData.SlotDirectory, "save.json");
       // ...
   }
```

   This is purely a convenience accessor — it does not select, create, or
   delete slots, and it does not replace `SaveSlotManager` as the single
   writer of `CurrentSlotData`. Use it when a dragged-in reference to
   `CurrentSlotData` would be impractical (e.g. a deeply nested or
   dynamically-instantiated system); prefer a normal `[SerializeField]`
   reference when that's convenient instead, to keep dependencies explicit
   in the Inspector.

---

## `SaveSlotAccess` details

- **Singleton, read-only.** Exposes `SaveSlotAccess.Instance` and the
  static shortcut `SaveSlotAccess.Data` (equivalent to
  `SaveSlotAccess.Instance.currentSlotData`, but null-safe if no instance
  exists yet).
- **Holds no slot logic.** No `SelectSlot`, `DeleteSlot`, `GetSlotPath`, or
  folder creation — all of that remains exclusive to `SaveSlotManager`.
  `SaveSlotAccess` only mirrors the reference to the same
  `CurrentSlotData` asset for global, code-only access.
- **Persists across scenes** via `DontDestroyOnLoad`, same as a typical
  bootstrap/persistent object.
- **Duplicate-safe**: if a second `SaveSlotAccess` is created (e.g. a
  bootstrap scene reloaded by mistake), the newer instance destroys itself
  and logs a warning, keeping the original `Instance` intact.
- **Must reference the same asset** dragged into `SaveSlotManager`'s
  `Current Slot Data` field — the two components point at the same
  `ScriptableObject`, they are not two separate sources of truth.

---

## Persistence: read this carefully

This is the most important point of the system and the reason it was
designed this way:

> **`CurrentSlotData` (i.e. "which slot is active right now") lives only
> in memory, for the current game session. It is NOT saved to disk and is
> reset (back to "no slot selected", index `-1`) every time the game is
> closed.**

In other words, there are **two completely different types of
persistence** here, and it's important not to confuse them:

| What | Where it lives | Survives closing the game? |
|---|---|---|
| **Save content** (progress, inventory, position, etc.) | Files inside `Slot 000/` on disk | ✅ Yes — you write it yourself via `File.WriteAllText`, `File.WriteAllBytes`, etc. |
| **Which slot is currently selected** (`CurrentSlotData`) | In memory (RAM) only, inside the ScriptableObject | ❌ No — resets on every run |

This applies equally whether you read it through `CurrentSlotData`
directly or through `SaveSlotAccess.Data` — the latter is just a different
way of *reaching* the same in-memory asset, it changes nothing about how
or when that asset resets.

In practice, this means: **every time the game is opened, the player has
to go through the slot selection screen again** (or the game defines a
default slot via `selectSlotOnAwake`/`defaultSlotIndex`) — the game never
"remembers on its own" which slot was used in the previous session, unless
you explicitly implement that (see the section below).

### Why it works this way (and doesn't persist on its own)

- In a **build** (compiled game), this would already be the natural
  behavior: every run of the game is a new process, so the
  `ScriptableObject` is reloaded from disk with its original values
  (`slotIndex = -1`).
- In the **Editor**, however, there's a trap: if the project is configured
  with `Edit > Project Settings > Editor > Enter Play Mode Options` and
  **Domain Reload disabled**, changes made to a ScriptableObject during
  Play Mode can "leak" and remain in effect the next time you enter Play
  Mode — like a fake, Editor-only persistence.
- That's why `CurrentSlotData` implements an automatic reset via
  `[RuntimeInitializeOnLoadMethod]`, which zeroes out `slotIndex` and
  `slotDirectory` on any loaded instance **before any scene starts**,
  guaranteeing the same behavior in both the Editor and builds.

### If you WANT the game to remember the last slot used

That's a separate design decision and isn't part of this system by default
(intentionally, to keep the behavior predictable: "every boot starts with
no slot"). If you want that behavior, the recommended approach is to write
the last used index to a simple file outside the slot folders (e.g.
`Application.persistentDataPath/last_slot.txt`) and, on boot, read that
file and call `SelectSlot(N)` manually — keeping the responsibility of
"remembering between sessions" outside of `CurrentSlotData`.

---

## Quick summary

- ✅ Multiple slots = multiple folders (`Slot 000`, `Slot 001`, `Slot 002`,
  ...) inside `Application.persistentDataPath`.
- ✅ `SaveSlotManager` is the one that creates the folder and decides the path.
- ✅ `CurrentSlotData` is the ScriptableObject other systems read to know
  where to read/write.
- ✅ `SaveSlotAccess` is an optional singleton for reaching that same
  `CurrentSlotData` by direct class reference, with no logic of its own.
- ⚠️ The **selected slot** is session state (RAM), it is not saved to disk,
  and it resets on every new game boot.
- ✅ The **save content itself** (whatever you write inside the slot's
  folder) persists normally, like any file on disk.