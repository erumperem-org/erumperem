using System;
using Erumperem.Characters;
using Erumperem.Combat.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenesManager : MonoBehaviour
{
    public static ScenesManager Instance { get; private set; }


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Cena de combate traz cópia local — remove o GO inteiro (handler incluído).
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (GetComponent<SceneTransitionHandler>() == null)
        {
            gameObject.AddComponent<SceneTransitionHandler>();
        }
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void LoadNextScene()
    {
        Time.timeScale = 1f;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            LoadMainMenu();
        }
    }

    public void LoadPreviousScene()
    {
        Time.timeScale = 1f;
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int previousSceneIndex = currentSceneIndex - 1;
        if (previousSceneIndex >= 0)
        {
            SceneManager.LoadScene(previousSceneIndex);
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        LoadSceneByName(sceneName, prepareExplorationStateBeforeCombatLoad: true);
    }

    public void LoadSceneByName(string sceneName, bool prepareExplorationStateBeforeCombatLoad)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[ScenesManager] Nome de cena vazio — load cancelado.");
            return;
        }

        Time.timeScale = 1f;

        if (IsCombatSceneName(sceneName)
            && EnemyHuntOrchestrator.BlockExternalCombatSceneLoads
            && !EnemyHuntOrchestrator.AllowOrchestratorCombatSceneLoad)
        {
            CombatOverworldFlowDiagnostics.LogWarning(
                "ScenesManager.LoadSceneByName",
                "load de combate via UnityEvent ignorado (orchestrator controla o load)");
            return;
        }

        if (IsCombatSceneName(sceneName))
        {
            CombatOverworldFlowDiagnostics.LogPhase(
                "ScenesManager.LoadSceneByName",
                $"combat load '{sceneName}', prepare={prepareExplorationStateBeforeCombatLoad}");

            if (!CombatSceneLoadCoordinator.TryBeginCombatSceneLoad(sceneName))
            {
                return;
            }
        }

        if (IsCombatSceneName(sceneName) && prepareExplorationStateBeforeCombatLoad)
        {
            PrepareExplorationStateBeforeCombatLoad();
        }

        SceneTransitionHandler.LoadScene(sceneName);
    }

    public static bool IsCombatSceneName(string sceneName)
    {
        return sceneName.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static void PrepareExplorationStateBeforeCombatLoad()
    {
        if (CombatExplorationBridge.TrySkipDuplicateCombatEntryPrepare())
        {
            return;
        }

        var allyCharacterStatCatalog = CombatCatalogLocator.ResolveAllyCharacterStatCatalog(null);
        var activeScene = SceneManager.GetActiveScene();
        var explorationSceneName = ExplorationSceneNames.IsOverworldExplorationScene(activeScene.name)
            ? activeScene.name
            : null;
        ExplorationLoadContext.EnsureRuntimeInstance(allyCharacterStatCatalog, explorationSceneName);
        CombatExplorationBridge.Instance?.NotifyEnteringCombat();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("_MainMenu");
    }

    public void LoadSceneByBuildIndex(int buildIndex)
    {
        SceneManager.LoadScene(buildIndex);
    }

    public String GetCurrentLevelName()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        return currentScene.name;
    }
}
