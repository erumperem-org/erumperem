// ConeShape.cs

using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class ConeShape : IDetectionShape
    {
        public float distance, angle;
        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 dir = point - center;

            if (dir.sqrMagnitude > distance * distance)
            {
                return false;
            }

            float dot = Vector3.Dot(forward.normalized, dir.normalized);
            return dot >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        }
    }
}