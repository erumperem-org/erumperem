using UnityEditor;
using UnityEngine;

/// <summary>
/// Readout do tamanho do pool e botão de teste em Play Mode: força uma
/// avaliação imediata de reposicionamento (sem esperar o
/// evaluationInterval). Não há mais distinção de "ativos" - todo Chaser
/// da lista está sempre ativo.
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

        EditorGUILayout.LabelField("Chasers no pool", pool.PoolSize.ToString());

        if (GUILayout.Button("Forçar reavaliação agora"))
        {
            pool.Editor_ForceEvaluate();
        }
    }
}
