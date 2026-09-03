# Scene Object Allocation System (Unity)

A lightweight Unity system that randomly places prefabs into a set of scene
positions, with per-object scale/rotation randomization and a **balanced**
selection algorithm so that no single prefab dominates the distribution.

The core allocation call is an `async Task`, designed to be `await`-ed by a
centralized orchestration service that needs to guarantee execution order
across multiple scene-setup steps.

---

## 1. Overview

The system is made of four small, focused pieces:

| File | Responsibility |
|---|---|
| `PlaceableObjectData.cs` | A `ScriptableObject` describing one placeable object type: its prefab, and its scale/rotation randomization ranges. |
| `SceneObjectAllocationSystem.cs` | The allocator itself. Picks positions and object types, instantiates and randomizes instances, and keeps the pool balanced. |
| `AllocationResult.cs` | Plain data returned from an allocation call: which instances were placed, from which data, at which position. |
| `IObjectAllocationSystem.cs` | Interface the allocator implements, so a centralized service can depend on an abstraction (and mock it in tests). |
| `Examples/AllocationOrchestratorExample.cs` | Demo of a service that awaits multiple allocation calls in a guaranteed order. |

---

## 2. `PlaceableObjectData` (ScriptableObject)

Create instances via **Assets > Create > Scene Allocation > Placeable Object Data**.

Each asset exposes:

- **Prefab** — the `GameObject` to instantiate.
- **Min Scale / Max Scale** (`float` each) — a single uniform scale range. One
  value is rolled per placement and applied equally to X, Y and Z, so the
  object grows/shrinks as a whole without distorting its proportions.
- **Min Rotation / Max Rotation** (`Vector3` each) — independent random range per axis, in Euler degrees.

At placement time, `GetRandomScale()` and `GetRandomRotation()` are called to
produce the actual transform values. Scale is a single rolled value shared by
all three axes; rotation is still rolled independently per axis (X, Y and Z
are not correlated with each other).

The Inspector automatically clamps `max >= min` per axis (`OnValidate`), so you
can't accidentally end up with an inverted range.

---

## 3. `SceneObjectAllocationSystem` (the allocator)

