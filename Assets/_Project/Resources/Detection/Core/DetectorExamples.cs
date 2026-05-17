// ─────────────────────────────────────────────────────────────────────────────
// DetectorExamples.cs  —  Usage examples (not part of the runtime package)
// Delete this file if you don't need it in production.
// ─────────────────────────────────────────────────────────────────────────────
using UnityEngine;

namespace DetectionSystem.Core.Examples
{
    // =========================================================================
    // Pattern A — TickingDetector (auto scan, common case)
    // Simply swap Detector for TickingDetector in the Inspector.
    // =========================================================================

    // No code needed — add TickingDetector component and configure checkInterval.


    // =========================================================================
    // Pattern B — Detector + manual Scan() call (event-driven)
    // The scan only runs when something relevant actually happens.
    // =========================================================================

    /// <summary>
    /// A pressure plate that only scans when an animation event or trigger fires.
    /// No wasted CPU between activations.
    /// </summary>
    public class PressurePlate : Detector
    {
        // Called by an animation event, UnityEvent, or other external trigger
        public void CheckNow() => Scan();
    }


    // =========================================================================
    // Pattern C — Subclass Detector, override virtual hooks
    // Best when reaction logic lives on the same GameObject as the Detector.
    // =========================================================================

    public class EnemyDetector : TickingDetector
    {
        private EnemyAI _ai;

        void Start() => _ai = GetComponent<EnemyAI>();

        protected override void OnDetectionEnter(Collider detected, string shapeLabel, int shapeIndex)
        {
            base.OnDetectionEnter(detected, shapeLabel, shapeIndex);

            if (shapeLabel == "ViewCone" && detected.CompareTag("Player"))
                _ai.EnterAlertState(detected.transform);
        }

        protected override void OnDetectionExit(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (shapeLabel == "ViewCone" && detected.CompareTag("Player"))
                _ai.ReturnToPatrol();
        }
    }


    // =========================================================================
    // Pattern D — External event subscription
    // A manager reacts to a shared Detector without touching its GameObject.
    // =========================================================================

    public class TurretManager : MonoBehaviour
    {
        [SerializeField] private Detector turretDetector;

        void OnEnable()
        {
            turretDetector.OnDetectorEnter += OnTargetEntered;
            turretDetector.OnDetectorExit  += OnTargetExited;
        }

        void OnDisable()
        {
            turretDetector.OnDetectorEnter -= OnTargetEntered;
            turretDetector.OnDetectorExit  -= OnTargetExited;
        }

        private void OnTargetEntered(Collider target, string shapeLabel, int shapeIndex)
            => Debug.Log($"[Turret] {target.name} entrou em '{shapeLabel}' — atirar!");

        private void OnTargetExited(Collider target, string shapeLabel, int shapeIndex)
            => Debug.Log($"[Turret] {target.name} saiu de '{shapeLabel}' — cessar fogo.");
    }


    // =========================================================================
    // Pattern E — External system drives the scan (LOD / pooling / ECS bridge)
    // A central manager controls when each detector scans, e.g. throttling
    // distant detectors to save CPU.
    // =========================================================================

    public class DetectorLODManager : MonoBehaviour
    {
        [SerializeField] private Detector[] detectors;
        [SerializeField] private Transform  playerTransform;
        [SerializeField] private float      nearDistance   = 20f;
        [SerializeField] private float      nearInterval   = 0f;   // every frame
        [SerializeField] private float      farInterval    = 0.5f; // twice per second

        private float[] _timers;

        void Awake()
        {
            _timers = new float[detectors.Length];
        }

        void Update()
        {
            for (int i = 0; i < detectors.Length; i++)
            {
                _timers[i] -= Time.deltaTime;
                if (_timers[i] > 0f) continue;

                float dist     = Vector3.Distance(detectors[i].transform.position, playerTransform.position);
                float interval = dist < nearDistance ? nearInterval : farInterval;

                _timers[i] = interval;
                detectors[i].Scan();
            }
        }
    }


    // ─── Stubs so the file compiles without real AI code ─────────────────────
    internal class EnemyAI : MonoBehaviour
    {
        public void EnterAlertState(Transform target) { }
        public void ReturnToPatrol() { }
    }
}
