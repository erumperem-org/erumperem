using UnityEngine;
using UnityEditor;
using DetectionSystem.Core.Shapes;

namespace DetectionSystem.View
{
    public class PlaneShapeView : DetectionShapeView
    {
        public PlaneShape data;

#if UNITY_EDITOR
        protected void OnDrawGizmos()
        {
            EnsureStyle();
            Handles.color = GetBorderColor();
            Matrix4x4 oldMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            Vector3 size = new Vector3(data.size.x, 0, data.size.y);
            Handles.DrawWireCube(Vector3.zero, size);

            if (drawSolid)
            {
                Handles.color = GetSolidColor();
                float halfX = data.size.x * 0.5f;
                float halfZ = data.size.y * 0.5f;

                Vector3[] verts = new Vector3[]
                {
                    new Vector3(-halfX, 0, -halfZ),
                    new Vector3(-halfX, 0,  halfZ),
                    new Vector3( halfX, 0,  halfZ),
                    new Vector3( halfX, 0, -halfZ)
                };
                Handles.DrawAAConvexPolygon(verts);
            }

            Handles.matrix = oldMatrix;

            if (showLabel)
            {
                string label = $"{shapeName}\nSize: {data.size.x}x{data.size.y}";
                Handles.Label(transform.position + Vector3.up * labelOffset, label, _cachedLabelStyle);
            }
        }
#endif
    }
}