Attach this `MonoBehaviour` to any GameObject in your scene (or manage it via
your own DI/service locator — it only depends on Unity's `Object.Instantiate`).

### Public API

```csharp
Task<AllocationResult> AllocateObjectsAsync(
    IReadOnlyList<PlaceableObjectData> objectPool,
    IReadOnlyList<Transform> availablePositions,
    CancellationToken cancellationToken = default);

void ResetBalancing();

IReadOnlyDictionary<PlaceableObjectData, int> GetUsageSnapshot();
```

- **`objectPool`** — the list of `PlaceableObjectData` to choose from for this call.
- **`availablePositions`** — the list of `Transform`s (typically empty marker
  GameObjects placed in the scene) that can each receive at most one object.
- **`cancellationToken`** — optional; checked between placements so a
  long-running allocation can be cancelled mid-way.
- Returns an `AllocationResult` (see below) once every position has been
  either filled or skipped.

### How allocation works, step by step

For every call to `AllocateObjectsAsync`:

1. A private copy of `availablePositions` is made (the caller's list is never mutated).
2. While there are still unfilled positions:
   1. A **position** is picked at random from the remaining ones and removed
      from the pool (so it can't be picked again in this call — this is what
      "if there is an available position" means in practice: only positions
      that haven't been consumed yet are ever candidates).
   2. An **object definition** is picked from `objectPool` using the
      *balanced weighted selection* described below.
   3. The prefab is instantiated at that position, with a random rotation and
      scale drawn from its own configured ranges.
   4. Usage statistics are updated for balancing purposes.
   5. Depending on configuration, the method may `await Task.Yield()` to hand
      control back to Unity before continuing (see "Async behaviour" below).
3. Once no positions remain, the method returns the accumulated `AllocationResult`.

If `objectPool` or `availablePositions` is null/empty, the call logs a warning
and returns an empty result instead of throwing — this keeps it safe to call
from fire-and-forget style setup code as well as from strictly ordered
orchestration code.

### `AllocationResult`

```csharp
public sealed class AllocationResult
{
    public List<PlacedObjectInfo> PlacedObjects { get; }
    public int RequestedCount { get; set; }
    public int PlacedCount { get; }        // PlacedObjects.Count
    public bool WasFullyAllocated { get; } // PlacedCount >= RequestedCount
}

public sealed class PlacedObjectInfo
{
    public GameObject Instance { get; }
    public PlaceableObjectData SourceData { get; }
    public Transform Position { get; }
}
```

This gives the caller full traceability: which prefab instance ended up at
which position, and from which `PlaceableObjectData` it was generated.

---

## 4. Balancing Algorithm

The goal: **selection must stay random**, but **no single object definition
should be picked far more often than the others** over the course of many
allocations.

### The formula

Each `PlaceableObjectData` in the pool gets a weight based on how many times
it has already been used by this allocator instance:

```
weight(item) = 1 / (usageCount(item) + 1) ^ balanceStrength
```

Then one item is picked via standard weighted random selection (roll a number
between `0` and the sum of all weights, walk the cumulative weights until you
pass the roll).

- `usageCount(item)` starts at `0` for every item and increments every time
  that item is placed.
- `balanceStrength` (Inspector slider, `0`–`2`, default `1`) controls how
  aggressively the algorithm compensates for uneven usage:
  - **`0`** → all weights become `1`, i.e. pure uniform random selection, no
    balancing at all.
  - **`~0.5`** → light balancing; popular items can still "win" a few times in
    a row, but long streaks become unlikely.
  - **`1`** (default) → weight is exactly inversely proportional to usage
    count; a well-balanced result over time without feeling mechanical.
  - **`2`** → strong balancing, close to strict round-robin, while still
    leaving room for randomness when usage counts are tied.

### Worked example

Pool: `A`, `B`, `C`. `balanceStrength = 1`.

| State | usage(A) | usage(B) | usage(C) | weight(A) | weight(B) | weight(C) |
|---|---|---|---|---|---|---|
| Start | 0 | 0 | 0 | 1.0 | 1.0 | 1.0 |
| After `A` picked | 1 | 0 | 0 | 0.5 | 1.0 | 1.0 |
| After `A` picked again | 2 | 0 | 0 | 0.33 | 1.0 | 1.0 |

Notice `A` becomes progressively less likely the more it's used, while `B`
and `C` (still unused) keep full weight — but `A` is never *impossible* to
pick again, it's just disfavored. This is what keeps the distribution
"reasonably balanced" without making it deterministic or removing randomness.

### Resetting balance

Usage counters live for the lifetime of the `SceneObjectAllocationSystem`
instance/component. Call `ResetBalancing()` when you want a clean slate (for
example, when loading a new level that should not be influenced by the
previous level's usage history).

### Inspecting balance

`GetUsageSnapshot()` returns a read-only copy of the current usage counts per
`PlaceableObjectData`, handy for debug overlays or editor tooling.

---

## 5. Async behaviour & execution order

`AllocateObjectsAsync` is an `async Task<AllocationResult>` so it can be
awaited by a centralized scene-setup service, guaranteeing that dependent
steps only run after allocation has actually completed:

```csharp
await allocationSystem.AllocateObjectsAsync(treePool, treePositions);
// The next line only runs once every tree position has been handled.
await allocationSystem.AllocateObjectsAsync(rockPool, rockPositions);
```

See `Examples/AllocationOrchestratorExample.cs` for a runnable version of this
pattern.

### Frame-spreading (optional)

Because `Object.Instantiate` must run on Unity's main thread, this system does
**not** offload work to a background thread. Instead, it can optionally spread
instantiation across multiple frames by yielding with `await Task.Yield()`
every `placementsPerFrame` placements (configurable in the Inspector, enabled
by default via `spreadAcrossFrames`). This avoids a single-frame spike when
allocating a large number of objects, while the call remains fully awaitable
and ordered from the caller's perspective — `await` still only returns once
**all** positions have been processed, regardless of how many frames were used
internally.

If you need every allocation to happen within a single frame (e.g. for a
deterministic test), set `spreadAcrossFrames = false`.

### Cancellation

Pass a `CancellationToken` if you need to abort a large allocation early (for
example, if the player leaves the scene mid-load). The token is checked once
per placement, so cancellation is prompt without needing to interrupt Unity's
`Instantiate` call itself.

---

## 6. Setup & usage

1. Create one `PlaceableObjectData` asset per object type you want to place
   (**Assets > Create > Scene Allocation > Placeable Object Data**), assign its
   prefab and configure scale/rotation ranges.
2. Add a `SceneObjectAllocationSystem` component to a GameObject in your scene.
3. Create your position markers (empty GameObjects) and collect their
   `Transform`s into a `List<Transform>`.
4. Call the allocator from your own setup/service code:

```csharp
public class MySceneSetup : MonoBehaviour
{
    [SerializeField] private SceneObjectAllocationSystem allocator;
    [SerializeField] private List<PlaceableObjectData> propPool;
    [SerializeField] private List<Transform> propPositions;

    private async void Start()
    {
        AllocationResult result = await allocator.AllocateObjectsAsync(propPool, propPositions);

        if (!result.WasFullyAllocated)
        {
            Debug.LogWarning($"Only {result.PlacedCount}/{result.RequestedCount} positions were filled.");
        }
    }
}
```

---

## 7. Design notes / extension points

- **Unity API calls stay on the main thread.** `AllocateObjectsAsync` does not
  use `Task.Run`; all Unity object creation happens on the calling
  (main) thread, with only `Task.Yield()` used to hand control back to Unity
  between batches. Do not wrap calls to this method in `Task.Run`.
- **Positions are Transforms, not raw Vector3s**, so you can drive
  placement from existing scene markers, snap points, spline samples, etc.,
  and reuse any rotation/parenting info already present on them if desired.
- **The allocator implements `IObjectAllocationSystem`**, so a centralized
  orchestration service can depend on the interface instead of the concrete
  `MonoBehaviour`, which also makes it straightforward to substitute a fake/mock
  implementation in edit-mode tests.
- **Balancing state is per-instance.** If you need independent balancing per
  category (e.g. "trees" vs "rocks" should each balance separately), use one
  `SceneObjectAllocationSystem` instance per category, or call
  `ResetBalancing()` between unrelated batches on a shared instance.
- **Object pooling is out of scope.** This system focuses on *selection and
  placement*; if you need instance pooling/recycling, wrap the
  `Instantiate` call site or post-process `AllocationResult.PlacedObjects`.