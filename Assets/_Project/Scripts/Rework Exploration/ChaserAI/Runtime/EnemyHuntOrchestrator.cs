using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class EnemyHuntOrchestrator : MonoBehaviour
{
    private const int ExtraFramesAfterSaveInvoke = 3;
    private const int MaximumFramesWaitingForCombatScene = 900;

    internal static bool BlockExternalCombatSceneLoads { get; private set; }
    internal static bool AllowOrchestratorCombatSceneLoad { get; set; }

    [SerializeField] private ChaserAI[] chasers;

    [Tooltip("Saves apenas — não ligue LoadScene aqui.")]
    public UnityEvent OnPreyCaught;

    [SerializeField] private string _combatSceneName = OverworldCombatEntry.DefaultCombatSceneName;
    [SerializeField] private float _delayBeforeCombatLoadSeconds = 0.35f;
    [SerializeField] private bool _clearLocalSavesBeforeCombatEntry;

    private bool _hasTriggeredCombatEntry;
    private readonly List<ChaserAI> _subscribedChasers = new List<ChaserAI>();

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoadedForCombatEntryReset;
        ResubscribeToAllManagedChasers();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoadedForCombatEntryReset;
        BlockExternalCombatSceneLoads = false;
        AllowOrchestratorCombatSceneLoad = false;
        UnsubscribeFromAllChasers();
    }

    private void HandleSceneLoadedForCombatEntryReset(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        if (ExplorationSceneNames.IsOverworldExplorationScene(loadedScene.name))
        {
            ResetOverworldCombatEntryStateForRetry();
        }
    }

    private void ResubscribeToAllManagedChasers()
    {
        UnsubscribeFromAllChasers();

        foreach (var chaser in BuildManagedChaserList())
        {
            if (chaser == null)
            {
                continue;
            }

            chaser.OnTargetCaught += HandlePreyCaught;
            _subscribedChasers.Add(chaser);
        }

        if (_subscribedChasers.Count == 0)
        {
            CombatOverworldFlowDiagnostics.LogWarning(
                "EnemyHuntOrchestrator",
                "nenhum ChaserAI subscrito — captura não inicia combate",
                this);
        }
    }

    private void UnsubscribeFromAllChasers()
    {
        foreach (var chaser in _subscribedChasers)
        {
            if (chaser != null)
            {
                chaser.OnTargetCaught -= HandlePreyCaught;
            }
        }

        _subscribedChasers.Clear();
    }

    private IEnumerable<ChaserAI> BuildManagedChaserList()
    {
        var uniqueChasers = new List<ChaserAI>();

        if (chasers != null)
        {
            foreach (var serializedChaser in chasers)
            {
                if (serializedChaser != null && !uniqueChasers.Contains(serializedChaser))
                {
                    uniqueChasers.Add(serializedChaser);
                }
            }
        }

        var chaserPool = GetComponentInParent<ChaserPool>();
        if (chaserPool != null)
        {
            foreach (var pooledChaser in chaserPool.GetManagedChasers())
            {
                if (pooledChaser != null && !uniqueChasers.Contains(pooledChaser))
                {
                    uniqueChasers.Add(pooledChaser);
                }
            }
        }

        var hierarchyRoot = transform.parent != null ? transform.parent : transform;
        var chasersInHierarchy = hierarchyRoot.GetComponentsInChildren<ChaserAI>(true);
        foreach (var hierarchyChaser in chasersInHierarchy)
        {
            if (hierarchyChaser != null && !uniqueChasers.Contains(hierarchyChaser))
            {
                uniqueChasers.Add(hierarchyChaser);
            }
        }

        return uniqueChasers;
    }

    private void HandlePreyCaught()
    {
        if (_hasTriggeredCombatEntry)
        {
            return;
        }

        if (!CombatSceneLoadCoordinator.TryBeginOverworldCombatEntry())
        {
            CombatOverworldFlowDiagnostics.LogWarning(
                "EnemyHuntOrchestrator",
                "captura ignorada — entrada em combate já em curso (tenta após voltar ao overworld)",
                this);
            return;
        }

        _hasTriggeredCombatEntry = true;
        CombatOverworldFlowDiagnostics.LogPhase("Chaser captura", "a iniciar EnterCombatAfterOverworldSaves", this);
        StartCoroutine(EnterCombatAfterOverworldSaves());
    }

    private IEnumerator EnterCombatAfterOverworldSaves()
    {
        BlockExternalCombatSceneLoads = true;
        AllowOrchestratorCombatSceneLoad = false;
        CombatOverworldFlowDiagnostics.LogActiveScene("Orchestrator início");

        try
        {
            if (_clearLocalSavesBeforeCombatEntry)
            {
                CombatOverworldFlowDiagnostics.LogWarning(
                    "Orchestrator",
                    "clearLocalSavesBeforeCombatEntry está ligado no Inspector — ignorado para preservar dados de combate",
                    this);
            }

            CombatOverworldFlowDiagnostics.LogPhase("Orchestrator", "PrepareExplorationStateBeforeCombatLoad", this);
            ScenesManager.PrepareExplorationStateBeforeCombatLoad();
            CombatOverworldFlowDiagnostics.LogActiveScene("Orchestrator pós-prepare");

            try
            {
                CombatOverworldFlowDiagnostics.LogPhase("Orchestrator", "OnPreyCaught.Invoke (saves)", this);
                OnPreyCaught?.Invoke();
            }
            catch (Exception exception)
            {
                CombatOverworldFlowDiagnostics.LogError(
                    "Orchestrator OnPreyCaught",
                    exception.Message,
                    this);
            }

            for (var frameIndex = 0; frameIndex < ExtraFramesAfterSaveInvoke; frameIndex++)
            {
                yield return null;
            }

            if (_delayBeforeCombatLoadSeconds > 0f)
            {
                yield return new WaitForSeconds(_delayBeforeCombatLoadSeconds);
            }

            var loadContext = ExplorationLoadContext.Instance;
            if (loadContext != null)
            {
                CombatOverworldFlowDiagnostics.LogPhase("Orchestrator", "WaitUntilSaveOperationsCompleteAsync", this);
                yield return RunTaskAsCoroutine(loadContext.WaitUntilSaveOperationsCompleteAsync());

                CombatOverworldFlowDiagnostics.LogPhase("Orchestrator", "FlushExplorationSaveToDiskForCombatEntryAsync", this);
                yield return RunTaskAsCoroutine(loadContext.FlushExplorationSaveToDiskForCombatEntryAsync());
            }
            else
            {
                CombatOverworldFlowDiagnostics.LogWarning(
                    "Orchestrator",
                    "ExplorationLoadContext.Instance null antes do load de combate",
                    this);
            }

            if (ScenesManager.IsCombatSceneName(SceneManager.GetActiveScene().name))
            {
                CombatOverworldFlowDiagnostics.LogWarning(
                    "Orchestrator",
                    "já na cena de combate — load ignorado",
                    this);
                yield break;
            }

            AllowOrchestratorCombatSceneLoad = true;
            CombatOverworldFlowDiagnostics.LogPhase(
                "Orchestrator",
                $"TryLoadCombatScene('{_combatSceneName}', prepare=false)",
                this);

            OverworldCombatEntry.TryLoadCombatScene(
                _combatSceneName,
                prepareExplorationStateBeforeCombatLoad: false);

            while (SceneTransitionHandler.IsSceneLoadInProgress)
            {
                yield return null;
            }

            yield return WaitUntilActiveSceneIsCombatScene();
            CombatOverworldFlowDiagnostics.LogActiveScene("Orchestrator pós-load combate");
        }
        finally
        {
            AllowOrchestratorCombatSceneLoad = false;
            BlockExternalCombatSceneLoads = false;
            CombatSceneLoadCoordinator.EndOverworldCombatEntry();

            if (ExplorationSceneNames.IsOverworldExplorationScene(SceneManager.GetActiveScene().name))
            {
                ResetOverworldCombatEntryStateForRetry();
            }
        }
    }

    private void ResetOverworldCombatEntryStateForRetry()
    {
        _hasTriggeredCombatEntry = false;
        BlockExternalCombatSceneLoads = false;
        AllowOrchestratorCombatSceneLoad = false;
        CombatSceneLoadCoordinator.EndOverworldCombatEntry();

        foreach (var chaser in BuildManagedChaserList())
        {
            chaser?.ResetCatchStateForCombatRetry();
        }
    }

    private static IEnumerator RunTaskAsCoroutine(Task task)
    {
        if (task == null)
        {
            yield break;
        }

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            CombatOverworldFlowDiagnostics.LogError(
                "Orchestrator task",
                task.Exception?.GetBaseException().Message ?? "erro desconhecido");
        }
    }

    private static IEnumerator WaitUntilActiveSceneIsCombatScene()
    {
        for (var frameIndex = 0; frameIndex < MaximumFramesWaitingForCombatScene; frameIndex++)
        {
            if (ScenesManager.IsCombatSceneName(SceneManager.GetActiveScene().name))
            {
                yield break;
            }

            yield return null;
        }

        CombatOverworldFlowDiagnostics.LogWarning(
            "Orchestrator",
            "timeout à espera da CombatScene — verifica build settings / nome da cena");
    }
}
