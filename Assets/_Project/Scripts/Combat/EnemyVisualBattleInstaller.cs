using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Compat: delega para <see cref="BattleVisualInstaller"/>.
    /// </summary>
    public static class EnemyVisualBattleInstaller
    {
        public static void ClearSlotForEnemyVisualPrefab(Transform slotRoot) =>
            BattleVisualInstaller.ClearSlotForBattlePrefab(slotRoot);

        public static Transform InstantiateEnemyUnderSlot(Transform slotRoot, GameObject battlePrefab) =>
            BattleVisualInstaller.InstantiateUnderSlot(slotRoot, battlePrefab);
    }
}
