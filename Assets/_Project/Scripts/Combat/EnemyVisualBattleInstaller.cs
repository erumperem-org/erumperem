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

        public static Transform InstantiateEnemyUnderSlot(
            Transform slotRoot,
            GameObject battlePrefab,
            Transform alliesFacingReference)
        {
            var instantiatedEnemyRoot = BattleVisualInstaller.InstantiateUnderSlot(slotRoot, battlePrefab);
            if (instantiatedEnemyRoot != null)
            {
                BattleVisualInstaller.OrientEnemyVisualTowardAllies(instantiatedEnemyRoot, alliesFacingReference);
            }

            return instantiatedEnemyRoot;
        }
    }
}
