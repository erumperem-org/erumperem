using UnityEngine.SceneManagement;

/// <summary>
/// Evita loads duplicados para a CombatScene (ex.: vários EnemyHuntOrchestrator).
/// </summary>
public static class CombatSceneLoadCoordinator
{
    private static bool _isCombatSceneLoadInFlight;
    private static bool _isOverworldCombatEntryInFlight;

    public static bool IsCombatSceneLoadInFlight => _isCombatSceneLoadInFlight;

    public static bool TryBeginOverworldCombatEntry()
    {
        if (_isOverworldCombatEntryInFlight)
        {
            CombatOverworldFlowDiagnostics.LogWarning(
                "CombatSceneLoadCoordinator",
                "entrada em combate já em curso — captura ignorada");
            return false;
        }

        _isOverworldCombatEntryInFlight = true;
        return true;
    }

    public static void EndOverworldCombatEntry()
    {
        _isOverworldCombatEntryInFlight = false;
    }

    public static bool TryBeginCombatSceneLoad(string sceneName)
    {
        if (!ScenesManager.IsCombatSceneName(sceneName))
        {
            return true;
        }

        if (_isCombatSceneLoadInFlight)
        {
            CombatOverworldFlowDiagnostics.LogWarning(
                "CombatSceneLoadCoordinator",
                $"load duplicado de '{sceneName}' bloqueado");
            return false;
        }

        _isCombatSceneLoadInFlight = true;
        return true;
    }

    public static void CancelCombatSceneLoadAttempt()
    {
        _isCombatSceneLoadInFlight = false;
    }

    public static void NotifyCombatSceneLoadFinished(Scene loadedScene)
    {
        _isCombatSceneLoadInFlight = false;

        if (ScenesManager.IsCombatSceneName(loadedScene.name)
            || ExplorationSceneNames.IsOverworldExplorationScene(loadedScene.name))
        {
            _isOverworldCombatEntryInFlight = false;
        }
    }
}
