using UnityEditor;
using UnityEngine;

/// <summary>Mostra, em Play Mode, quantos chasers foram criados e quantos aguardam colocação.</summary>
[CustomEditor(typeof(ChaserSpawner))]
public class ChaserSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var spawner = (ChaserSpawner)target;

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Estado disponível apenas em Play Mode.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Criados", spawner.SpawnedCount.ToString());
        EditorGUILayout.LabelField("Aguardando colocação", spawner.PendingCount.ToString());
        Repaint();
    }
}
