#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Editor
{
    [CustomEditor(typeof(InstigatorProximityZone))]
    public class InstigatorProximityZoneEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var zone = (InstigatorProximityZone)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Entre em Play Mode para ver se o instigador está no raio.", MessageType.Info);
                return;
            }

            var prevColor = GUI.color;
            GUI.color = zone.IsInstigatorInRange ? Color.green : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField("Instigador no raio:", zone.IsInstigatorInRange.ToString());
            GUI.color = prevColor;
        }
    }
}
#endif