#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.SceneAllocation.Editor
{
    [CustomEditor(typeof(SceneObjectAllocationSaveSystem))]
    public sealed class SceneObjectAllocationSaveSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var system = (SceneObjectAllocationSaveSystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test save/load/delete — file path depends on the runtime SaveSlotAccess singleton.", MessageType.Info);
                return;
            }

            DrawDiagnostics(system);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Startup Flow", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(system.IsInitialized))
            {
                if (GUILayout.Button("Initialize (Load Save, or Allocate + Save if none)"))
                    _ = system.Initialize();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Independent Actions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save"))
                    system.Save();

                if (GUILayout.Button("Load"))
                    system.Load();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Delete Save File"))
                    system.Delete();

                using (new EditorGUI.DisabledScope(!system.SaveFileExists()))
                {
                    if (GUILayout.Button("Open Save File"))
                        OpenSaveFile(system);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Reset destroys every spawned instance AND deletes the save file, " +
                "so the next Initialize() call re-runs the real allocation routine.",
                MessageType.Info);

            if (GUILayout.Button("Reset And Delete Save"))
                system.ResetAndDeleteSave();
        }

        private void DrawDiagnostics(SceneObjectAllocationSaveSystem system)
        {
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Initialized", system.IsInitialized.ToString());
            EditorGUILayout.LabelField("Currently spawned", system.SpawnedCount.ToString());
            EditorGUILayout.LabelField("Save file exists", system.SaveFileExists().ToString());
            EditorGUILayout.LabelField("Placements saved on disk", system.GetSavedPlacementCountOnDisk().ToString());
            EditorGUILayout.LabelField("Save file size", $"{system.GetSaveFileSizeBytes()} bytes");

            string path = system.GetSavePath();
            EditorGUILayout.LabelField("Save path", string.IsNullOrEmpty(path) ? "<no slot selected>" : path);
        }

        private void OpenSaveFile(SceneObjectAllocationSaveSystem system)
        {
            string path = system.GetSavePath();
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                Debug.LogWarning("[SceneObjectAllocationSaveSystem] No save file to open.");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}
#endif