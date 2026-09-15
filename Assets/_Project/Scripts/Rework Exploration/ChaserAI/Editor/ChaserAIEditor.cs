using UnityEditor;
using UnityEngine;

/// <summary>
/// Mostra o estado atual do <see cref="ChaserAI"/> em Play Mode e permite
/// forçar Resting/saída de Resting manualmente, para testar essa transição
/// sem depender do Hub / orquestrador externo estarem prontos ainda.
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

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Forçar Resting"))
            {
                chaser.EnterResting();
            }

            if (GUILayout.Button("Sair de Resting (volta a Wandering)"))
            {
                chaser.ExitResting();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Disponível apenas em Play Mode.", MessageType.Info);
            }
        }
    }
}
