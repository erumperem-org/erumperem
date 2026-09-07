#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Inventory.UI.Editor
{
    [CustomEditor(typeof(InventoryGridView))]
    public sealed class InventoryGridViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var view = (InventoryGridView)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test grid rebuilding/refresh.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Rebuild Slots"))
                view.BuildSlots();

            if (GUILayout.Button("Refresh All"))
                view.RefreshAll();
        }
    }
}
#endif