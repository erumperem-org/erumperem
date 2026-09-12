#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Core.Storage.Editor;

namespace Core.Economy.Currency.Editor
{
    [CustomEditor(typeof(CoinRegistry))]
    public sealed class CoinRegistryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var registry = (CoinRegistry)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing / Validation", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Registry"))
            {
                var errors = CoinRegistryValidator.Validate(registry).ToList();

                if (errors.Count == 0)
                    Debug.Log($"[CoinRegistry] '{registry.name}' is valid — no errors found.");
                else
                    foreach (var error in errors)
                        Debug.LogError($"[CoinRegistry] {error.Message}", error.Context);
            }

            if (GUILayout.Button("List Resolvable Coins"))
            {
                foreach (var obj in registry.Coins)
                {
                    if (obj is ICoin coin && !string.IsNullOrEmpty(coin.StorageableId))
                        Debug.Log($"[CoinRegistry] '{coin.StorageableId}' → {obj.name}", obj);
                }
            }

            // Only fills in ids that are currently empty — never overwrites an existing one.
            if (GUILayout.Button("Generate Missing IDs (COIN_...)"))
            {
                int count = StorageableIdGenerator.GenerateMissingIds(
                    registry.Coins,
                    "COIN",
                    obj => (obj as ICoin)?.StorageableId);

                Debug.Log($"[CoinRegistry] {count} id(s) generated.");
            }
        }
    }
}
#endif