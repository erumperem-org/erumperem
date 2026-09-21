using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Conjunto fixo de ChaserAI pré-existentes na cena, todos sempre ativos
/// (Wandering/Chasing/Investigating - nunca Resting). Periodicamente
/// verifica a distância de cada um até o personagem Em Jogo atual e
/// teleporta de volta, para um ponto num anel ao redor do player (fora de
/// MapLimits/SafeArea/campo de visão), qualquer Chaser que tenha se
/// afastado demais - permitindo que poucos ChaserAI deem a impressão de
/// povoar um mapa grande, sem nunca desligar movimento/percepção de
/// nenhum deles.
///
/// Nenhum ChaserAI é instanciado/destruído em runtime - todos já existem
/// na cena (arrastados no Inspector) e só são teleportados quando ficam
/// longe demais do alvo.
/// </summary>
public class ChaserPool : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private List<ChaserAI> chasers = new List<ChaserAI>();
    [SerializeField] private PlayableCharacterController playableCharacterController;
    [SerializeField] private MapLimits mapLimits;
    [SerializeField] private SafeArea excludedArea;
    [SerializeField] private ChaserPoolSettings settings;

    private Transform currentTarget;
    private Coroutine evaluationRoutine;

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
        currentTarget = playableCharacterController != null
            ? playableCharacterController.InGameCharacter?.transform
            : null;

        if (currentTarget != null)
        {
            foreach (var chaser in chasers)
            {
                chaser.SetTarget(currentTarget);
            }
        }

        evaluationRoutine = StartCoroutine(EvaluationLoop());
    }

    private void HandleTargetChanged(PlayableCharacters newInGameCharacter)
    {
        currentTarget = newInGameCharacter.transform;

        // Atualiza TODOS os Chasers - como não há mais distinção de
        // ativo/inativo, todos precisam apontar para o novo alvo
        // imediatamente (ChaserAI.SetTarget já repassa para o
        // PerceptionSensor na hora, sem esperar a próxima troca de estado).
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
            EvaluateRelocations();
            yield return wait;
        }
    }

    private void EvaluateRelocations()
    {
        if (currentTarget == null)
        {
            return;
        }

        var toRelocate = new List<ChaserAI>();

        foreach (var chaser in chasers)
        {
            if (chaser != null && Vector3.Distance(chaser.transform.position, currentTarget.position) > settings.returnDistance)
            {
                toRelocate.Add(chaser);
            }
        }

        foreach (var chaser in toRelocate)
        {
            if (TryFindSpawnPoint(out Vector3 spawnPoint))
            {
                chaser.Relocate(spawnPoint);
            }
            // Se nenhum ponto válido for encontrado nesta rodada, o Chaser
            // simplesmente continua onde está e tenta de novo na próxima
            // avaliação - não força um reposicionamento ruim.
        }
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

        return !PlayerFieldOfViewApproximation.IsInside(point, currentTarget, settings.fieldOfViewForwardOffset, settings.fieldOfViewRadius);
    }

#if UNITY_EDITOR
    /// <summary>Uso exclusivo do editor de testes - força uma avaliação de reposicionamento imediatamente.</summary>
    public void Editor_ForceEvaluate()
    {
        EvaluateRelocations();
    }

    private void OnDrawGizmosSelected()
    {
        if (currentTarget == null || settings == null)
        {
            return;
        }

        // Anel de reposicionamento: verde = mínimo, amarelo = máximo
        Gizmos.color = Color.green;
        DrawWireCircle(currentTarget.position, settings.spawnMinDistance);

        Gizmos.color = Color.yellow;
        DrawWireCircle(currentTarget.position, settings.spawnMaxDistance);

        // Distância que dispara o reposicionamento: vermelho
        Gizmos.color = Color.red;
        DrawWireCircle(currentTarget.position, settings.returnDistance);

        // Círculo de campo de visão: ciano, centrado à frente do player
        Vector3 viewPoint = PlayerFieldOfViewApproximation.GetViewPoint(currentTarget, settings.fieldOfViewForwardOffset);
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
