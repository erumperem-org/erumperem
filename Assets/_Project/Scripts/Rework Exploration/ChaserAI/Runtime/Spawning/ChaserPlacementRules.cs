using UnityEngine;

/// <summary>
/// Única fonte das regras de "onde um chaser pode aparecer": dentro dos limites do mapa,
/// fora da área segura e fora do campo de visão do player. Usada tanto no spawn inicial
/// quanto na realocação, para as duas nunca divergirem.
/// </summary>
public sealed class ChaserPlacementRules
{
    private readonly ChaserWorld world;
    private readonly ChaserSpawnSettings settings;

    public ChaserPlacementRules(ChaserWorld world, ChaserSpawnSettings settings)
    {
        this.world = world;
        this.settings = settings;
    }

    public bool IsValidPoint(Vector3 point, Transform target)
    {
        if (!world.IsInsideMap(point))
        {
            return false;
        }

        // Com margem: o mesmo limite que o chaser respeita ao andar, para não nascer na faixa proibida.
        if (world.IsInsideSafeAreaWithMargin(point))
        {
            return false;
        }

        return !PlayerFieldOfViewApproximation.IsInside(
            point, target, settings.fieldOfViewForwardOffset, settings.fieldOfViewRadius);
    }

    /// <summary>Sorteia pontos no anel ao redor do alvo até achar um válido.</summary>
    public bool TryFindPointAroundTarget(Transform target, out Vector3 point)
    {
        if (target != null)
        {
            float minSqr = settings.spawnMinDistance * settings.spawnMinDistance;
            float maxSqr = settings.spawnMaxDistance * settings.spawnMaxDistance;

            for (int i = 0; i < settings.maxSampleAttempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                // Raiz da interpolação dos quadrados = distribuição uniforme por área no anel.
                float distance = Mathf.Sqrt(Mathf.Lerp(minSqr, maxSqr, Random.value));

                Vector3 candidate = target.position +
                                    new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                candidate.y = target.position.y;

                if (IsValidPoint(candidate, target))
                {
                    point = candidate;
                    return true;
                }
            }
        }

        point = default(Vector3);
        return false;
    }
}
