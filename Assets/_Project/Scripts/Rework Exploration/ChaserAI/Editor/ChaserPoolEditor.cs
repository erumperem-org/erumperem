using UnityEditor;
using UnityEngine;

/// <summary>
/// Readout de ativos/total e botões de teste em Play Mode: força uma
/// avaliação imediata de retorno/spawn, ou recolhe todos os Chasers ativos
/// de volta para a pool.
/// </summary>
[CustomEditor(typeof(ChaserPool))]
public class ChaserPoolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var pool = (ChaserPool)target;

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Estado e controles de teste disponíveis apenas em Play Mode.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Ativos", $"{pool.ActiveCount} / {pool.PoolSize}");

        if (GUILayout.Button("Forçar reavaliação agora"))
        {
            pool.Editor_ForceEvaluate();
        }

        if (GUILayout.Button("Recolher todos para a pool"))
        {
            pool.Editor_RecallAll();
        }
    }
}