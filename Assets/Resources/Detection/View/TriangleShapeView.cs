using UnityEngine;
using UnityEditor;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.View
{
    public class TriangleShapeView : DetectionShapeView
    {
        public TriangleShape data;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();
            Handles.color = GetBorderColor();
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Bordas
            Handles.DrawLine(data.a, data.b);
            Handles.DrawLine(data.b, data.c);
            Handles.DrawLine(data.c, data.a);

            if (drawSolid)
            {
                Handles.color = GetSolidColor();
                Handles.DrawAAConvexPolygon(data.a, data.b, data.c);
            }

            Handles.matrix = oldMatrix;

            if (showLabel)
            {
                // Calcula o centro do triângulo para o label
                Vector3 center = (data.a + data.b + data.c) / 3f;
                Vector3 worldCenter = transform.TransformPoint(center);
                
                string label = $"{shapeName}\nTriangle Area";
                Handles.Label(worldCenter + Vector3.up * labelOffset, label, _cachedLabelStyle);
            }
        }
#endif
    }
}