using Erumperem.Characters;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Resolve catálogos de combate em runtime quando referências serializadas se perdem
    /// (ex.: prefab instanciado após load assíncrono vindo do Overworld).
    /// </summary>
    public static class CombatCatalogLocator
    {
        public static AllyCharacterStatCatalog ResolveAllyCharacterStatCatalog(
            AllyCharacterStatCatalog serializedReference)
        {
            if (serializedReference != null)
            {
                return serializedReference;
            }

            var catalogFromExplorationContext = ExplorationLoadContext.Instance?.AllyCharacterStatCatalogForCombat;
            if (catalogFromExplorationContext != null)
            {
                return catalogFromExplorationContext;
            }

            return FindLoadedScriptableAsset<AllyCharacterStatCatalog>();
        }

        public static EnemyCharacterStatCatalog ResolveEnemyCharacterStatCatalog(
            EnemyCharacterStatCatalog serializedReference)
        {
            return serializedReference != null
                ? serializedReference
                : FindLoadedScriptableAsset<EnemyCharacterStatCatalog>();
        }

        public static EnemyVisualSpawnCatalog ResolveEnemyVisualSpawnCatalog(
            EnemyVisualSpawnCatalog serializedReference)
        {
            return serializedReference != null
                ? serializedReference
                : FindLoadedScriptableAsset<EnemyVisualSpawnCatalog>();
        }

        public static EnemyVisualDefinition ResolveHorseBossVisualDefinition(
            EnemyVisualDefinition serializedReference)
        {
            return serializedReference != null
                ? serializedReference
                : FindLoadedScriptableAsset<EnemyVisualDefinition>();
        }

        private static T FindLoadedScriptableAsset<T>() where T : Object
        {
            var loadedAssets = Resources.FindObjectsOfTypeAll<T>();
            for (var assetIndex = 0; assetIndex < loadedAssets.Length; assetIndex++)
            {
                var loadedAsset = loadedAssets[assetIndex];
                if (loadedAsset == null)
                {
                    continue;
                }

                if ((loadedAsset.hideFlags & HideFlags.NotEditable) != 0)
                {
                    continue;
                }

                return loadedAsset;
            }

            return loadedAssets.Length > 0 ? loadedAssets[0] : null;
        }
    }
}
