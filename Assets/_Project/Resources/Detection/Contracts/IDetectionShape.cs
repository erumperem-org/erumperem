using UnityEngine;

namespace DetectionSystem.Core
{
    public interface IDetectionShape
    {
        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point);
    }
}