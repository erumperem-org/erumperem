#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Editor
{
    [CustomEditor(typeof(InteractionSensor))]
    public class InteractionSensorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var sensor = (InteractionSensor)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Entre em Play Mode para ver o alvo detectado.", MessageType.Info);
                return;
            }

            var targetComponent = sensor.CurrentTarget as Component;
            EditorGUILayout.ObjectField("Alvo atual", targetComponent, typeof(Component), true);

            using (new EditorGUI.DisabledScope(sensor.CurrentTarget == null))
            {
                if (GUILayout.Button("Testar Interação"))
                {
                    bool success = sensor.TryInteract();
                    Debug.Log(success ? "[InteractionSensor] Interação disparada." : "[InteractionSensor] Falhou.");
                }
            }
        }
    }
}
#endif
