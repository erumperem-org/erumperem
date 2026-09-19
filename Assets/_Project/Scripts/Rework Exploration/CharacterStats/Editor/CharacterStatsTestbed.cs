using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.CharacterStats.Testing
{
#if UNITY_EDITOR
    [CustomEditor(typeof(CharacterStatsTestbed))]
    public sealed class CharacterStatsTestbedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (CharacterStatsTestbed)target;

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to read/write the stats file.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Dump Stats For ID"))
                testbed.DumpStats();

            if (GUILayout.Button("Apply Test Modifier"))
                testbed.ApplyTestModifier();

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Stats File"))
                testbed.DeleteFile();
        }
    }
#endif
}
