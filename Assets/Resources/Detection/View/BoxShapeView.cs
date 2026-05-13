using UnityEngine;
using UnityEditor;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.View
{
    public class BoxShapeView : DetectionShapeView
    {
        public BoxShape data;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            Gizmos.color = GetBorderColor();
            Gizmos.DrawWireCube(Vector3.zero, data.halfExtents * 2f);

            if (drawSolid)
            {
                Gizmos.color = GetSolidColor();
                Gizmos.DrawCube(Vector3.zero, data.halfExtents * 2f);
            }

            Gizmos.matrix = oldMatrix;

            if (showLabel)
            {
                string label = $"{shapeName}\nExtents: {data.halfExtents}";
                UnityEditor.Handles.Label(transform.position + Vector3.up * (data.halfExtents.y + labelOffset), label, _cachedLabelStyle);
            }
        }
#endif
    }
}