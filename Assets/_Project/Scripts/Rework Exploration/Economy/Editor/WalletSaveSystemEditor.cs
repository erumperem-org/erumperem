#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Economy.Currency.Editor
{
    [CustomEditor(typeof(WalletSaveSystem))]
    public sealed class WalletSaveSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var saveSystem = (WalletSaveSystem)target;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test save/load/delete.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Save Wallet"))
                saveSystem.SaveAsync();

            if (GUILayout.Button("Load Wallet"))
                _ = saveSystem.LoadAsync();

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Save"))
                saveSystem.DeleteSave();
        }
    }
}
#endif
