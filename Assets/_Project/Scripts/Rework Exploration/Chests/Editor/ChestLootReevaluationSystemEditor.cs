#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Chests.Editor
{
    [CustomEditor(typeof(ChestLootReevaluationSystem))]
    public sealed class ChestLootReevaluationSystemEditor : UnityEditor.Editor
    {
        private int _testTierInput;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var system = (ChestLootReevaluationSystem)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnostics", EditorStyles.boldLabel);

            var poolSizes = system.GetTablePoolSizes();
            for (int i = 0; i < poolSizes.Count; i++)
            {
                string status = poolSizes[i] > 0 ? $"{poolSizes[i]} table(s)" : "EMPTY — will fail";
                EditorGUILayout.LabelField($"Tier {i} pool", status);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test reevaluation.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Known chests", system.KnownChests.Count.ToString());
            EditorGUILayout.LabelField("Current tier (resolved)", system.CurrentTier.ToString());

            if (GUILayout.Button("Discover Chests In Scene"))
                system.DiscoverChestsInScene();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Manual Test Tier (used only if no ICorruptionTierSource is assigned)", EditorStyles.wordWrappedLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                _testTierInput = EditorGUILayout.IntField("Test tier", _testTierInput);

                if (GUILayout.Button("Apply", GUILayout.Width(60)))
                    system.SetManualTestTier(_testTierInput);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Force Reevaluate All"))
                system.ForceReevaluateAll();
        }
    }
}
#endif