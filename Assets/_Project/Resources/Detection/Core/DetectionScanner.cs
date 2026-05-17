using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Services.DebugUtilities;
#endif

namespace DetectionSystem.Core
{
    /// <summary>
    /// Pure (non-MonoBehaviour) detection engine.
    ///
    /// Instead of relying on <see cref="DetectionReceiver"/> objects to self-register,
    /// every <see cref="Tick"/> actively queries the physics engine for ALL colliders
    /// overlapping each shape's bounding volume, then runs the precise per-shape
    /// <c>Contains</c> test. Any collider that passes the shape filter is detected —
    /// no <see cref="DetectionReceiver"/> component required.
    ///
    /// State machine: per-(collider × shape) Enter/Exit events fire only on transitions.
    /// </summary>
    [Serializable]
    public sealed class DetectionScanner
    {
        // ── Constructor args ───────────────────────────────────────────

        private readonly IReadOnlyList<ShapeEntry> _shapes;
        private readonly Transform _transform;

        // ── Physics overlap buffer (reused every tick, no GC) ──────────

        private static readonly Collider[] s_overlapBuffer = new Collider[256];

        // ── Per-(collider × shape) state ───────────────────────────────

        private readonly Dictionary<StateKey, bool> _state = new Dictionary<StateKey, bool>();

        // ── Receiver component cache ───────────────────────────────────

        /// <summary>Cached DetectionReceiver per collider instance-ID (null = has no receiver).</summary>
        private readonly Dictionary<int, DetectionReceiver> _receiverCache
            = new Dictionary<int, DetectionReceiver>();

        /// <summary>Collider reference cache: instance-ID → Collider (for exit callbacks).</summary>
        private readonly Dictionary<int, Collider> _colliderCache
            = new Dictionary<int, Collider>();

        // ── Per-tick working sets (fields to avoid per-tick allocation) ─

        /// <summary>Keys that were "inside" at the end of the PREVIOUS tick.</summary>
        private readonly HashSet<StateKey> _insideLastTick = new HashSet<StateKey>();

        /// <summary>Keys that are "inside" in the CURRENT tick (built each Tick call).</summary>
        private readonly HashSet<StateKey> _insideThisTick = new HashSet<StateKey>();

        // ── Debug / inspection lists ───────────────────────────────────

        /// <summary>
        /// All colliders currently inside at least one shape, as of the last tick.
        /// Updated every <see cref="Tick"/> — safe to read between ticks.
        /// Intended for the Inspector, runtime debug overlays, and unit tests.
        /// </summary>
        public readonly List<Collider> DetectedColliders = new List<Collider>();

        /// <summary>
        /// Per-shape lists of colliders currently inside, indexed by shape index.
        /// <c>DetectedPerShape[i]</c> contains every collider inside shape <c>i</c>.
        /// Populated after each <see cref="Tick"/>; count matches <c>Shapes.Count</c>.
        /// </summary>
        public readonly List<List<Collider>> DetectedPerShape = new List<List<Collider>>();

        // ── State key ──────────────────────────────────────────────────

        private readonly struct StateKey : IEquatable<StateKey>
        {
            public readonly int ColliderId;
            public readonly int ShapeIndex;

            public StateKey(int colliderId, int shapeIndex)
            {
                ColliderId = colliderId;
                ShapeIndex = shapeIndex;
            }

            public bool Equals(StateKey o) => ColliderId == o.ColliderId && ShapeIndex == o.ShapeIndex;
            public override bool Equals(object o) => o is StateKey k && Equals(k);
            public override int GetHashCode() => ColliderId * 397 ^ ShapeIndex;
        }

        // ── Detector-side events ───────────────────────────────────────

        /// <summary>Fired when a collider enters a shape. (collider, shapeLabel, shapeIndex)</summary>
        public event Action<Collider, string, int> OnEnter;

        /// <summary>Fired when a collider exits a shape. (collider, shapeLabel, shapeIndex)</summary>
        public event Action<Collider, string, int> OnExit;

        // ── Constructor ────────────────────────────────────────────────

        public DetectionScanner(IReadOnlyList<ShapeEntry> shapes, Transform transform)
        {
            _shapes = shapes ?? throw new ArgumentNullException(nameof(shapes));
            _transform = transform ?? throw new ArgumentNullException(nameof(transform));

            for (int i = 0; i < shapes.Count; i++)
                DetectedPerShape.Add(new List<Collider>());
        }

