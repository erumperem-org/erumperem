using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class BoxShape : IDetectionShape
    {
        public Vector3 halfExtents = Vector3.one;
        public bool Contains(Vector3 center,Quaternion rotation,Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(rotation)* (point - center);
            return Mathf.Abs(local.x) <= halfExtents.x && Mathf.Abs(local.y) <= halfExtents.y && Mathf.Abs(local.z) <= halfExtents.z;
        }
    }
}