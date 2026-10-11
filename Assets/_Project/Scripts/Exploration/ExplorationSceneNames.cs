using System;

/// <summary>
/// Nomes de cenas de exploração reconhecidos pelo ciclo save/load e combate.
/// </summary>
public static class ExplorationSceneNames
{
    public const string LegacyOverworld = "Overworld";
    public const string LegacyOverworldMerge = "OverorldMerge";
    public const string ReworkingOverworld = "REWORKING_Overworld";

    public static bool IsOverworldExplorationScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return string.Equals(sceneName, LegacyOverworld, StringComparison.Ordinal)
            || string.Equals(sceneName, LegacyOverworldMerge, StringComparison.Ordinal)
            || string.Equals(sceneName, ReworkingOverworld, StringComparison.Ordinal);
    }

    public static bool MatchesConfiguredExplorationScene(string activeSceneName, string configuredSceneName)
    {
        if (string.IsNullOrWhiteSpace(activeSceneName) || string.IsNullOrWhiteSpace(configuredSceneName))
        {
            return false;
        }

        if (string.Equals(activeSceneName, configuredSceneName, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(configuredSceneName, LegacyOverworld, StringComparison.Ordinal)
            && string.Equals(activeSceneName, LegacyOverworldMerge, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
