#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Storage;

namespace Core.Rewards.Editor
{
    [CustomEditor(typeof(CorruptionRewardDispenser))]
    public sealed class CorruptionRewardDispenserEditor : UnityEditor.Editor
    {
        private double _testCorruptionValue;
        private string _lastResultSummary = "";

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var dispenser = (CorruptionRewardDispenser)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Generation Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test generation.", MessageType.Info);
                return;
            }

            _testCorruptionValue = EditorGUILayout.DoubleField("Corruption value", _testCorruptionValue);

            if (GUILayout.Button("Generate"))
            {
                var result = dispenser.GenerateFromCorruption(_testCorruptionValue);
                _lastResultSummary = FormatResult(result);
                Debug.Log($"[CorruptionRewardDispenser] {_lastResultSummary}");
            }

            if (!string.IsNullOrEmpty(_lastResultSummary))
                EditorGUILayout.HelpBox(_lastResultSummary, MessageType.None);
        }

        private static string FormatResult(System.Collections.Generic.IReadOnlyDictionary<InterfaceStorageable, int> result)
        {
            if (result.Count == 0) return "No rewards were generated.";

            var lines = new System.Text.StringBuilder("Generated:\n");
            foreach (var (storageable, amount) in result)
                lines.AppendLine($"- {storageable.StorageableId} x{amount}");

            return lines.ToString();
        }
    }
}
#endif
