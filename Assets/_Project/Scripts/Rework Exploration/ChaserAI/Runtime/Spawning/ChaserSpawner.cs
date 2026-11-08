using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera a população de chasers e os coloca perto do player, sempre fora da visão dele
/// (regras em <see cref="ChaserPlacementRules"/>). Mantê-los perto depois é trabalho do
/// <see cref="ChaserRelocator"/>.
///
/// Todos os chasers são criados de uma vez no Awake (população fixa; nada é criado ou
/// destruído depois), desativados. A ativação acontece aos poucos
/// (<see cref="ChaserSpawnSettings.placementsPerFrame"/> por frame) assim que o player
/// existe e há ponto válido. Criar tudo no Awake com ordem de execução antecipada
/// é o que garante que o EnemyHuntOrchestrator (que só se inscreve no OnEnable dele) veja
/// todos os chasers.
///
/// IMPORTANTE: os chasers nascem sob <c>spawnParent</c> (padrão: o pai deste objeto), que deve ser
/// o mesmo pai do EnemyHuntOrchestrator: é por varredura dessa hierarquia que ele os encontra.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(ChaserPool))]
public class ChaserSpawner : MonoBehaviour
{
    [Serializable]

    public class Archetype
    {
        [Tooltip("Prefixo do nome das instâncias na Hierarchy.")]
        public string label = "Chaser";

        [Tooltip("Prefab do chaser. Precisa ter ChaserAI na raiz; o spawner valida isso.")]
        public GameObject prefab;

        [Min(0)] public int count = 10;

        [Tooltip("Se ligado, estes chasers são forçados periodicamente a ir até a posição do player " +
                 "(se ele estiver fora da área segura).")]
        public bool periodicHunt;

        public PeriodicHuntSettings huntSettings = new PeriodicHuntSettings();
    }

    [Header("Referências")]
    [Tooltip("Se vazio, procura no mesmo objeto e depois na cena.")]
    [SerializeField] private ChaserWorld world;
    [SerializeField] private ChaserSpawnSettings settings;

    [Tooltip("Onde os chasers nascem. Vazio = pai deste objeto. Deve ser o pai do EnemyHuntOrchestrator.")]
    [SerializeField] private Transform spawnParent;

    [Header("População")]
    [SerializeField] private List<Archetype> archetypes = new List<Archetype>();

    private ChaserPool pool;
    private ChaserPlacementRules rules;
    private Transform parent;
    private readonly List<ChaserAI> pending = new List<ChaserAI>();

    /// <summary>Quantos chasers já foram criados.</summary>
    public int SpawnedCount => pool != null ? pool.PoolSize : 0;

    /// <summary>Quantos ainda aguardam um ponto válido / a existência do player.</summary>
    public int PendingCount => pending.Count;

    private void Awake()
    {
        pool = GetComponent<ChaserPool>();

        if (world == null)
        {
            world = GetComponent<ChaserWorld>();
        }

        if (world == null)
        {
            world = FindAnyObjectByType<ChaserWorld>();
        }

        if (world == null || settings == null)
        {
            Debug.LogError("[ChaserSpawner] Falta ChaserWorld ou ChaserSpawnSettings. Nenhum chaser será criado.", this);
            enabled = false;
            return;
        }

        parent = spawnParent != null ? spawnParent : (transform.parent != null ? transform.parent : transform);
        rules = new ChaserPlacementRules(world, settings);

        SpawnAll();
        RefreshOrchestratorSubscriptions();
    }

    private void Start()
    {
        StartCoroutine(PlacementLoop());
    }

