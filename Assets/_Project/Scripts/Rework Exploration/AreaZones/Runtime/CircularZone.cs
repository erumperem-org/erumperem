using UnityEngine;

/// <summary>
/// Base para qualquer área circular no mundo (raio configurável, centro =
/// posição do transform). Existe para não duplicar "contém ponto" e desenho
/// de gizmo entre <see cref="MapLimits"/>, <see cref="SafeArea"/> e
/// qualquer outra zona circular que venha a existir.
///
/// A contenção ignora a altura (Y) - segue a mesma convenção do resto do
/// sistema de movimentação, que trabalha em X/Z porque o terreno não tem
/// variação de altura relevante.
/// </summary>
public abstract class CircularZone : MonoBehaviour
{
    [SerializeField] protected float radius = 10f;

    /// <summary>Raio atual da zona.</summary>
    public float Radius => radius;

    /// <summary>Centro da zona em coordenadas de mundo (posição do transform).</summary>
    public Vector3 Center => transform.position;

    /// <summary>Testa se um ponto do mundo está dentro do círculo (projeção em X/Z).</summary>
    public bool Contains(Vector3 worldPosition)
    {
        Vector3 delta = worldPosition - Center;
        delta.y = 0f;
        return delta.sqrMagnitude <= radius * radius;
    }

    /// <summary>Cor do gizmo desta zona. Subclasses sobrescrevem para se diferenciar visualmente.</summary>
    protected virtual Color GizmoColor => Color.white;

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = GizmoColor;
        DrawWireCircle(Center, radius);
    }

    private static void DrawWireCircle(Vector3 center, float circleRadius, int segments = 48)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + new Vector3(circleRadius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angleRad = Mathf.Deg2Rad * angleStep * i;
            Vector3 nextPoint = center + new Vector3(
                Mathf.Cos(angleRad) * circleRadius,
                0f,
                Mathf.Sin(angleRad) * circleRadius);

            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}
