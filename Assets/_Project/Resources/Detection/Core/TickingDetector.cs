using UnityEngine;

namespace DetectionSystem.Core
{
    /// <summary>
    /// Convenience subclass of <see cref="Detector"/> that calls
    /// <see cref="Detector.Scan"/> automatically on every <c>Update</c>,
    /// with an optional minimum interval to reduce CPU cost.
    ///
    /// Use this when you want the familiar "always scanning" behaviour without
    /// writing the timing loop yourself.  For anything more specialised —
    /// FixedUpdate, event-driven, LOD-throttled, externally managed — use
    /// <see cref="Detector"/> directly and call <see cref="Detector.Scan"/>
    /// at the right moment.
    /// </summary>
    [AddComponentMenu("Detection/Ticking Detector")]
    public class TickingDetector : Detector
    {
        [Header("Timing")]
        [Tooltip("Minimum seconds between scans.  0 = every frame.")]
        [SerializeField, Min(0f)] private float checkInterval = 0f;

        private float _timer;

        protected override void Start()
        {
            base.Start();
            _timer = 0f; // scan immediately on the first frame
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            _timer = checkInterval;
            Scan();
        }
    }
}
