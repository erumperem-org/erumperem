#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public class CircleGizmo : MonoBehaviour
{
    [Header("Circle Settings")]
    [SerializeField] private string circleName = "Detection Radius";

    [SerializeField] private float outerRadius = 5f;
    [SerializeField] private float innerRadius = 2f;

    [SerializeField] private Color circleColor = Color.green;

    [Header("Orientation")]
    [SerializeField] private Vector3 normal = Vector3.up;

    [Header("Display")]
    [SerializeField] private bool drawSolid = true;

    [Range(0f, 1f)]
    [SerializeField] private float solidAlpha = 0.15f;

    [Header("Label")]
    [SerializeField] private float labelAngle = 0f;
    [SerializeField] private float labelOffset = 0.25f;

    [Header("Rendering")]
    [SerializeField] private int segments = 64;

#if UNITY_EDITOR
    // FIX: Cache GUIStyle to avoid allocating a new one every Editor frame.
    private GUIStyle _cachedLabelStyle;

    private void OnDrawGizmos()
    {
        normal.Normalize();

        Vector3 center = transform.position;

        // Clamp
        innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius);

        Color borderColor = new Color(
            circleColor.r,
            circleColor.g,
            circleColor.b,
            1f
        );

        // -------- OUTER BORDER --------
        Handles.color = borderColor;
        Handles.DrawWireDisc(center, normal, outerRadius);

        // -------- INNER BORDER --------
        if (innerRadius > 0f)
        {
            Handles.DrawWireDisc(center, normal, innerRadius);
        }

        // -------- RING FILL --------
        if (drawSolid)
        {
            Color solidColor = new Color(
                circleColor.r,
                circleColor.g,
                circleColor.b,
                solidAlpha
            );

            Handles.color = solidColor;

            DrawRing(center, normal, innerRadius, outerRadius, segments);
        }

        // -------- LABEL POSITION --------
        float angleRad = labelAngle * Mathf.Deg2Rad;

        Vector3 localOffset = new Vector3(
            Mathf.Cos(angleRad),
            0f,
            Mathf.Sin(angleRad)
        ) * (outerRadius + labelOffset);

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
        Vector3 labelPosition = center + (rotation * localOffset);

        // FIX: Reuse cached style instead of allocating every frame.
        if (_cachedLabelStyle == null)
        {
            _cachedLabelStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
        }
        _cachedLabelStyle.normal.textColor = borderColor;

        string label =
            $"{circleName}\n" +
            $"Inner: {innerRadius:F1}\n" +
            $"Outer: {outerRadius:F1}";

        Handles.Label(labelPosition, label, _cachedLabelStyle);
    }

    private void DrawRing(
        Vector3 center,
        Vector3 normal,
        float innerRadius,
        float outerRadius,
        int segments)
    {
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);

        Vector3[] vertices = new Vector3[segments * 2];

        for (int i = 0; i < segments; i++)
        {
            float angle = ((float)i / segments) * Mathf.PI * 2f;

            Vector3 dir = new Vector3(
                Mathf.Cos(angle),
                0f,
                Mathf.Sin(angle)
            );

            vertices[i * 2]     = center + rotation * (dir * innerRadius);
            vertices[i * 2 + 1] = center + rotation * (dir * outerRadius);
        }

        for (int i = 0; i < segments; i++)
        {
            int current = i * 2;
            int next    = ((i + 1) % segments) * 2;

            Handles.DrawAAConvexPolygon(
                vertices[current],
                vertices[next],
                vertices[next + 1],
                vertices[current + 1]
            );
        }
    }
#endif
}
