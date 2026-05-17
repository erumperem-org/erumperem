using UnityEngine;
using UnityEditor;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.View
{
    public class CylinderShapeView : DetectionShapeView
    {
        public CylinderShape data;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();
            Handles.color = GetBorderColor();
            Matrix4x4 oldMatrix = Handles.matrix;
            // Alinha o desenho à rotação e posição do objeto
            Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            Vector3 top = Vector3.up * data.height * 0.5f;
            Vector3 bottom = Vector3.down * data.height * 0.5f;

            // Bordas (Wire)
            Handles.DrawWireDisc(top, Vector3.up, data.radius);
            Handles.DrawWireDisc(bottom, Vector3.up, data.radius);
            
            // Linhas laterais de conexão
            Handles.DrawLine(top + Vector3.left * data.radius, bottom + Vector3.left * data.radius);
            Handles.DrawLine(top + Vector3.right * data.radius, bottom + Vector3.right * data.radius);
            Handles.DrawLine(top + Vector3.forward * data.radius, bottom + Vector3.forward * data.radius);
            Handles.DrawLine(top + Vector3.back * data.radius, bottom + Vector3.back * data.radius);

            if (drawSolid)
            {
                Handles.color = GetSolidColor();
                // Preenchimento das tampas
                Handles.DrawSolidDisc(top, Vector3.up, data.radius);
                Handles.DrawSolidDisc(bottom, Vector3.up, data.radius);
            }

            Handles.matrix = oldMatrix;

            if (showLabel)
            {
                string label = $"{shapeName}\nRadius: {data.radius:F1}\nHeight: {data.height:F1}";
                Handles.Label(transform.position + transform.up * (data.height * 0.5f + labelOffset), label, _cachedLabelStyle);
            }
        }
#endif
    }
}