using UnityEngine;

/// <summary>
/// Equivalente de <see cref="CircularZone"/> para UI em Canvas Overlay:
/// define uma área circular ancorada a um RectTransform, com raio em
/// unidades locais (pixels na escala 1) e centro em anchoredPosition + um
/// offset ajustável. Mesma forma de uso de CircularZone/MapLimits, só que em
/// espaço de UI (X/Y) em vez de espaço de mundo (X/Z).
/// </summary>
[RequireComponent(typeof(RectTransform))]
public abstract class CircularUIZone : MonoBehaviour
{
    [SerializeField] protected float radius = 100f;
    [SerializeField] private Vector2 centerOffset = Vector2.zero;

    private RectTransform _rectTransform;
    private RectTransform RectTransformRef => _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

    /// <summary>Raio atual da zona, em unidades locais do RectTransform (pixels na escala 1).</summary>
    public float Radius => radius;

    /// <summary>Centro da zona, em coordenadas locais do RectTransform (anchoredPosition + offset).</summary>
    public virtual Vector2 Center => RectTransformRef.anchoredPosition + centerOffset;

    /// <summary>Testa se um ponto (no mesmo espaço local de Center) está dentro do círculo.</summary>
    public bool Contains(Vector2 localPosition)
    {
        Vector2 delta = localPosition - Center;
        return delta.sqrMagnitude <= radius * radius;
    }

    /// <summary>Cor do gizmo desta zona. Subclasses sobrescrevem para se diferenciar visualmente.</summary>
    protected virtual Color GizmoColor => Color.white;

    protected virtual void OnDrawGizmosSelected()
    {
        // RectTransforms vivem em espaço local 2D; desenha o círculo no
        // plano do próprio RectTransform em vez de assumir X/Z do mundo.
        Gizmos.color = GizmoColor;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = RectTransformRef.localToWorldMatrix;
        DrawWireCircle(centerOffset, radius);
        Gizmos.matrix = previousMatrix;
    }

    private static void DrawWireCircle(Vector2 center, float circleRadius, int segments = 48)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = (Vector3)center + new Vector3(circleRadius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angleRad = Mathf.Deg2Rad * angleStep * i;
            Vector3 nextPoint = (Vector3)center + new Vector3(
                Mathf.Cos(angleRad) * circleRadius,
                Mathf.Sin(angleRad) * circleRadius,
                0f);

            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}