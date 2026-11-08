#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Storage;
using Core.Exploration.Items;

namespace Core.CharacterStats.Authoring
{
    public static class RestorativeDrinkGenerator
    {
        private const string Folder = "Assets/Items/CharacterStats/Utility";
        private const string AssetPath = Folder + "/RESTORATIVE_DRINK.asset";

        [MenuItem("Tools/Character Stats/Create Restorative Drink")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(AssetPath) != null)
            {
                Debug.LogWarning($"[RestorativeDrinkGenerator] '{AssetPath}' already exists — nothing changed.");
                return;
            }

            EnsureFolders();

            var asset = ScriptableObject.CreateInstance<RestorativeDrinkItem>();
            var so = new SerializedObject(asset);

            so.FindProperty("_storageableId").stringValue = "RESTORATIVE_DRINK";
            so.FindProperty("_displayName").stringValue = "Restorative Drink";
            so.FindProperty("_description").stringValue = "Restores health to every character in the party.";
            so.FindProperty("_rarity").enumValueIndex = (int)ItemRarity.Rare; // placeholder: set the rarity you want
            so.FindProperty("_storageStrategy").managedReferenceValue = new StackableStorageStrategy();
            so.FindProperty("_healAmount").floatValue = 0f;

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;
        }

        private static void EnsureFolders()
        {
            string current = "Assets";
            foreach (string part in Folder.Substring("Assets/".Length).Split('/'))
            {
                string next = $"{current}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
    }
}
#endif