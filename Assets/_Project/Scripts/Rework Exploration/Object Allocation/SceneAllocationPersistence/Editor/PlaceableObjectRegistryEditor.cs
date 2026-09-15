#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.SceneAllocation.Editor
{
    [CustomEditor(typeof(PlaceableObjectRegistry))]
    public sealed class PlaceableObjectRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var registry = (PlaceableObjectRegistry)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing / Validation", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Registry"))
            {
                var errors = PlaceableObjectRegistryValidator.Validate(registry).ToList();

                if (errors.Count == 0)
                    Debug.Log($"[PlaceableObjectRegistry] '{registry.name}' is valid — no errors found.");
                else
                    foreach (var error in errors)
                        Debug.LogError($"[PlaceableObjectRegistry] {error.Message}", registry);
            }

            if (GUILayout.Button("List Resolvable Entries"))
            {
                foreach (var entry in registry.Entries)
                {
                    if (entry.Data != null && !string.IsNullOrEmpty(entry.Id))
                        Debug.Log($"[PlaceableObjectRegistry] '{entry.Id}' → {entry.Data.name}", entry.Data);
                }
            }

            // Only fills in ids that are currently empty — never overwrites an existing one,
            // for the same reason as StorageableIdGenerator: overwriting would break any save
            // data already referencing that id.
            if (GUILayout.Button("Generate Missing IDs (PLACEABLE_...)"))
                GenerateMissingIds(registry);
        }

        private static void GenerateMissingIds(PlaceableObjectRegistry registry)
        {
            var serializedObject = new SerializedObject(registry);
            var entriesProp = serializedObject.FindProperty("_entries");

            var existingIds = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var id = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue;
                if (!string.IsNullOrEmpty(id)) existingIds.Add(id);
            }

            int generatedCount = 0;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var idProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("_id");
                if (!string.IsNullOrEmpty(idProp.stringValue)) continue;

                string newId;
                do
                {
                    newId = $"PLACEABLE_{GenerateHex()}";
                } while (existingIds.Contains(newId));

                existingIds.Add(newId);
                idProp.stringValue = newId;
                generatedCount++;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(registry);

            Debug.Log($"[PlaceableObjectRegistry] {generatedCount} id(s) generated.");
        }

        private static string GenerateHex()
        {
            var bytes = new byte[4];
            Guid.NewGuid().ToByteArray().AsSpan(0, 4).CopyTo(bytes);
            return BitConverter.ToString(bytes).Replace("-", "").ToUpperInvariant();
        }
    }
}
#endif
