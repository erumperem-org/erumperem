using UnityEngine;
using UnityEditor;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.View
{
    public class ConeShapeView : DetectionShapeView
    {
        public ConeShape data;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();
            Vector3 forward = transform.forward;
            float halfAngle = data.angle * 0.5f;

            Vector3 leftRay = Quaternion.Euler(0, -halfAngle, 0) * forward;
            Vector3 rightRay = Quaternion.Euler(0, halfAngle, 0) * forward;

            Handles.color = GetBorderColor();
            Handles.DrawLine(transform.position, transform.position + leftRay * data.distance);
            Handles.DrawLine(transform.position, transform.position + rightRay * data.distance);
            Handles.DrawWireArc(transform.position, Vector3.up, leftRay, data.angle, data.distance);

            if (drawSolid)
            {
                Handles.color = GetSolidColor();
                Handles.DrawSolidArc(transform.position, Vector3.up, leftRay, data.angle, data.distance);
            }

            if (showLabel)
            {
                string label = $"{shapeName}\nAngle: {data.angle}°\nDist: {data.distance:F1}";
                Handles.Label(transform.position + forward * (data.distance + labelOffset), label, _cachedLabelStyle);
            }
        }
#endif
    }
}