        // ── Tick ───────────────────────────────────────────────────────

        /// <summary>
        /// Runs one full detection pass using Physics overlap queries on every shape.
        /// Call from <c>Update</c>, <c>FixedUpdate</c>, or any external trigger.
        /// </summary>
        public void Tick(Detector ownerDetector)
        {
            int shapeCount = _shapes.Count;
            Vector3 detPos = _transform.position;
            Quaternion detRot = _transform.rotation;

            _insideThisTick.Clear();

            // ── Pass 1: find all colliders currently inside each shape ──
            for (int i = 0; i < shapeCount; i++)
            {
                var entry = _shapes[i];
                if (!entry.enabled) continue;

                Vector3 shapeCenter = entry.WorldCenter(detPos, detRot);
                int hitCount = OverlapShape(entry, shapeCenter, detRot);

                for (int h = 0; h < hitCount; h++)
                {
                    Collider col = s_overlapBuffer[h];
                    if (col == null) continue;

                    // Skip colliders belonging to the detector's own hierarchy
                    if (col.transform == _transform || col.transform.IsChildOf(_transform))
                        continue;

                    // Layer / tag filter
                    if (!entry.PassesFilter(col)) continue;

                    // Precise narrow-phase test (e.g. cylinder/cone need exact math)
                    if (!entry.Contains(detPos, detRot, col.bounds.center)) continue;

                    int colId = col.GetInstanceID();
                    CacheCollider(col, colId);

                    var key = new StateKey(colId, i);
                    _insideThisTick.Add(key);
                }
            }

            // ── Pass 2: fire ENTER for newly-inside keys ───────────────
            foreach (var key in _insideThisTick)
            {
                if (_insideLastTick.Contains(key)) continue;   // was already inside

                _state[key] = true;

                Collider col = _colliderCache.TryGetValue(key.ColliderId, out var c) ? c : null;
                DetectionReceiver recv = col != null ? GetOrCacheReceiver(col, key.ColliderId) : null;
                var entry = _shapes[key.ShapeIndex];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[Detection] ENTER {col?.name ?? $"#{key.ColliderId}"} → '{entry.label}' ({entry.shapeType})",
                    LogCategory.Detection);
#endif
                recv?.NotifyEnter(ownerDetector, entry.label, key.ShapeIndex);
                if (col != null) OnEnter?.Invoke(col, entry.label, key.ShapeIndex);
            }

            // ── Pass 3: fire EXIT for keys that were inside but aren't now
            foreach (var key in _insideLastTick)
            {
                if (_insideThisTick.Contains(key)) continue;   // still inside

                _state[key] = false;

                Collider col = _colliderCache.TryGetValue(key.ColliderId, out var c) ? c : null;
                DetectionReceiver recv = GetCachedReceiver(key.ColliderId);
                var entry = _shapes[key.ShapeIndex];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[Detection] EXIT  {col?.name ?? $"#{key.ColliderId}"} → '{entry.label}' ({entry.shapeType})",
                    LogCategory.Detection);
#endif
                recv?.NotifyExit(ownerDetector, entry.label, key.ShapeIndex);
                if (col != null) OnExit?.Invoke(col, entry.label, key.ShapeIndex);

                // Clean up cache for colliders that are no longer anywhere
                bool stillTrackedElsewhere = false;
                for (int i = 0; i < _shapes.Count; i++)
                {
                    if (i == key.ShapeIndex) continue;
                    if (_insideThisTick.Contains(new StateKey(key.ColliderId, i)))
                    {
                        stillTrackedElsewhere = true;
                        break;
                    }
                }
                if (!stillTrackedElsewhere)
                    _colliderCache.Remove(key.ColliderId);
            }

            // Swap for next tick — manual swap to avoid tuple deconstruct on readonly fields
            var tmp = _insideLastTick;
            _insideLastTick.Clear();
            foreach (var k in _insideThisTick) _insideLastTick.Add(k);
            _insideThisTick.Clear();

            // ── Rebuild debug lists from the fresh _insideLastTick ─────
            DetectedColliders.Clear();
            for (int i = 0; i < _shapes.Count; i++)
            {
                // Grow per-shape list if shapes were added at runtime
                while (DetectedPerShape.Count <= i)
                    DetectedPerShape.Add(new List<Collider>());
                DetectedPerShape[i].Clear();
            }

