using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Instancia prefabs de batalha (aliados ou inimigos) sob o slot de cena.
    /// </summary>
    public static class BattleVisualInstaller
    {
        public static void ClearSlotForBattlePrefab(Transform slotRoot)
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

            RemoveMeshAndColliderComponentsFromSlotRoot(slotRoot);
        }

        /// <summary>
        /// Instancia o prefab sob o slot. O prefab é criado desparentado primeiro;
        /// o slot só é limpo depois de instanciar com sucesso.
        /// </summary>
        public static Transform InstantiateUnderSlot(Transform slotRoot, GameObject battlePrefab)
        {
            if (slotRoot == null || battlePrefab == null)
            {
                return null;
            }

            GameObject instantiatedBattleVisual;
            try
            {
                instantiatedBattleVisual = UnityEngine.Object.Instantiate(battlePrefab);
            }
            catch (System.InvalidCastException exception)
            {
                Debug.LogError(
                    $"BattleVisualInstaller: prefab '{battlePrefab.name}' não é um GameObject instanciável " +
                    $"(use o root do prefab ou o GameObject interno, como nos inimigos). {exception.Message}",
                    battlePrefab);
                return null;
            }

            if (instantiatedBattleVisual == null)
            {
                Debug.LogError(
                    $"BattleVisualInstaller: Instantiate devolveu null para '{battlePrefab.name}'.",
                    battlePrefab);
                return null;
            }

            ClearSlotForBattlePrefab(slotRoot);

            var instantiatedTransform = instantiatedBattleVisual.transform;
            instantiatedTransform.SetParent(slotRoot, false);
            instantiatedTransform.localPosition = Vector3.zero;
            instantiatedTransform.localRotation = Quaternion.identity;
            instantiatedTransform.localScale = Vector3.one;

            return instantiatedTransform;
        }

        private static void RemoveMeshAndColliderComponentsFromSlotRoot(Transform slotRoot)
        {
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
    }
}
