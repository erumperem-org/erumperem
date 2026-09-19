using System.Collections.Generic;
using UnityEngine;
using Core.Exploration.Items;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.CharacterStats.Testing
{
    /// <summary>
    /// One-click test scene setup: registers a chosen set of item assets
    /// into an ItemRegistry, and spawns the ItemEffectTestbed and
    /// CharacterStatsTestbed harnesses as child GameObjects, ready to press
    /// Play.
    /// </summary>
    public sealed class CharacterStatsTestSceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private ItemRegistry _itemRegistry;
        [SerializeField] private List<ScriptableObject> _itemsToRegister = new();

#if UNITY_EDITOR
        public void RegisterItemsInRegistry()
        {
            if (_itemRegistry == null)
            {
                Debug.LogError("[CharacterStatsTestSceneBootstrapper] ItemRegistry not assigned.");
                return;
            }

            var so = new SerializedObject(_itemRegistry);
            var itemsProp = so.FindProperty("_items");

            foreach (var item in _itemsToRegister)
            {
                bool alreadyPresent = false;
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    if (itemsProp.GetArrayElementAtIndex(i).objectReferenceValue == item)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (alreadyPresent) continue;

                int index = itemsProp.arraySize;
                itemsProp.arraySize++;
                itemsProp.GetArrayElementAtIndex(index).objectReferenceValue = item;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_itemRegistry);

            Debug.Log($"[CharacterStatsTestSceneBootstrapper] Registered {_itemsToRegister.Count} item(s) into '{_itemRegistry.name}'.");
        }

        public void SpawnTestHarnesses()
        {
            var effectTestbedGO = new GameObject("ItemEffectTestbed");
            effectTestbedGO.transform.SetParent(transform);
            effectTestbedGO.AddComponent<ItemEffectTestbed>();

            var statsTestbedGO = new GameObject("CharacterStatsTestbed");
            statsTestbedGO.transform.SetParent(transform);
            statsTestbedGO.AddComponent<CharacterStatsTestbed>();

            Debug.Log("[CharacterStatsTestSceneBootstrapper] Test harness GameObjects created as children.");
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CharacterStatsTestSceneBootstrapper))]
    public sealed class CharacterStatsTestSceneBootstrapperEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var bootstrapper = (CharacterStatsTestSceneBootstrapper)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);

            if (GUILayout.Button("Register Items In Registry"))
                bootstrapper.RegisterItemsInRegistry();

            if (GUILayout.Button("Spawn Test Harnesses (ItemEffectTestbed + CharacterStatsTestbed)"))
                bootstrapper.SpawnTestHarnesses();
        }
    }
}
#endif