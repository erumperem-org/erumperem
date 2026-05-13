using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DetectionSystem.Core
{
    /// <summary>
    /// Attach to any GameObject.  Configure a list of detection shapes in the
    /// Inspector, then call <see cref="Contains"/> or <see cref="GetAllContaining"/>
    /// at runtime to test whether a world-space point falls inside any (or all) of them.
    /// </summary>
    public class DetectionComponent : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────

        [Tooltip("List of detection shapes.  Each entry can be individually enabled, " +
                 "labelled, and have its own shape type and properties.")]
        [SerializeField]
        private List<ShapeEntry> shapes = new List<ShapeEntry>();

        [Header("Gizmos")]
        [Tooltip("Draw all enabled shapes in the Scene view.")]
        [SerializeField] private bool drawGizmos = true;

        [Tooltip("Draw labels in the Scene view.")]
        [SerializeField] private bool showLabels = true;

        // ── Public API ─────────────────────────────────────────────────

        /// <summary>Read-only access to the configured shape entries.</summary>
        public IReadOnlyList<ShapeEntry> Shapes => shapes;

        /// <summary>
        /// Returns true if <paramref name="point"/> is inside ANY enabled shape.
        /// </summary>
        public bool Contains(Vector3 point)
        {
            foreach (var entry in shapes)
                if (entry.enabled && entry.Contains(transform.position, transform.rotation, point))
                    return true;

            return false;
        }

        /// <summary>
        /// Returns true if <paramref name="point"/> is inside ALL enabled shapes.
        /// </summary>
        public bool ContainsAll(Vector3 point)
        {
            foreach (var entry in shapes)
                if (entry.enabled && !entry.Contains(transform.position, transform.rotation, point))
                    return false;

            return true;
        }

        /// <summary>
        /// Returns every <see cref="ShapeEntry"/> whose shape contains <paramref name="point"/>.
        /// The returned list is a new allocation; cache it if calling every frame.
        /// </summary>
        public List<ShapeEntry> GetAllContaining(Vector3 point)
        {
            var result = new List<ShapeEntry>();
            foreach (var entry in shapes)
                if (entry.enabled && entry.Contains(transform.position, transform.rotation, point))
                    result.Add(entry);

            return result;
        }

        /// <summary>
        /// Returns the index of the first enabled shape that contains <paramref name="point"/>,
        /// or -1 if none.
        /// </summary>
        public int IndexOf(Vector3 point)
        {
            for (int i = 0; i < shapes.Count; i++)
                if (shapes[i].enabled && shapes[i].Contains(transform.position, transform.rotation, point))
                    return i;

            return -1;
        }

        // ── Gizmos ─────────────────────────────────────────────────────

#if UNITY_EDITOR
        private GUIStyle _labelStyle;

        private void EnsureStyle()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize  = 11,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = Color.white;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || shapes == null) return;
            EnsureStyle();

            Quaternion rot = transform.rotation;

            for (int i = 0; i < shapes.Count; i++)
            {
                var entry = shapes[i];
                if (!entry.enabled) continue;

                Color c      = entry.gizmoColor;
                Color border = new Color(c.r, c.g, c.b, 1f);
                Color fill   = new Color(c.r, c.g, c.b, entry.solidAlpha);
                Vector3 center = entry.WorldCenter(transform.position, rot);

                DrawEntry(entry, center, rot, border, fill);
            }
        }

        private void DrawEntry(ShapeEntry entry, Vector3 center, Quaternion rot, Color border, Color fill)
        {
            switch (entry.shapeType)
            {
                case ShapeType.Sphere:   DrawSphere  (entry, center, rot, border, fill); break;
                case ShapeType.Box:      DrawBox     (entry, center, rot, border, fill); break;
                case ShapeType.Cylinder: DrawCylinder(entry, center, rot, border, fill); break;
                case ShapeType.Cone:     DrawCone    (entry, center, rot, border, fill); break;
                case ShapeType.Plane:    DrawPlane   (entry, center, rot, border, fill); break;
                case ShapeType.Triangle: DrawTriangle(entry, center, rot, border, fill); break;
            }
        }

        // ── per-shape draw helpers ─────────────────────────────────────

        private void DrawSphere(ShapeEntry e, Vector3 pos, Quaternion rot, Color border, Color fill)
        {
            float r = e.sphere.radius;
            Handles.color = border;
            Handles.DrawWireDisc(pos, Vector3.up,      r);
            Handles.DrawWireDisc(pos, Vector3.forward, r);
            Handles.DrawWireDisc(pos, Vector3.right,   r);

            if (e.solidAlpha > 0f)
            {
                Gizmos.color = fill;
                Gizmos.DrawSphere(pos, r);
            }

            if (showLabels)
                DrawLabel(pos + Vector3.up * (r + 0.25f), $"{e.label}\nRadius: {r:F2}", e.gizmoColor);
        }

        private void DrawBox(ShapeEntry e, Vector3 pos, Quaternion rot, Color border, Color fill)
        {
            Matrix4x4 old = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            Vector3 size = e.box.halfExtents * 2f;

            Gizmos.color = border;
            Gizmos.DrawWireCube(Vector3.zero, size);

            if (e.solidAlpha > 0f)
            {
                Gizmos.color = fill;
                Gizmos.DrawCube(Vector3.zero, size);
            }

            Gizmos.matrix = old;

            if (showLabels)
                DrawLabel(pos + rot * Vector3.up * (e.box.halfExtents.y + 0.25f),
                          $"{e.label}\nExtents: {e.box.halfExtents}", e.gizmoColor);
        }

        private void DrawCylinder(ShapeEntry e, Vector3 pos, Quaternion rot, Color border, Color fill)
        {
            Matrix4x4 old = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            float r = e.cylinder.radius;
            float h = e.cylinder.height;
            Vector3 top    = Vector3.up   * h * 0.5f;
            Vector3 bottom = Vector3.down * h * 0.5f;

            Handles.color = border;
            Handles.DrawWireDisc(top,    Vector3.up, r);
            Handles.DrawWireDisc(bottom, Vector3.up, r);
            Handles.DrawLine(top + Vector3.left    * r, bottom + Vector3.left    * r);
            Handles.DrawLine(top + Vector3.right   * r, bottom + Vector3.right   * r);
            Handles.DrawLine(top + Vector3.forward * r, bottom + Vector3.forward * r);
            Handles.DrawLine(top + Vector3.back    * r, bottom + Vector3.back    * r);

            if (e.solidAlpha > 0f)
            {
                Handles.color = fill;
                Handles.DrawSolidDisc(top,    Vector3.up, r);
                Handles.DrawSolidDisc(bottom, Vector3.up, r);
            }

            Handles.matrix = old;

            if (showLabels)
                DrawLabel(pos + rot * Vector3.up * (h * 0.5f + 0.25f),
                          $"{e.label}\nR: {r:F2}  H: {h:F2}", e.gizmoColor);
        }

        private void DrawCone(ShapeEntry e, Vector3 pos, Quaternion rot, Color border, Color fill)
        {
            Vector3 fwd       = rot * Vector3.forward;
            float   halfAngle = e.cone.angle * 0.5f;
            float   dist      = e.cone.distance;
            Vector3 left      = Quaternion.Euler(0, -halfAngle, 0) * fwd;
            Vector3 right     = Quaternion.Euler(0,  halfAngle, 0) * fwd;

            Handles.color = border;
            Handles.DrawLine(pos, pos + left  * dist);
            Handles.DrawLine(pos, pos + right * dist);
            Handles.DrawWireArc(pos, Vector3.up, left, e.cone.angle, dist);

            if (e.solidAlpha > 0f)
            {
                Handles.color = fill;
                Handles.DrawSolidArc(pos, Vector3.up, left, e.cone.angle, dist);
            }

            if (showLabels)
                DrawLabel(pos + fwd * (dist + 0.25f),
                          $"{e.label}\n{e.cone.angle}°  D: {dist:F2}", e.gizmoColor);
        }

        private void DrawPlane(ShapeEntry e, Vector3 pos, Quaternion rot, Color border, Color fill)
        {
            Matrix4x4 old = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
            Vector3 size = new Vector3(e.plane.size.x, 0, e.plane.size.y);

            Handles.color = border;
            Handles.DrawWireCube(Vector3.zero, size);

            if (e.solidAlpha > 0f)
            {
                float hx = e.plane.size.x * 0.5f;
                float hz = e.plane.size.y * 0.5f;
                Handles.color = fill;
                Handles.DrawAAConvexPolygon(
                    new Vector3(-hx, 0, -hz),
                    new Vector3(-hx, 0,  hz),
                    new Vector3( hx, 0,  hz),
                    new Vector3( hx, 0, -hz));
            }

            Handles.matrix = old;

            if (showLabels)
                DrawLabel(pos + Vector3.up * 0.25f,
                          $"{e.label}\n{e.plane.size.x:F1}×{e.plane.size.y:F1}", e.gizmoColor);
        }

        private void DrawTriangle(ShapeEntry e, Vector3 pos, Quaternion rot, Color border, Color fill)
        {
            Matrix4x4 old = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);

            Handles.color = border;
            Handles.DrawLine(e.triangle.a, e.triangle.b);
            Handles.DrawLine(e.triangle.b, e.triangle.c);
            Handles.DrawLine(e.triangle.c, e.triangle.a);

            if (e.solidAlpha > 0f)
            {
                Handles.color = fill;
                Handles.DrawAAConvexPolygon(e.triangle.a, e.triangle.b, e.triangle.c);
            }

            Handles.matrix = old;

            if (showLabels)
            {
                Vector3 localCenter = (e.triangle.a + e.triangle.b + e.triangle.c) / 3f;
                DrawLabel(pos + rot * localCenter + Vector3.up * 0.25f,
                          $"{e.label}\nTriangle", e.gizmoColor);
            }
        }

        private void DrawLabel(Vector3 worldPos, string text, Color color)
        {
            _labelStyle.normal.textColor = new Color(color.r, color.g, color.b, 1f);
            Handles.Label(worldPos, text, _labelStyle);
        }
#endif
    }
}
