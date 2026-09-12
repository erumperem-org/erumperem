using UnityEngine;
using UnityEditor;
using BarSystem.Bars.Corruption;

namespace BarSystem.Editor
{
    [CustomEditor(typeof(CorruptionBarInstaller))]
    public class CorruptionBarInstallerEditor : UnityEditor.Editor
    {
        private const float CorruptionAmount = 10f;
        private const float ReductionAmount = 10f;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var installer = (CorruptionBarInstaller)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tests (Play Mode)", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"Add Corruption ({CorruptionAmount})"))
                        installer.AddCorruption(CorruptionAmount);

                    if (GUILayout.Button($"Reduce Corruption ({ReductionAmount})"))
                        installer.ReduceCorruption(ReductionAmount);
                }
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test the buttons.",
                    MessageType.Info);
        }
    }
}