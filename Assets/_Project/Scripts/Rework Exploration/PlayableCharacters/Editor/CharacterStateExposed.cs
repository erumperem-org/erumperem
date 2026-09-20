using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterStateExposed))]
public class CharacterStateExposedEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CharacterStateExposed characterState = (CharacterStateExposed)target;

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);

        EditorGUILayout.ObjectField(
            "Torch Manager",
            serializedObject.FindProperty("_torchManager").objectReferenceValue,
            typeof(TorchManager),
            true
        );

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Current States", EditorStyles.boldLabel);

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField(
            "Movement",
            characterState.MovementState.ToString()
        );

        EditorGUILayout.LabelField(
            "Interaction",
            characterState.InteractionState.ToString()
        );

        EditorGUILayout.LabelField(
            "Torch",
            characterState.TorchState.ToString()
        );

        serializedObject.ApplyModifiedProperties();
    }
}