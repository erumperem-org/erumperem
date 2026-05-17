using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class CylinderShape : IDetectionShape
    {
        public float radius, height;
        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - center);
            float radial = new Vector2(local.x, local.z).sqrMagnitude;
            return radial <= radius * radius && Mathf.Abs(local.y)<= height * 0.5f;
        }
    }
}