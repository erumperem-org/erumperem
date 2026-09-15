using UnityEditor;
using UnityEngine;

/// <summary>
/// Estende <see cref="CircularZoneEditor"/> (herda o handle de raio) e
/// adiciona um readout do estado atual mais botões para simular a entrada e
/// saída do alvo em Play Mode - útil para testar o orquestrador externo que
/// escuta <see cref="Hub.OnPlayerEnteredSafeArea"/>/
/// <see cref="Hub.OnPlayerExitedSafeArea"/> sem precisar mover o alvo até o
/// Hub de verdade a cada teste.
/// </summary>
[CustomEditor(typeof(Hub))]
public class HubEditor : CircularZoneEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var hub = (Hub)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Alvo dentro do Hub", hub.IsTargetInside ? "Sim" : "Não");

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Simular Entrada do Player"))
            {
                hub.Editor_SimulateEnter();
            }

            if (GUILayout.Button("Simular Saída do Player"))
            {
                hub.Editor_SimulateExit();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Disponível apenas em Play Mode.", MessageType.Info);
            }
        }
    }
}
