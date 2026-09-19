#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor for TorchManager.
/// Adds test buttons to the Inspector to light/extinguish the torches
/// and to manually save/load their state.
///
/// IMPORTANT: this file must be inside a folder named
/// "Editor" at any level of the project (e.g. Assets/Editor/), otherwise
/// Unity will attempt to include it in the build and compilation will fail.
/// </summary>
[CustomEditor(typeof(TorchManager))]
public class TorchManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TorchManager manager = (TorchManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Tests", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to use the test buttons below.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Light Torches"))
            {
                manager.SetTorchState(true);
            }
            if (GUILayout.Button("Extinguish Torches"))
            {
                manager.SetTorchState(false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save State"))
            {
                _ = manager.SaveTorchStateAsync();
            }
            if (GUILayout.Button("Load State"))
            {
                _ = manager.LoadTorchStateAsync();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(
            "Current state: " + (manager.IsTorchLit ? "LIT" : "UNLIT"));
    }
}
#endif