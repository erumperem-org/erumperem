using System.Collections;
using UnityEngine;

/// <summary>
/// Mantém a população perto do player: periodicamente mede a distância de cada chaser
/// e teleporta (<see cref="ChaserAI.Relocate"/>) para um ponto válido no anel de spawn
/// quem está vagando e passou de <see cref="ChaserSpawnSettings.returnDistance"/>.
/// Se nenhum ponto válido for achado, o chaser fica onde está e tenta de novo na próxima rodada.
/// </summary>
public class ChaserRelocator : MonoBehaviour
{
    [Header("Referências (vazio = procura no mesmo objeto / na cena)")]
    [SerializeField] private ChaserWorld world;
    [SerializeField] private ChaserSpawnSettings settings;
    [SerializeField] private ChaserPool pool;

    private ChaserPlacementRules rules;
    private Coroutine routine;

    private void OnEnable()
    {
        if (world == null) world = GetComponent<ChaserWorld>();
        if (world == null) world = FindAnyObjectByType<ChaserWorld>();
        if (pool == null) pool = GetComponent<ChaserPool>();

        if (world == null || settings == null || pool == null)
        {
            Debug.LogError("[ChaserRelocator] Falta ChaserWorld, ChaserSpawnSettings ou ChaserPool.", this);
            return;
        }

        rules = new ChaserPlacementRules(world, settings);
        routine = StartCoroutine(EvaluationLoop());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator EvaluationLoop()
    {
        var wait = new WaitForSeconds(settings.evaluationInterval);

        while (true)
        {
            yield return wait;
            EvaluateNow();
        }
    }

    /// <summary>Roda uma avaliação agora (também usado pelo botão do Inspector).</summary>
    public void EvaluateNow()
    {
        Transform target = world != null ? world.Target : null;
        if (target == null || rules == null)
        {
            return;
        }

        float returnSqr = settings.returnDistance * settings.returnDistance;
        var chasers = pool.GetManagedChasers();

        for (int i = 0; i < chasers.Count; i++)
        {
            ChaserAI chaser = chasers[i];

            // Só realoca quem está vagando: não cancela perseguição nem caçada em andamento.
            if (chaser == null || !chaser.CanBeRelocated)
            {
                continue;
            }

            if ((chaser.transform.position - target.position).sqrMagnitude <= returnSqr)
            {
                continue;
            }

            if (rules.TryFindPointAroundTarget(target, out Vector3 point))
            {
                chaser.Relocate(point);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Transform target = world != null ? world.Target : null;
        if (target == null || settings == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        DrawWireCircle(target.position, settings.spawnMinDistance);

        Gizmos.color = Color.yellow;
        DrawWireCircle(target.position, settings.spawnMaxDistance);

        Gizmos.color = Color.red;
        DrawWireCircle(target.position, settings.returnDistance);

        Gizmos.color = Color.cyan;
        DrawWireCircle(
            PlayerFieldOfViewApproximation.GetViewPoint(target, settings.fieldOfViewForwardOffset),
            settings.fieldOfViewRadius);
    }

    private static void DrawWireCircle(Vector3 center, float radius, int segments = 48)
    {
        float step = 360f / segments;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float rad = Mathf.Deg2Rad * step * i;
            Vector3 next = center + new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
#endif
}
