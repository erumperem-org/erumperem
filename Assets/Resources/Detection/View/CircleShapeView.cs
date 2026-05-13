using UnityEngine;
using UnityEditor;

namespace DetectionSystem.View
{
    public class CircleShapeView : DetectionShapeView
    {
        public Vector3 center;
        public float radius = 1f;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();

            Vector3 worldCenter = transform.position + center;

            UnityEditor.Handles.color = GetBorderColor();
            UnityEditor.Handles.DrawWireDisc(worldCenter, Vector3.up, radius);

            if (drawSolid)
            {
                UnityEditor.Handles.color = GetSolidColor();
                UnityEditor.Handles.DrawSolidDisc(worldCenter, Vector3.up, radius);
            }

            if (showLabel)
            {
                string label = $"{shapeName}\nRadius: {radius:F1}";

                UnityEditor.Handles.Label(
                    worldCenter + Vector3.up * (radius + labelOffset),
                    label,
                    _cachedLabelStyle
                );
            }
        }
#endif
    }
}