    private void SpawnAll()
    {
        foreach (Archetype archetype in archetypes)
        {
            if (archetype == null || archetype.prefab == null || archetype.count <= 0)
            {
                continue;
            }

            // Filtro: o spawner só exige que o GameObject tenha um ChaserAI na raiz.
            if (!archetype.prefab.TryGetComponent(out ChaserAI _))
            {
                Debug.LogError($"[ChaserSpawner] O prefab '{archetype.prefab.name}' do arquétipo " +
                               $"'{archetype.label}' não tem ChaserAI na raiz. Arquétipo ignorado.", this);
                continue;
            }

            for (int i = 0; i < archetype.count; i++)
            {
                GameObject instance = Instantiate(archetype.prefab, parent);
                instance.name = $"{archetype.label} {i + 1}";
                instance.SetActive(false);

                ChaserAI chaser = instance.GetComponent<ChaserAI>();
                chaser.Initialize(world);

                if (archetype.periodicHunt)
                {
                    if (!instance.TryGetComponent(out PeriodicHuntBehaviour hunt))
                    {
                        hunt = instance.AddComponent<PeriodicHuntBehaviour>();
                    }

                    hunt.Configure(archetype.huntSettings);
                }

                pool.Register(chaser);
                pending.Add(chaser);
            }
        }

        if (pool.PoolSize == 0)
        {
            Debug.LogWarning("[ChaserSpawner] Nenhum arquétipo válido configurado (prefab vazio, sem ChaserAI ou count 0): nada foi criado.", this);
        }
        else
        {
            Debug.Log($"[ChaserSpawner] {pool.PoolSize} chasers criados (ficam inativos até haver player e ponto válido).", this);
        }
    }

    private IEnumerator PlacementLoop()
    {
        var retryWait = new WaitForSeconds(settings.placementRetryInterval);
        int activated = 0;
        float blockedSince = -1f;
        float nextWarnAfter = 5f;

        while (pending.Count > 0)
        {
            Transform target = world.Target;
            string blocker = null;
            int placed = 0;

            if (target == null)
            {
                blocker = "o ChaserWorld ainda não tem alvo (confira o PlayableCharacterController e se há " +
                          "um personagem Em Jogo)";
            }
            else
            {
                int failedSearches = 0;

                for (int i = pending.Count - 1; i >= 0 && placed < settings.placementsPerFrame; i--)
                {
                    ChaserAI chaser = pending[i];

                    if (chaser == null)
                    {
                        pending.RemoveAt(i);
                        continue;
                    }

                    if (!rules.TryFindPointAroundTarget(target, out Vector3 point))
                    {
                        failedSearches++;
                        continue;
                    }

                    // Um chaser com configuração inválida não pode travar os demais: sai da fila.
                    pending.RemoveAt(i);

                    if (TryActivate(chaser, point))
                    {
                        placed++;
                        activated++;
                    }
                }

                if (placed == 0 && failedSearches > 0)
                {
                    blocker = "nenhum ponto válido no anel de spawn (limites do mapa, área segura ou " +
                              "campo de visão do player descartam todos os candidatos; revise ChaserSpawnSettings e MapLimits)";
                }
            }

            if (blocker != null)
            {
                if (blockedSince < 0f)
                {
                    blockedSince = Time.unscaledTime;
                }

                float waited = Time.unscaledTime - blockedSince;
                if (waited >= nextWarnAfter)
                {
                    Debug.LogWarning($"[ChaserSpawner] {pending.Count} chasers aguardando há {waited:0}s: {blocker}.", this);
                    nextWarnAfter += 10f;
                }
            }
            else
            {
                blockedSince = -1f;
                nextWarnAfter = 5f;
            }

            if (placed > 0)
            {
                yield return null;
            }
            else
            {
                yield return retryWait;
            }
        }

        Debug.Log($"[ChaserSpawner] {activated} chasers ativos.", this);
    }

    private bool TryActivate(ChaserAI chaser, Vector3 point)
    {
        try
        {
            chaser.transform.position = point;
            chaser.gameObject.SetActive(true);
            chaser.Begin(point);

            if (chaser.HasBegun)
            {
                return true;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, chaser);
        }

        Debug.LogError($"[ChaserSpawner] {chaser.name} não pôde ser iniciado (veja o erro acima) e foi mantido desativado.", chaser);
        chaser.gameObject.SetActive(false);
        return false;
    }

    /// <summary>
    /// Rede de segurança: se o orquestrador de combate já tiver se inscrito antes de a população
    /// existir, reinicia a inscrição dele (liga/desliga o componente) para que ele veja todos.
    /// Só atua no spawn inicial; sem efeito se ele ainda nem foi habilitado.
    /// </summary>
    private void RefreshOrchestratorSubscriptions()
    {
        EnemyHuntOrchestrator orchestrator = FindAnyObjectByType<EnemyHuntOrchestrator>();

        if (orchestrator != null && orchestrator.isActiveAndEnabled)
        {
            orchestrator.enabled = false;
            orchestrator.enabled = true;
        }
    }
}
