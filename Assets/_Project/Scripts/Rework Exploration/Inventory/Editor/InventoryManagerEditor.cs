#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Inventory;

namespace Core.Inventory.Editor
{
    [CustomEditor(typeof(InventoryManager))]
    public sealed class InventoryManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var manager = (InventoryManager)target;

            var losableProp = serializedObject.FindProperty("_losable");
            var permanentProp = serializedObject.FindProperty("_permanent");

            var losable = losableProp.objectReferenceValue as InventorySystem;
            var permanent = permanentProp.objectReferenceValue as InventorySystem;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Migration Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test migration.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Request Full Migration (Losable → Permanent)"))
                manager.RequestFullMigration();

            EditorGUILayout.Space();
            DrawInventoryState("Losable", losable);
            EditorGUILayout.Space();
            DrawInventoryState("Permanent", permanent);
        }

        private void DrawInventoryState(string label, InventorySystem inventory)
        {
            EditorGUILayout.LabelField($"{label} Inventory", EditorStyles.boldLabel);

            if (inventory == null)
            {
                EditorGUILayout.HelpBox("Not assigned.", MessageType.Warning);
                return;
            }

            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                var slot = inventory.Slots[i];
                string content = slot.IsEmpty
                    ? "<empty>"
                    : $"{slot.Item.StorageableId} x{slot.Quantity}";

                EditorGUILayout.LabelField($"Slot {i}", content);
            }
        }
    }
}
#endif