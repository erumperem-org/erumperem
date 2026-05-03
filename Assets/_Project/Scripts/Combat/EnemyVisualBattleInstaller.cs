using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Instancia o prefab de inimigo sob o slot de cena e devolve o root do instance (para <see cref="CombatPrototypeController"/> e <see cref="CombatCapsuleTag"/>).
    /// </summary>
    public static class EnemyVisualBattleInstaller
    {
        /// <summary>
        /// Remove filhos do slot e componentes de placeholder (cápsula/mesh no próprio slot).
        /// </summary>
        public static void ClearSlotForEnemyVisualPrefab(Transform slotRoot)
        {
            if (slotRoot == null)
            {
                return;
            }

            foreach (var combatCapsuleTag in slotRoot.GetComponents<CombatCapsuleTag>())
            {
                UnityEngine.Object.Destroy(combatCapsuleTag);
            }

            for (var childIndex = slotRoot.childCount - 1; childIndex >= 0; childIndex--)
            {
                var childTransform = slotRoot.GetChild(childIndex);
                UnityEngine.Object.Destroy(childTransform.gameObject);
            }

            foreach (var meshFilter in slotRoot.GetComponents<MeshFilter>())
            {
                UnityEngine.Object.Destroy(meshFilter);
            }

            foreach (var meshRenderer in slotRoot.GetComponents<MeshRenderer>())
            {
                UnityEngine.Object.Destroy(meshRenderer);
            }

            foreach (var capsuleCollider in slotRoot.GetComponents<CapsuleCollider>())
            {
                UnityEngine.Object.Destroy(capsuleCollider);
            }
        }

        public static Transform InstantiateEnemyUnderSlot(Transform slotRoot, GameObject battlePrefab)
        {
            if (slotRoot == null || battlePrefab == null)
            {
                return null;
            }

            // Mantém localPosition / localRotation / localScale do root do prefab (não forçar identidade).
            var instance = UnityEngine.Object.Instantiate(battlePrefab, slotRoot);
            return instance.transform;
        }
    }
}
