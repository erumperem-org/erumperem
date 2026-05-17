using UnityEngine;
using UnityEditor;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.View
{
    public class SphereShapeView : DetectionShapeView
    {
        public SphereShape data;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();
            UnityEditor.Handles.color = GetBorderColor();
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, data.radius);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, data.radius);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.right, data.radius);

            if (drawSolid)
            {
                Gizmos.color = GetSolidColor();
                Gizmos.DrawSphere(transform.position, data.radius);
            }

            if (showLabel)
            {
                string label = $"{shapeName}\nRadius: {data.radius:F1}";
                UnityEditor.Handles.Label(transform.position + Vector3.up * (data.radius + labelOffset), label, _cachedLabelStyle);
            }
        }
#endif
    }
}