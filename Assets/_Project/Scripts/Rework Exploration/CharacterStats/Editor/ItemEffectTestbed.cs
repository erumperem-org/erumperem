using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.CharacterStats.Testing
{
#if UNITY_EDITOR
    [CustomEditor(typeof(ItemEffectTestbed))]
    public sealed class ItemEffectTestbedEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (ItemEffectTestbed)target;

            EditorGUILayout.Space();

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to execute the item effect.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Execute Effect"))
                testbed.ExecuteEffect();
        }
    }
#endif
}
