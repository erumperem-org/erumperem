using Erumperem.Characters;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Garante serviços de exploração/combate (bridge + load context) quando a
/// cena Overworld rework carrega sem um <see cref="ExplorationLoadContext"/> na hierarquia.
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class OverworldExplorationBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExplorationServicesAfterSceneLoad()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!IsOverworldExplorationSceneName(activeScene.name))
        {
            return;
        }

        var sceneBootstrap = UnityEngine.Object.FindFirstObjectByType<OverworldExplorationBootstrap>();
        if (sceneBootstrap != null)
        {
            sceneBootstrap.EnsureExplorationServices();
            return;
        }

        if (ExplorationLoadContext.Instance != null)
        {
            var allyCatalog = FindAllyCharacterStatCatalog();
            ExplorationLoadContext.Instance.ConfigureForExplorationScene(activeScene.name, allyCatalog);
            return;
        }

        var bootstrapHost = new GameObject("[Runtime] OverworldExplorationBootstrap");
        bootstrapHost.AddComponent<OverworldExplorationBootstrap>();
    }

    private void Awake()
    {
        EnsureExplorationServices();
    }

    private void EnsureExplorationServices()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!IsOverworldExplorationSceneName(activeScene.name))
        {
            return;
        }

        var allyCharacterStatCatalog = FindAllyCharacterStatCatalog();
        var activeSceneName = activeScene.name;

        var sceneLoadContext = UnityEngine.Object.FindFirstObjectByType<ExplorationLoadContext>();
        if (sceneLoadContext != null)
        {
            sceneLoadContext.ConfigureForExplorationScene(activeSceneName, allyCharacterStatCatalog);
            return;
        }

        ExplorationLoadContext.EnsureRuntimeInstance(allyCharacterStatCatalog, activeSceneName);
    }

    private static bool IsOverworldExplorationSceneName(string sceneName) =>
        ExplorationSceneNames.IsOverworldExplorationScene(sceneName);

    private static AllyCharacterStatCatalog FindAllyCharacterStatCatalog()
    {
        var catalogInstances = Resources.FindObjectsOfTypeAll<AllyCharacterStatCatalog>();
        for (var catalogIndex = 0; catalogIndex < catalogInstances.Length; catalogIndex++)
        {
            var catalogCandidate = catalogInstances[catalogIndex];
            if (catalogCandidate != null)
            {
                return catalogCandidate;
            }
        }

        return null;
    }
}
