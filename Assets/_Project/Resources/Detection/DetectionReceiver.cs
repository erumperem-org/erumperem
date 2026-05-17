using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Services.DebugUtilities;
#endif

namespace DetectionSystem.Core
{
    /// <summary>
    /// Optional component for objects that want to react when a <see cref="Detector"/>
    /// detects them.
    ///
    /// <b>Detection no longer requires this component.</b> Any collider that enters a
    /// detector shape is detected automatically via Physics overlap queries. Adding this
    /// component simply provides a convenient per-object callback surface
    /// (<see cref="OnEnter"/> / <see cref="OnExit"/> / virtual overrides).
    ///
    /// The <see cref="Detector"/>'s scanner discovers this component automatically
    /// the first time the object is detected and caches it — no manual registration needed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DetectionReceiver : MonoBehaviour
    {
        /// <summary>Fired on this object when a detector's shape starts overlapping it.</summary>
        public System.Action<Detector, string, int> OnEnter;

        /// <summary>Fired on this object when a detector's shape stops overlapping it.</summary>
        public System.Action<Detector, string, int> OnExit;

        // ── Notifications (called by DetectionScanner) ─────────────────

        public void NotifyEnter(Detector detector, string shapeLabel, int shapeIndex)
        {
            OnEnter?.Invoke(detector, shapeLabel, shapeIndex);
            OnDetectionEnter(detector, shapeLabel, shapeIndex);
        }

        public void NotifyExit(Detector detector, string shapeLabel, int shapeIndex)
        {
            OnExit?.Invoke(detector, shapeLabel, shapeIndex);
            OnDetectionExit(detector, shapeLabel, shapeIndex);
        }

        /// <summary>Override to react when a detector enters this object.</summary>
        protected virtual void OnDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[Receiver] {name} ENTER '{shapeLabel}' (index {shapeIndex}) via {detector.name}",
                LogCategory.Detection);
#endif
        }

        /// <summary>Override to react when a detector exits this object.</summary>
        protected virtual void OnDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[Receiver] {name} EXIT  '{shapeLabel}' (index {shapeIndex}) via {detector.name}",
                LogCategory.Detection);
#endif
        }
    }
}
