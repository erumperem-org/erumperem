using UnityEditor;
using UnityEngine;

/// <summary>
/// Mostra, em Play Mode, se o alvo está atualmente disponível para caça ou
/// dentro de alguma área segura monitorada - útil para conferir o estado
/// agregado sem precisar inspecionar cada <c>Hub</c> individualmente.
/// </summary>
[CustomEditor(typeof(EnemyHuntOrchestrator))]
public class EnemyHuntOrchestratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var orchestrator = (EnemyHuntOrchestrator)target;

        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            string status = orchestrator.IsTargetAvailableForHunt
                ? "Disponível para caça"
                : "Em área segura";

            EditorGUILayout.LabelField("Estado atual", status);
        }
        else
        {
            EditorGUILayout.HelpBox("Estado atual disponível apenas em Play Mode.", MessageType.Info);
        }
    }
}