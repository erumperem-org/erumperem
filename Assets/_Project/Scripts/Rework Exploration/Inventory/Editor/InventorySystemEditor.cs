#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Inventory.Editor
{
    [CustomEditor(typeof(InventorySystem))]
    public sealed class InventorySystemEditor : UnityEditor.Editor
    {
        private int _testResizeValue = 9;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var inventory = (InventorySystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mechanic Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test resize.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "README: delete this inventory's save (button on InventorySaveSystem) " +
                "before resizing, to avoid orphaned items outside the new size.",
                MessageType.Warning);

            _testResizeValue = EditorGUILayout.IntField("New size", _testResizeValue);

            if (GUILayout.Button("Resize (Test)"))
                inventory.Resize(_testResizeValue);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Current size: {inventory.Size}");
        }
    }
}
#endif
