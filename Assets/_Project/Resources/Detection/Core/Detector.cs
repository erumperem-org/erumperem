using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Services.DebugUtilities;
#endif

namespace DetectionSystem.Core
{
    /// <summary>
    /// Base detector. Owns the <see cref="DetectionScanner"/>, exposes detector-side
    /// events and virtual hooks, and provides the public query API.
    ///
    /// <b>Does NOT scan on its own.</b> Call <see cref="Scan"/> manually, or use
    /// <see cref="TickingDetector"/> for the common Update/interval pattern.
    ///
    /// <b>How detection works:</b> on every <see cref="Scan"/>, the scanner issues
    /// Physics overlap queries for each shape and tests every collider it finds.
    /// No opt-in component is required — any collider that enters a shape volume
    /// and passes the layer/tag filter is detected automatically.
    ///
    /// <see cref="DetectionReceiver"/> is still supported: if a detected object has
    /// one, it will receive the Enter/Exit callbacks as before.
    /// </summary>
    [RequireComponent(typeof(DetectionComponent))]
    public class Detector : MonoBehaviour
    {
        // ── Components ─────────────────────────────────────────────────

        private DetectionComponent _detection;
        public DetectionComponent DetectionComponent { get => _detection; }

        // ── Core scanner (non-MonoBehaviour) ───────────────────────────

        private DetectionScanner _scanner;

        // ── Detector-side events ───────────────────────────────────────

        /// <summary>
        /// Fired on <b>this detector</b> when any collider enters one of its shapes.
        /// Parameters: (detectedCollider, shapeLabel, shapeIndex).
        /// </summary>
        public event Action<Collider, string, int> OnDetectorEnter;

        /// <summary>
        /// Fired on <b>this detector</b> when any collider exits one of its shapes.
        /// Parameters: (detectedCollider, shapeLabel, shapeIndex).
        /// </summary>
        public event Action<Collider, string, int> OnDetectorExit;

        // ── Unity lifecycle ────────────────────────────────────────────

        private void Awake()
        {
            _detection = GetComponent<DetectionComponent>();
        }

        protected virtual void OnEnable() => EnsureScannerInitialized();

        protected virtual void Start() => EnsureScannerInitialized();

        protected virtual void OnDestroy()
        {
            if (_scanner == null) return;
            _scanner.OnEnter -= HandleEnter;
            _scanner.OnExit -= HandleExit;
        }

        // ── Scan ───────────────────────────────────────────────────────

        /// <summary>
        /// Runs one full detection pass. Call this whenever your logic requires
        /// a scan — every frame, on FixedUpdate, on an external trigger, etc.
        /// </summary>
        public void Scan()
        {
            EnsureScannerInitialized();
            _scanner?.Tick(this);
        }

        private void EnsureScannerInitialized()
        {
            if (_scanner != null) return;

            if (_detection == null)
                _detection = GetComponent<DetectionComponent>();

            if (_detection == null) return;

            _scanner = new DetectionScanner(_detection.Shapes, transform);
            _scanner.OnEnter += HandleEnter;
            _scanner.OnExit += HandleExit;
        }

        // ── Detector-side reaction hooks ───────────────────────────────

        private void HandleEnter(Collider col, string shapeLabel, int shapeIndex)
        {
            OnDetectorEnter?.Invoke(col, shapeLabel, shapeIndex);
            OnDetectionEnter(col, shapeLabel, shapeIndex);
        }

        private void HandleExit(Collider col, string shapeLabel, int shapeIndex)
        {
            OnDetectorExit?.Invoke(col, shapeLabel, shapeIndex);
            OnDetectionExit(col, shapeLabel, shapeIndex);
        }

        /// <summary>Override to react when something enters a shape on this detector.</summary>
        protected virtual void OnDetectionEnter(Collider detected, string shapeLabel, int shapeIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[Detector] {name} ENTER '{detected.name}' → '{shapeLabel}' (index {shapeIndex})",
                LogCategory.Detection);
#endif
        }

        /// <summary>Override to react when something exits a shape on this detector.</summary>
        protected virtual void OnDetectionExit(Collider detected, string shapeLabel, int shapeIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[Detector] {name} EXIT  '{detected.name}' → '{shapeLabel}' (index {shapeIndex})",
                LogCategory.Detection);
#endif
        }

        // ── Utility queries ────────────────────────────────────────────

        /// <summary>True if <paramref name="col"/> was inside any shape on the last scan.</summary>
        public bool IsInsideAny(Collider col) => _scanner?.IsInsideAny(col) ?? false;

        /// <summary>True if <paramref name="col"/> was inside the shape at <paramref name="shapeIndex"/> on the last scan.</summary>
        public bool IsInside(Collider col, int shapeIndex) => _scanner?.IsInside(col, shapeIndex) ?? false;

        // ── Debug / inspection ─────────────────────────────────────────

        /// <summary>
        /// All colliders currently inside at least one shape, as of the last scan.
        /// Updated every <see cref="Scan"/> — safe to read in the Inspector or
        /// runtime debug overlays. Returns null before <c>Start</c>.
        /// </summary>
        public List<Collider> DetectedColliders => _scanner?.DetectedColliders;

        /// <summary>
        /// Per-shape collider lists, indexed by shape index.
        /// <c>DetectedPerShape[i]</c> contains every collider currently inside shape <c>i</c>.
        /// Returns null before <c>Start</c>.
        /// </summary>
        public List<List<Collider>> DetectedPerShape => _scanner?.DetectedPerShape;

        // ── Editor Gizmos ──────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || _scanner == null) return;

            var detected = _scanner.DetectedColliders;
            for (int c = 0; c < detected.Count; c++)
            {
                Collider col = detected[c];
                if (col == null) continue;

                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(col.bounds.center, 0.12f);
            }
        }
#endif
    }
}
