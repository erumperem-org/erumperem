using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class ConeShape : IDetectionShape
    {
        public float distance, angle;

        // ── Cached values (rebuilt when data changes) ──────────────────
        // Avoids Mathf.Cos + Deg2Rad + two sqrts per Contains call.
        private float _cachedAngle   = float.NaN;
        private float _cachedCosHalf;          // cos(angle/2) in radians
        private float _cachedDistSq;           // distance²

        private void RebuildCache()
        {
            _cachedAngle   = angle;
            _cachedCosHalf = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            _cachedDistSq  = distance * distance;
        }

        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point)
        {
            // Rebuild cache only when angle or distance changes (editor tweaks / init).
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_cachedAngle != angle || _cachedDistSq != distance * distance)
                RebuildCache();

            Vector3 dir = point - center;

            // Early-out: outside the bounding sphere.
            if (dir.sqrMagnitude > _cachedDistSq)
                return false;

            // forward is already unit-length (rotation * unit vector).
            Vector3 forward = rotation * Vector3.forward;

            // dot of two normalized vectors — avoid sqrt via divide-by-magnitude.
            float dirMag = dir.magnitude;
            if (dirMag < 1e-6f) return true;            // point is at apex

            float dot = Vector3.Dot(forward, dir) / dirMag;
            return dot >= _cachedCosHalf;
        }
    }
}
