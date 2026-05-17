using UnityEngine;

namespace DetectionSystem.Core.Shapes
{
    [System.Serializable]
    public class TriangleShape : IDetectionShape
    {
        public Vector3 a, b, c;

        public bool Contains(Vector3 center, Quaternion rotation, Vector3 point)
        {
            Vector3 local = Quaternion.Inverse(rotation) * (point - center);
            Vector2 p  = new Vector2(local.x, local.z);
            Vector2 av = new Vector2(a.x, a.z);
            Vector2 bv = new Vector2(b.x, b.z);
            Vector2 cv = new Vector2(c.x, c.z);

            // Sign-only test: no division needed.
            // All three cross products must share the same sign for the point
            // to be inside (or on the boundary of) the triangle.
            float d0 = Cross(bv - av, p  - av);
            float d1 = Cross(cv - bv, p  - bv);
            float d2 = Cross(av - cv, p  - cv);

            bool hasNeg = (d0 < 0) || (d1 < 0) || (d2 < 0);
            bool hasPos = (d0 > 0) || (d1 > 0) || (d2 > 0);
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
