using UnityEditor;
using UnityEngine;

/// <summary>Botão de teste em Play Mode: força uma avaliação de realocação imediata.</summary>
[CustomEditor(typeof(ChaserRelocator))]
public class ChaserRelocatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Controles de teste disponíveis apenas em Play Mode.", MessageType.Info);
            return;
        }

        if (GUILayout.Button("Forçar reavaliação agora"))
        {
            ((ChaserRelocator)target).EvaluateNow();
        }
    }
}
