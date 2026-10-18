using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Entrada fiável no combate a partir do Overworld rework (Chaser / fantasma).
/// Não depende da cadeia de UnityEvents chegar ao <see cref="ScenesManager"/>.
/// </summary>
public static class OverworldCombatEntry
{
    public const string DefaultCombatSceneName = "CombatScene";

    public static void TryLoadCombatScene(
        string combatSceneName = DefaultCombatSceneName,
        bool prepareExplorationStateBeforeCombatLoad = true)
    {
        if (string.IsNullOrWhiteSpace(combatSceneName))
        {
            Debug.LogError("[OverworldCombatEntry] Nome de cena de combate vazio.");
            return;
        }

        CombatOverworldFlowDiagnostics.LogPhase(
            "OverworldCombatEntry",
            $"TryLoadCombatScene('{combatSceneName}', prepare={prepareExplorationStateBeforeCombatLoad})");

        if (ScenesManager.Instance != null)
        {
            ScenesManager.Instance.LoadSceneByName(combatSceneName, prepareExplorationStateBeforeCombatLoad);
            return;
        }

        if (ScenesManager.IsCombatSceneName(combatSceneName)
            && !CombatSceneLoadCoordinator.TryBeginCombatSceneLoad(combatSceneName))
        {
            return;
        }

        if (prepareExplorationStateBeforeCombatLoad)
        {
            Debug.LogWarning(
                "[OverworldCombatEntry] ScenesManager.Instance ausente — a preparar combate e carregar cena directamente.");

            ScenesManager.PrepareExplorationStateBeforeCombatLoad();
        }

        SceneTransitionHandler.LoadScene(combatSceneName);
    }
}
