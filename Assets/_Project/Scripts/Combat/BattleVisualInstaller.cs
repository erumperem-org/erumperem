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
        /// Instancia aliado preservando posição/rotação/escala locais do root do prefab (ex.: Buck Wyatt).
        /// </summary>
        public static Transform InstantiateAllyUnderSlot(Transform slotRoot, GameObject battlePrefab)
        {
            return InstantiateUnderSlot(slotRoot, battlePrefab, resetLocalTransform: false);
        }

        /// <summary>
        /// Instancia o prefab sob o slot. O prefab é criado desparentado primeiro;
        /// o slot só é limpo depois de instanciar com sucesso.
        /// </summary>
        public static Transform InstantiateUnderSlot(
            Transform slotRoot,
            GameObject battlePrefab,
            bool resetLocalTransform = true)
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

            CapturePrefabRootLocalTransform(battlePrefab, out var prefabLocalPosition, out var prefabLocalRotation, out var prefabLocalScale);

            var hierarchyRootTransform = ResolveInstantiatedHierarchyRoot(instantiatedBattleVisual.transform);

            ClearSlotForBattlePrefab(slotRoot);

            hierarchyRootTransform.SetParent(slotRoot, false);

            if (resetLocalTransform)
            {
                hierarchyRootTransform.localPosition = Vector3.zero;
                hierarchyRootTransform.localRotation = Quaternion.identity;
                hierarchyRootTransform.localScale = Vector3.one;
            }
            else
            {
                hierarchyRootTransform.localPosition = prefabLocalPosition;
                hierarchyRootTransform.localRotation = prefabLocalRotation;
                hierarchyRootTransform.localScale = prefabLocalScale;
            }

            return hierarchyRootTransform;
        }

        /// <summary>
        /// Quando <paramref name="battlePrefab"/> aponta para um filho do prefab, Unity instancia a hierarquia
        /// completa mas devolve o objeto referenciado — sobe até ao root da instância (ex.: Buck Wyatt).
        /// </summary>
        private static Transform ResolveInstantiatedHierarchyRoot(Transform instantiatedTransform)
        {
            var hierarchyRootTransform = instantiatedTransform;
            while (hierarchyRootTransform.parent != null)
            {
                hierarchyRootTransform = hierarchyRootTransform.parent;
            }

            return hierarchyRootTransform;
        }

        private static void CapturePrefabRootLocalTransform(
            GameObject prefabAsset,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            if (prefabAsset == null)
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                localScale = Vector3.one;
                return;
            }

            var prefabTransform = ResolvePrefabAssetHierarchyRoot(prefabAsset.transform);
            localPosition = prefabTransform.localPosition;
            localRotation = prefabTransform.localRotation;
            localScale = prefabTransform.localScale;
        }

        private static Transform ResolvePrefabAssetHierarchyRoot(Transform prefabTransform)
        {
            var hierarchyRootTransform = prefabTransform;
            while (hierarchyRootTransform.parent != null)
            {
                hierarchyRootTransform = hierarchyRootTransform.parent;
            }

            return hierarchyRootTransform;
        }

        /// <summary>
        /// Remove offsets de rotação Y herdados de prefabs de exploração (ex.: ~180° no root do FBX)
        /// e orienta o modelo para a party inimiga.
        /// </summary>
        public static void OrientEnemyVisualTowardAllies(Transform enemyVisualRoot, Transform alliesFacingReference)
        {
            if (enemyVisualRoot == null)
            {
                return;
            }

            ClearExplorationYawOnDirectChildren(enemyVisualRoot);

            if (alliesFacingReference == null)
            {
                return;
            }

            var directionTowardAllies = alliesFacingReference.position - enemyVisualRoot.position;
            directionTowardAllies.y = 0f;
            if (directionTowardAllies.sqrMagnitude < 0.0001f)
            {
                return;
            }

            enemyVisualRoot.rotation = Quaternion.LookRotation(directionTowardAllies.normalized, Vector3.up);
        }

        private static void ClearExplorationYawOnDirectChildren(Transform visualRoot)
        {
            for (var childIndex = 0; childIndex < visualRoot.childCount; childIndex++)
            {
                var childTransform = visualRoot.GetChild(childIndex);
                var localEulerAngles = childTransform.localEulerAngles;
                childTransform.localEulerAngles = new Vector3(localEulerAngles.x, 0f, localEulerAngles.z);
            }
        }

        /// <summary>
        /// Remove scripts de exploração (WASD, física, passos) dos modelos instanciados para combate.
        /// </summary>
        public static void PrepareAllyVisualForCombat(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                return;
            }

            foreach (var movementComponent in visualRoot.GetComponentsInChildren<MovimentoXZ>(true))
            {
                UnityEngine.Object.Destroy(movementComponent);
            }

            foreach (var rigidbody in visualRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                UnityEngine.Object.Destroy(rigidbody);
            }

            foreach (var playableAnimationController in visualRoot.GetComponentsInChildren<PlayableAnimationController>(true))
            {
                UnityEngine.Object.Destroy(playableAnimationController);
            }
        }

        /// <summary>
        /// Garante que o visual instanciado tem um <see cref="Collider"/> para raycast de seleção.
        /// O collider deve vir do prefab de batalha (ex.: CapsuleCollider no root do prefab).
        /// </summary>
        public static void EnsureCombatSelectionCollider(Transform visualRoot, string characterNameForLog = null)
        {
            if (visualRoot == null)
            {
                return;
            }

            var selectionCollider = visualRoot.GetComponentInChildren<Collider>(true);
            if (selectionCollider != null)
            {
                selectionCollider.enabled = true;
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(characterNameForLog)
                ? visualRoot.name
                : characterNameForLog;

            Debug.LogWarning(
                $"BattleVisualInstaller: '{displayName}' não tem Collider no prefab de batalha. " +
                "Arrasta o root do prefab (com CapsuleCollider) para battlePrefab no catálogo.",
                visualRoot);
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
