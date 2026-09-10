#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.Chests.Editor
{
    [CustomEditor(typeof(ChestView))]
    public sealed class ChestViewEditor : UnityEditor.Editor
    {
        private ItemRarity _testRarity;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var view = (ChestView)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration Check", EditorStyles.boldLabel);

            var rendererProp = serializedObject.FindProperty("_renderer");
            var paletteProp = serializedObject.FindProperty("_rarityColorPalette");

            if (rendererProp.objectReferenceValue == null)
                EditorGUILayout.HelpBox("Renderer not assigned — rarity color will never be applied.", MessageType.Warning);

            if (paletteProp.objectReferenceValue == null)
                EditorGUILayout.HelpBox("RarityColorPalette not assigned — rarity color will never be applied.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            _testRarity = (ItemRarity)EditorGUILayout.EnumPopup("Rarity to preview", _testRarity);

            using (new EditorGUI.DisabledScope(rendererProp.objectReferenceValue == null || paletteProp.objectReferenceValue == null))
            {
                if (GUILayout.Button("Apply Color Preview"))
                    ApplyPreview(view, _testRarity);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("State Simulation (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to simulate state changes through the actual Chest.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Simulate Open"))
                    SimulateStateChange(view, ChestState.Open);

                if (GUILayout.Button("Simulate Closed"))
                    SimulateStateChange(view, ChestState.Closed);
            }
        }

        /// <summary>
        /// Applies a rarity color directly via reflection into the private
        /// handler, without requiring a real Chest to raise the event —
        /// useful for previewing the palette on the actual mesh in editor,
        /// even outside Play Mode.
        /// </summary>
        private void ApplyPreview(ChestView view, ItemRarity rarity)
        {
            var method = typeof(ChestView).GetMethod("HandleBestItemRarityRevealed",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method?.Invoke(view, new object[] { rarity });
        }

        /// <summary>
        /// Simulates a chest state change the same way. Only meaningful in
        /// Play Mode since the (currently commented-out) animation wiring
        /// will eventually depend on an Animator component being active.
        /// </summary>
        private void SimulateStateChange(ChestView view, ChestState state)
        {
            var method = typeof(ChestView).GetMethod("HandleChestStateChanged",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            method?.Invoke(view, new object[] { state });
        }
    }
}
#endif