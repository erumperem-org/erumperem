using System.Collections.Generic;

/// <summary>
/// Cópia estática do estado capturado ao entrar em combate (sobrevive a recriação do
/// <see cref="ExplorationLoadContext"/> / <see cref="CombatExplorationBridge"/>).
/// </summary>
internal static class CombatEntrySnapshotCache
{
    private static List<PlayableCharacterSnapshot> _snapshots = new();
    private static float _corruptionValue;
    private static string _explorationSceneNameAtCombatEntry;

    public static bool HasSnapshots => _snapshots != null && _snapshots.Count > 0;

    public static void StoreFromExplorationLoadContext(ExplorationLoadContext loadContext)
    {
        if (loadContext == null || loadContext.SnapshotCountForCombatEntry <= 0)
        {
            return;
        }

        _snapshots = loadContext.CopySnapshotsForCombatEntryCache();
        _corruptionValue = loadContext.SavedCorruptionValueForCombatEntry;
        _explorationSceneNameAtCombatEntry = loadContext.ConfiguredExplorationSceneName;
    }

    public static void ApplyToExplorationLoadContextIfNeeded(ExplorationLoadContext loadContext)
    {
        if (loadContext == null || !HasSnapshots)
        {
            return;
        }

        if (loadContext.SnapshotCountForCombatEntry > 0)
        {
            return;
        }

        loadContext.RestoreSnapshotsFromCombatEntryCache(_snapshots, _corruptionValue);
    }

    public static string ExplorationSceneNameAtCombatEntry => _explorationSceneNameAtCombatEntry;

    public static void Clear()
    {
        _snapshots = new List<PlayableCharacterSnapshot>();
        _corruptionValue = 0f;
        _explorationSceneNameAtCombatEntry = null;
    }
}
