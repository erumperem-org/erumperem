#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Shop.Editor
{
    [CustomEditor(typeof(SkillPointShopSaveSystem))]
    public sealed class SkillPointShopSaveSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var saveSystem = (SkillPointShopSaveSystem)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test save/load/delete.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save State"))
                saveSystem.SaveAsync();

            if (GUILayout.Button("Load State"))
                _ = saveSystem.LoadAsync();

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Save"))
                saveSystem.DeleteSave();
        }
    }
}
#endif
