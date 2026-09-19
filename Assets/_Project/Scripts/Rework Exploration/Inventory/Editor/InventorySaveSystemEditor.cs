#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Inventory.Editor
{
    [CustomEditor(typeof(InventorySaveSystem))]
    public sealed class InventorySaveSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var saveSystem = (InventorySaveSystem)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test save/load/delete.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Inventory"))
                saveSystem.SaveAsync();

            if (GUILayout.Button("Load Inventory"))
                _ = saveSystem.LoadAsync();

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Save (use before resizing)"))
                saveSystem.DeleteSave();
        }
    }
}
#endif
