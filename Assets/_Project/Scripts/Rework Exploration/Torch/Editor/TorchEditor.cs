#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom editor for Torch.
/// Adds test buttons to the Inspector to activate/deactivate
/// only the objects controlled by this specific torch (without relying on the TorchManager).
///
/// IMPORTANT: this file must be inside a folder named
/// "Editor" at any level of the project (e.g. Assets/Editor/), otherwise
/// Unity will attempt to include it in the build and compilation will fail.
/// </summary>
[CustomEditor(typeof(Torch))]
[CanEditMultipleObjects]
public class TorchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Torch torch = (Torch)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Torch Tests", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to use the test buttons below.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test: Activate"))
            {
                torch.TestActivate();
            }
            if (GUILayout.Button("Test: Deactivate"))
            {
                torch.TestDeactivate();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif