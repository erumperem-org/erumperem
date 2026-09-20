#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Bridges.Editor
{
    [CustomEditor(typeof(PlayableCharacterInstigatorProvider))]
    public class PlayableCharacterInstigatorProviderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var provider = (PlayableCharacterInstigatorProvider)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Entre em Play Mode para ver o instigador atual.", MessageType.Info);
                return;
            }

            EditorGUILayout.ObjectField("Instigador atual", provider.Current, typeof(GameObject), true);
        }
    }
}
#endif
