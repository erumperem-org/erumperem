// TriangleShape.cs

using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class TriangleShape : IDetectionShape
    {
        public Vector3 a, b ,c;
        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point)
        {
            Vector3 local =Quaternion.Inverse(rotation) * (point - center);
            Vector2 p = new Vector2(local.x, local.z);
            Vector2 av = new Vector2(a.x, a.z);
            Vector2 bv = new Vector2(b.x, b.z);
            Vector2 cv = new Vector2(c.x, c.z);
            float area = Cross(bv - av, cv - av);
            float s = Cross(cv - av, p - av) / area;
            float t = Cross(av - bv, p - bv) / area;
            float u = Cross(bv - cv, p - cv) / area;
            return s >= 0 && t >= 0 && u >= 0;
        }

        private float Cross(Vector2 a, Vector2 b) =>  a.x * b.y - a.y * b.x;
    }
}