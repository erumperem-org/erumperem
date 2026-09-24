using Erumperem.Combat;
using Services.DebugUtilities;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Logs de diagnóstico do fluxo REWORKING_Overworld → CombatScene (categoria Player).
/// </summary>
public static class CombatOverworldFlowDiagnostics
{
    private const string LogPrefix = "[COMBAT-FLOW]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void LogInitialActiveScene()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        LogActiveScene("AfterSceneLoad (runtime init)");
    }

    private static void HandleSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
    {
        LogActiveScene($"SceneManager.sceneLoaded (mode={loadSceneMode})");

        if (ScenesManager.IsCombatSceneName(loadedScene.name))
        {
            CombatSceneLoadCoordinator.NotifyCombatSceneLoadFinished(loadedScene);
        }
    }

    public static void LogPhase(string phase, string detail = null, Object context = null)
    {
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"{LogPrefix} {phase}"
            : $"{LogPrefix} {phase} — {detail}";

        LoggerService.PrintLogMessage(LogLevel.Debug, message, LogCategory.Player, context);
    }

    public static void LogWarning(string phase, string detail, Object context = null)
    {
        LoggerService.PrintLogMessage(
            LogLevel.Warning,
            $"{LogPrefix} {phase} — {detail}",
            LogCategory.Player,
            context);
    }

    public static void LogError(string phase, string detail, Object context = null)
    {
        LoggerService.PrintLogMessage(
            LogLevel.Error,
            $"{LogPrefix} {phase} — {detail}",
            LogCategory.Player,
            context);
    }

    public static void LogActiveScene(string phase)
    {
        var activeScene = SceneManager.GetActiveScene();
        var sceneSummary =
            $"cena='{activeScene.name}', buildIndex={activeScene.buildIndex}, " +
            $"isLoaded={activeScene.isLoaded}, rootCount={activeScene.rootCount}";

        LogPhase(phase, sceneSummary);

        var combatPrototypeControllers = Object.FindObjectsByType<CombatPrototypeController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        LogPhase(
            phase,
            $"CombatPrototypeController instâncias={combatPrototypeControllers.Length} " +
            $"(FindObjectsInactive.Include)");

        for (var controllerIndex = 0; controllerIndex < combatPrototypeControllers.Length; controllerIndex++)
        {
            var combatPrototypeController = combatPrototypeControllers[controllerIndex];
            if (combatPrototypeController == null)
            {
                continue;
            }

            var gameObject = combatPrototypeController.gameObject;
            LogPhase(
                phase,
                $"  [{controllerIndex}] GO='{gameObject.name}', " +
                $"scene='{gameObject.scene.name}', " +
                $"activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy}, " +
                $"controllerEnabled={combatPrototypeController.enabled}, " +
                $"battleReady={combatPrototypeController.IsBattleSessionReady}");
        }

        var bridgeParty = CombatExplorationBridge.Instance?.TryGetPendingCombatAllyCharacterNames();
        var partyLabel = bridgeParty == null || bridgeParty.Count == 0
            ? "(vazia)"
            : string.Join(", ", bridgeParty);

        var loadContext = ExplorationLoadContext.Instance;
        var snapshotCount = loadContext != null ? loadContext.SnapshotCountForCombatEntry : 0;

        LogPhase(
            phase,
            $"Bridge party={partyLabel}, ExplorationLoadContext snapshots={snapshotCount}, " +
            $"cache snapshots={(CombatEntrySnapshotCache.HasSnapshots ? "sim" : "não")}");
    }
}
