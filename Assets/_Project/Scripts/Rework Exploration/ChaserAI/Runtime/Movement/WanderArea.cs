using UnityEngine;

public class WanderArea : MonoBehaviour
{
    [Tooltip("Normalmente preenchido pelo ChaserWorld no Awake; só atribua aqui se usar o WanderArea sem ele.")]
    [SerializeField] private MapLimits mapLimits;
    [SerializeField] private CircularZone excludedArea;
    [SerializeField] private float fixedY = 0f;

    [Tooltip("Número máximo de tentativas de sortear um ponto válido antes de usar o fallback.")]
    [SerializeField] private int maxSampleAttempts = 30;

    [Tooltip("Distância extra somada ao raio da área excluída. Sobrescrita pelo ChaserWorld (única fonte da margem).")]
    [SerializeField] private float safeAreaMargin = 2f;

    private Vector3 lastValidPoint;
    private bool hasValidPoint;

    /// <summary>
    /// Recebe as zonas do <see cref="ChaserWorld"/>. Valores nulos mantêm o que já estava atribuído.
    /// </summary>
    public void Configure(MapLimits map, CircularZone excluded, float margin)
    {
        if (map != null)
        {
            mapLimits = map;
        }

        if (excluded != null)
        {
            excludedArea = excluded;
            safeAreaMargin = Mathf.Max(0f, margin);
        }
    }

    public Vector3 GetRandomPoint()
    {
        for (int i = 0; i < maxSampleAttempts; i++)
        {
            Vector3 candidate = RandomPointInMapLimits();

            if (!IsInsideExcludedAreaWithMargin(candidate))
            {
                lastValidPoint = candidate;
                hasValidPoint = true;
                return lastValidPoint;
            }
        }

        if (hasValidPoint)
        {
            return lastValidPoint;
        }

        Vector3 fallback = mapLimits.Center + new Vector3(mapLimits.Radius, 0f, 0f);
        fallback.y = fixedY;
        lastValidPoint = fallback;
        hasValidPoint = true;
        return fallback;
    }

    private Vector3 RandomPointInMapLimits()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float distance = mapLimits.Radius * Mathf.Sqrt(Random.value);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 point = mapLimits.Center + offset;
        point.y = fixedY;
        return point;
    }

    // Usa raio + margem em vez de excludedArea.Contains() diretamente, para
    // criar uma faixa de segurança ao redor da área excluída - o perseguidor
    // nunca sorteia um ponto de patrulha bem na borda dela.
    private bool IsInsideExcludedAreaWithMargin(Vector3 point)
    {
        if (excludedArea == null)
        {
            return false;
        }

        Vector3 delta = point - excludedArea.Center;
        delta.y = 0f;

        float effectiveRadius = excludedArea.Radius + safeAreaMargin;
        return delta.sqrMagnitude <= effectiveRadius * effectiveRadius;
    }
}