            foreach (var key in _insideLastTick)
            {
                if (!_colliderCache.TryGetValue(key.ColliderId, out Collider col) || col == null)
                    continue;

                DetectedPerShape[key.ShapeIndex].Add(col);

                // DetectedColliders: add once even if inside multiple shapes
                if (!DetectedColliders.Contains(col))
                    DetectedColliders.Add(col);
            }
        }

        // ── IsInside queries ───────────────────────────────────────────

        /// <summary>True if <paramref name="col"/> was inside any enabled shape on the last tick.</summary>
        public bool IsInsideAny(Collider col)
        {
            int id = col.GetInstanceID();
            for (int i = 0; i < _shapes.Count; i++)
                if (_state.TryGetValue(new StateKey(id, i), out bool v) && v)
                    return true;
            return false;
        }

        /// <summary>True if <paramref name="col"/> was inside the shape at <paramref name="shapeIndex"/> on the last tick.</summary>
        public bool IsInside(Collider col, int shapeIndex)
            => _state.TryGetValue(new StateKey(col.GetInstanceID(), shapeIndex), out bool v) && v;

        // ── Physics overlap helpers ────────────────────────────────────

        /// <summary>
        /// Runs the tightest-fitting <c>Physics.OverlapXxx</c> for the given shape's
        /// bounding volume. Returns hit count into <see cref="s_overlapBuffer"/>.
        /// Shapes without a native Unity overlap (Cylinder, Cone, Triangle) use a
        /// conservative bounding sphere — the exact <c>Contains</c> test in Pass 1
        /// eliminates false positives.
        /// </summary>
        private static int OverlapShape(ShapeEntry entry, Vector3 center, Quaternion rot)
        {
            const QueryTriggerInteraction triggers = QueryTriggerInteraction.Collide;

            switch (entry.shapeType)
            {
                case ShapeType.Sphere:
                    return Physics.OverlapSphereNonAlloc(
                        center, entry.sphere.radius,
                        s_overlapBuffer, Physics.AllLayers, triggers);

                case ShapeType.Box:
                    return Physics.OverlapBoxNonAlloc(
                        center, entry.box.halfExtents,
                        s_overlapBuffer, rot, Physics.AllLayers, triggers);

                case ShapeType.Cylinder:
                    // No native cylinder overlap — use bounding sphere
                    float cylR = Mathf.Max(entry.cylinder.radius, entry.cylinder.height * 0.5f);
                    return Physics.OverlapSphereNonAlloc(
                        center, cylR,
                        s_overlapBuffer, Physics.AllLayers, triggers);

                case ShapeType.Cone:
                    // Bounding sphere covers the entire cone from apex
                    return Physics.OverlapSphereNonAlloc(
                        center, entry.cone.distance,
                        s_overlapBuffer, Physics.AllLayers, triggers);

                case ShapeType.Plane:
                    // Thin box (height = 0.05 m) covering the plane surface
                    var planeHalf = new Vector3(entry.plane.size.x * 0.5f, 0.05f, entry.plane.size.y * 0.5f);
                    return Physics.OverlapBoxNonAlloc(
                        center, planeHalf,
                        s_overlapBuffer, rot, Physics.AllLayers, triggers);

                case ShapeType.Triangle:
                    // Bounding sphere around the three local-space vertices (transformed to world)
                    Vector3 lc = (entry.triangle.a + entry.triangle.b + entry.triangle.c) / 3f;
                    float triR = Mathf.Max(
                        Vector3.Distance(lc, entry.triangle.a),
                        Mathf.Max(
                            Vector3.Distance(lc, entry.triangle.b),
                            Vector3.Distance(lc, entry.triangle.c)));
                    return Physics.OverlapSphereNonAlloc(
                        center, triR,
                        s_overlapBuffer, Physics.AllLayers, triggers);

                default:
                    return 0;
            }
        }

        // ── Cache helpers ──────────────────────────────────────────────

        private void CacheCollider(Collider col, int id)
        {
            if (!_colliderCache.ContainsKey(id))
                _colliderCache[id] = col;
        }

        private DetectionReceiver GetOrCacheReceiver(Collider col, int id)
        {
            if (!_receiverCache.TryGetValue(id, out DetectionReceiver r))
            {
                col.TryGetComponent(out r);
                _receiverCache[id] = r;   // null is a valid cached value (means "has no receiver")
            }
            return r;
        }

        private DetectionReceiver GetCachedReceiver(int id)
        {
            _receiverCache.TryGetValue(id, out DetectionReceiver r);
            return r;
        }
    }
}
