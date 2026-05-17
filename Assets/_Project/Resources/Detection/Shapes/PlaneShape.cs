// PlaneShape.cs

using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class PlaneShape : IDetectionShape
    {
        public Vector2 size;
        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - center);
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z)<= size.y * 0.5f;
        }
    }
}