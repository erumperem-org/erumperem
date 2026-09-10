using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pool de tamanho fixo de ChaserAI pré-existentes na cena. Mantém um
/// número configurável ativos ao redor do personagem Em Jogo atual (spawn
/// fora do HUB, fora dos limites do mapa, e fora de uma zona simplificada
/// de "campo de visão" à frente do player) e recolhe automaticamente
/// qualquer Chaser ativo que fique longe demais - teleportando-o para um
/// ponto de espera fixo e colocando-o em Resting até ser reaproveitado.
///
/// Nenhum ChaserAI é instanciado/destruído em runtime - todos já existem
/// na cena (arrastados no Inspector) e só alternam entre Resting e ativo.
/// </summary>
public class ChaserPool : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private List<ChaserAI> chasers = new List<ChaserAI>();
    [SerializeField] private PlayableCharacterController playableCharacterController;
    [SerializeField] private MapLimits mapLimits;
    [SerializeField] private SafeArea excludedArea;
    [SerializeField] private Transform holdingPoint;
    [SerializeField] private ChaserPoolSettings settings;

    private readonly HashSet<ChaserAI> activeChasers = new HashSet<ChaserAI>();
    private Transform currentTarget;
    private Coroutine evaluationRoutine;

    public int ActiveCount => activeChasers.Count;
    public int PoolSize => chasers.Count;

    private void OnEnable()
    {
        if (playableCharacterController != null)
        {
            playableCharacterController.OnCharacterEnteredInGame += HandleTargetChanged;
        }
    }

    private void OnDisable()
    {
        if (playableCharacterController != null)
        {
            playableCharacterController.OnCharacterEnteredInGame -= HandleTargetChanged;
        }

        if (evaluationRoutine != null)
        {
            StopCoroutine(evaluationRoutine);
            evaluationRoutine = null;
        }
    }

    private void Start()
    {
        // Todos começam recolhidos, parados no ponto de espera - a pool
        // decide quem ativar a partir da primeira avaliação.
        foreach (var chaser in chasers)
        {
            SendToPool(chaser);
        }

        currentTarget = playableCharacterController != null
            ? playableCharacterController.InGameCharacter?.transform
            : null;

        evaluationRoutine = StartCoroutine(EvaluationLoop());
    }

    private void HandleTargetChanged(PlayableCharacters newInGameCharacter)
    {
        currentTarget = newInGameCharacter.transform;

        // Atualiza TODOS os Chasers, ativos ou não - evita que um Chaser em
        // Resting seja reativado depois ainda apontando para um personagem
        // antigo, caso o player tenha trocado enquanto ele estava na pool.
        foreach (var chaser in chasers)
        {
            chaser.SetTarget(currentTarget);
        }
    }

    private IEnumerator EvaluationLoop()
    {
        var wait = new WaitForSeconds(Mathf.Max(0.1f, settings.evaluationInterval));

        while (true)
        {
            EvaluateReturns();
            EvaluateSpawns();
            yield return wait;
        }
    }

    private void EvaluateReturns()
    {
        if (currentTarget == null)
        {
            return;
        }

        var toReturn = new List<ChaserAI>();

        foreach (var chaser in activeChasers)
        {
            if (Vector3.Distance(chaser.transform.position, currentTarget.position) > settings.returnDistance)
            {
                toReturn.Add(chaser);
            }
        }

        foreach (var chaser in toReturn)
        {
            SendToPool(chaser);
        }
    }

    private void EvaluateSpawns()
    {
        if (currentTarget == null)
        {
            return;
        }

        while (activeChasers.Count < settings.desiredActiveCount)
        {
            ChaserAI candidate = GetNextRestingChaser();

            if (candidate == null)
            {
                break; // pool esgotada - nada mais para ativar
            }

            if (!TryFindSpawnPoint(out Vector3 spawnPoint))
            {
                break; // nenhum ponto válido nesta rodada - tenta de novo no próximo intervalo
            }

            ActivateChaser(candidate, spawnPoint);
        }
    }

    private ChaserAI GetNextRestingChaser()
    {
        foreach (var chaser in chasers)
        {
            if (!activeChasers.Contains(chaser))
            {
                return chaser;
            }
        }

        return null;
    }

    private void ActivateChaser(ChaserAI chaser, Vector3 spawnPoint)
    {
        chaser.transform.position = spawnPoint;
        chaser.SetTarget(currentTarget);
        chaser.ExitResting(); // sempre reativa em Wandering, nunca direto em Chasing
        activeChasers.Add(chaser);
    }

    private void SendToPool(ChaserAI chaser)
    {
        chaser.EnterResting(); // zera movimento e desliga percepção antes de teleportar
        chaser.transform.position = holdingPoint.position;
        activeChasers.Remove(chaser);
    }

    private bool TryFindSpawnPoint(out Vector3 point)
    {
        for (int i = 0; i < settings.maxSampleAttempts; i++)
        {
            Vector3 candidate = RandomPointAroundTarget();

            if (IsValidSpawnPoint(candidate))
            {
                point = candidate;
                return true;
            }
        }

        point = default;
        return false;
    }

    private Vector3 RandomPointAroundTarget()
    {
        float angle = Random.value * Mathf.PI * 2f;
        float distance = Random.Range(settings.spawnMinDistance, settings.spawnMaxDistance);

        Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 point = currentTarget.position + offset;
        point.y = currentTarget.position.y;
        return point;
    }

    private bool IsValidSpawnPoint(Vector3 point)
    {
        if (mapLimits != null && !mapLimits.Contains(point))
        {
            return false;
        }

        if (excludedArea != null && excludedArea.Contains(point))
        {
            return false;
        }

        return !IsInsidePlayerFieldOfView(point);
    }

    private bool IsInsidePlayerFieldOfView(Vector3 point)
    {
        Vector3 viewPoint = currentTarget.position + currentTarget.forward * settings.fieldOfViewForwardOffset;
        return Vector3.Distance(point, viewPoint) <= settings.fieldOfViewRadius;
    }

