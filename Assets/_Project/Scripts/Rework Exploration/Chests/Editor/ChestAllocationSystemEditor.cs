#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Chests.Editor
{
    [CustomEditor(typeof(ChestAllocationSystem))]
    public sealed class ChestAllocationSystemEditor : UnityEditor.Editor
    {
        private int _testTierInput;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var system = (ChestAllocationSystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test initialization/reallocation.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Pool created", system.IsPoolCreated.ToString());
            EditorGUILayout.LabelField("Active chests", system.ActiveChestCount.ToString());
            EditorGUILayout.LabelField("Current tier (resolved)", system.CurrentTier.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Manual Test Tier (used only if no ICorruptionTierSource is assigned)", EditorStyles.wordWrappedLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _testTierInput = EditorGUILayout.IntField("Test tier", _testTierInput);

                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    system.SetManualTestTier(_testTierInput);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(system.IsPoolCreated))
            {
                if (GUILayout.Button("Initialize (Create Pool)"))
                    system.Initialize();
            }

            using (new EditorGUI.DisabledScope(!system.IsPoolCreated))
            {
                if (GUILayout.Button("Reallocate (Reposition)"))
                    system.Reallocate();
            }
        }
    }
}
#endif