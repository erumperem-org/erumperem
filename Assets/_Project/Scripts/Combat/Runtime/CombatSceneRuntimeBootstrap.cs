using System.Collections;
using Erumperem.Characters;
using Erumperem.Combat;
using Services.DebugUtilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Garante catálogos e load context ao entrar na CombatScene vinda do Overworld.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public sealed class CombatSceneRuntimeBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SubscribeCombatSceneLoaded()
        {
            SceneManager.sceneLoaded -= HandleCombatSceneLoaded;
            SceneManager.sceneLoaded += HandleCombatSceneLoaded;

            var activeScene = SceneManager.GetActiveScene();
            if (IsCombatSceneName(activeScene.name))
            {
                HandleCombatSceneLoaded(activeScene, LoadSceneMode.Single);
            }
        }

        private static void HandleCombatSceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
        {
            if (!IsCombatSceneName(loadedScene.name))
            {
                return;
            }

            var existingBootstrap = Object.FindFirstObjectByType<CombatSceneRuntimeBootstrap>();
            if (existingBootstrap != null)
            {
                existingBootstrap.StartCoroutine(existingBootstrap.EnsureCombatServicesAfterSceneSettled());
                return;
            }

            var bootstrapHost = new GameObject("[Runtime] CombatSceneRuntimeBootstrap");
            var bootstrap = bootstrapHost.AddComponent<CombatSceneRuntimeBootstrap>();
            bootstrap.StartCoroutine(bootstrap.EnsureCombatServicesAfterSceneSettled());
        }

        private IEnumerator EnsureCombatServicesAfterSceneSettled()
        {
            yield return null;

            var activeScene = SceneManager.GetActiveScene();
            if (!IsCombatSceneName(activeScene.name))
            {
                yield break;
            }

            CombatOverworldFlowDiagnostics.LogPhase(
                "CombatSceneRuntimeBootstrap",
                "EnsureCombatServices (pós-sceneLoaded + 1 frame)");

            CombatOverworldFlowDiagnostics.LogActiveScene("CombatSceneRuntimeBootstrap.EnsureCombatServices");

            var allyCharacterStatCatalog = CombatCatalogLocator.ResolveAllyCharacterStatCatalog(null);
            CombatOverworldFlowDiagnostics.LogPhase(
                "CombatSceneRuntimeBootstrap",
                $"AllyCharacterStatCatalog={(allyCharacterStatCatalog != null ? allyCharacterStatCatalog.name : "null")}");

            ExplorationLoadContext.EnsureRuntimeInstance(allyCharacterStatCatalog);

            var combatPrototypeController = Object.FindFirstObjectByType<CombatPrototypeController>(
                FindObjectsInactive.Include);
            if (combatPrototypeController == null)
            {
                CombatOverworldFlowDiagnostics.LogError(
                    "CombatSceneRuntimeBootstrap",
                    "CombatPrototypeController não encontrado — confirma CombatSceneCore na cena");
                yield break;
            }

            CombatOverworldFlowDiagnostics.LogPhase(
                "CombatSceneRuntimeBootstrap",
                $"CombatPrototypeController encontrado em '{combatPrototypeController.gameObject.name}', " +
                $"enabled={combatPrototypeController.enabled}, " +
                $"activeInHierarchy={combatPrototypeController.gameObject.activeInHierarchy}");

            combatPrototypeController.EnsureCombatCatalogReferences();
            StartCoroutine(KickstartCombatSessionInitialization(combatPrototypeController));
            StartCoroutine(WaitForBattleSessionReadyOrLogFailure(combatPrototypeController));
        }

        private static IEnumerator KickstartCombatSessionInitialization(
            CombatPrototypeController combatPrototypeController)
        {
            yield return null;
            yield return null;

            if (combatPrototypeController == null)
            {
                yield break;
            }

            if (combatPrototypeController.IsBattleSessionReady)
            {
                yield break;
            }

            combatPrototypeController.TryBeginCombatSessionInitialization("CombatSceneRuntimeBootstrap");
        }

        private static IEnumerator WaitForBattleSessionReadyOrLogFailure(
            CombatPrototypeController combatPrototypeController)
        {
            const int maximumWaitFrames = 360;

            for (var frameIndex = 0; frameIndex < maximumWaitFrames; frameIndex++)
            {
                if (combatPrototypeController == null)
                {
                    yield break;
                }

                if (combatPrototypeController.IsBattleSessionReady)
                {
                    yield break;
                }

                yield return null;
            }

            if (combatPrototypeController != null && !combatPrototypeController.IsBattleSessionReady)
            {
                CombatOverworldFlowDiagnostics.LogActiveScene("CombatSceneRuntimeBootstrap TIMEOUT battle ready");
                CombatOverworldFlowDiagnostics.LogError(
                    "CombatSceneRuntimeBootstrap",
                    "combate não ficou pronto após load — ver fases CombatPrototypeController acima");
            }
            else if (combatPrototypeController != null)
            {
                CombatOverworldFlowDiagnostics.LogPhase(
                    "CombatSceneRuntimeBootstrap",
                    "IsBattleSessionReady=true");
            }
        }


        private static bool IsCombatSceneName(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName)
                && sceneName.IndexOf("Combat", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
