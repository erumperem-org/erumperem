#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.Inventory.UI.Editor
{
    [CustomEditor(typeof(SelectedItemPanelView))]
    public sealed class SelectedItemPanelViewEditor : UnityEditor.Editor
    {
        private ScriptableObject _testItemAsset;
        private InventorySystem _testInventory;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var panel = (SelectedItemPanelView)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate selection.", MessageType.Info);
                return;
            }

            _testItemAsset = (ScriptableObject)EditorGUILayout.ObjectField(
                "Test item (must implement IIITem)", _testItemAsset, typeof(ScriptableObject), false);
            _testInventory = (InventorySystem)EditorGUILayout.ObjectField(
                "Test inventory", _testInventory, typeof(InventorySystem), true);

            if (GUILayout.Button("Simulate Hide"))
                panel.Hide();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Currently selected", panel.SelectedItem?.StorageableId ?? "<none>");
        }
    }
}
#endif