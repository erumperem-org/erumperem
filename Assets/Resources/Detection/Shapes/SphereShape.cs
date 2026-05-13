using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class SphereShape : IDetectionShape
    {
        public float radius;
        public bool Contains(Vector3 center,Quaternion rotation,Vector3 point) => (point - center).sqrMagnitude <= radius * radius;  
    }
}