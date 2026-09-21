using UnityEditor;
using UnityEngine;

/// <summary>
/// Mostra o estado atual do <see cref="ChaserAI"/> em Play Mode - útil
/// para conferir a transição Wandering/Chasing/Investigating sem depender
/// de gizmos de percepção. Não há mais Resting: todo Chaser fica sempre
/// ativo.
/// </summary>
[CustomEditor(typeof(ChaserAI))]
public class ChaserAIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var chaser = (ChaserAI)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Estado atual", chaser.CurrentState.ToString());
        EditorGUILayout.LabelField("Alvo atual", chaser.Target != null ? chaser.Target.name : "-");

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Estado atual disponível apenas em Play Mode.", MessageType.Info);
        }
    }
}
