using UnityEditor;
using UnityEngine;

/// <summary>Mostra o papel atual e o estado da barra de vida deste PlayableCharacters específico em Play Mode.</summary>
[CustomEditor(typeof(PlayableCharacters))]
public class PlayableCharactersEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var character = (PlayableCharacters)target;

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Estado atual disponível apenas em Play Mode.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Estado atual", character.CurrentState.ToString());

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vida", EditorStyles.boldLabel);

        if (character.HealthBar == null || character.HealthBar.Model == null)
        {
            EditorGUILayout.HelpBox("Nenhuma HealthBar atribuída (ou ainda não inicializada).", MessageType.None);
            return;
        }

        var model = character.HealthBar.Model;
        EditorGUILayout.LabelField("Min", model.Min.ToString("F1"));
        EditorGUILayout.LabelField("Max", model.Max.ToString("F1"));
        EditorGUILayout.LabelField("Atual", model.Current.ToString("F1"));
        EditorGUILayout.LabelField("Normalizado", model.Normalized.ToString("P0"));
    }
}