#if UNITY_EDITOR
    /// <summary>Uso exclusivo do editor de testes - força uma avaliação de retorno/spawn imediatamente.</summary>
    public void Editor_ForceEvaluate()
    {
        EvaluateReturns();
        EvaluateSpawns();
    }

    /// <summary>Uso exclusivo do editor de testes - recolhe todos os Chasers ativos para a pool.</summary>
    public void Editor_RecallAll()
    {
        var toRecall = new List<ChaserAI>(activeChasers);

        foreach (var chaser in toRecall)
        {
            SendToPool(chaser);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (currentTarget == null || settings == null)
        {
            return;
        }

        // Anel de spawn: verde = mínimo, amarelo = máximo
        Gizmos.color = Color.green;
        DrawWireCircle(currentTarget.position, settings.spawnMinDistance);

        Gizmos.color = Color.yellow;
        DrawWireCircle(currentTarget.position, settings.spawnMaxDistance);

        // Distância de retorno: vermelho
        Gizmos.color = Color.red;
        DrawWireCircle(currentTarget.position, settings.returnDistance);

        // Círculo de campo de visão: ciano, centrado à frente do player
        Vector3 viewPoint = currentTarget.position + currentTarget.forward * settings.fieldOfViewForwardOffset;
        Gizmos.color = Color.cyan;
        DrawWireCircle(viewPoint, settings.fieldOfViewRadius);
    }

    private static void DrawWireCircle(Vector3 center, float radius, int segments = 48)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angleRad = Mathf.Deg2Rad * angleStep * i;
            Vector3 nextPoint = center + new Vector3(Mathf.Cos(angleRad) * radius, 0f, Mathf.Sin(angleRad) * radius);
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
#endif
}