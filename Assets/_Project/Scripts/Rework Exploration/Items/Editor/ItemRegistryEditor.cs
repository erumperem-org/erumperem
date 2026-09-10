#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Core.Storage.Editor;

namespace Core.Exploration.Items.Editor
{
    [CustomEditor(typeof(NewItemRegistry))]
    public sealed class ItemRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var registry = (NewItemRegistry)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing / Validation", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Registry"))
            {
                var errors = NewItemRegistryValidator.Validate(registry).ToList();

                if (errors.Count == 0)
                    Debug.Log($"[NewItemRegistry] '{registry.name}' is valid — no errors found.");
                else
                    foreach (var error in errors)
                        Debug.LogError($"[NewItemRegistry] {error.Message}", error.Context);
            }

            if (GUILayout.Button("List Resolvable Items"))
            {
                foreach (var obj in registry.Items)
                {
                    if (obj is IIITem item && !string.IsNullOrEmpty(item.StorageableId))
                        Debug.Log($"[NewItemRegistry] '{item.StorageableId}' → {obj.name}", obj);
                }
            }

            // Only fills in ids that are currently empty — never overwrites an existing one.
            if (GUILayout.Button("Generate Missing IDs (ITEM_...)"))
            {
                int count = StorageableIdGenerator.GenerateMissingIds(
                    registry.Items,
                    "ITEM",
                    obj => (obj as IIITem)?.StorageableId);

                Debug.Log($"[NewItemRegistry] {count} id(s) generated.");
            }
        }
    }
}
#endif