using UnityEditor;
using UnityEngine;

/// <summary>Mostra o estado da barra de stamina usada por este controlador em Play Mode.</summary>
[CustomEditor(typeof(PlayerInputMovementController))]
public class PlayerInputMovementControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (PlayerInputMovementController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stamina", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Estado da stamina disponível apenas em Play Mode.", MessageType.Info);
            return;
        }

        if (controller.StaminaBar == null || controller.StaminaBar.Model == null)
        {
            EditorGUILayout.HelpBox("Nenhuma StaminaBar atribuída (ou ainda não inicializada) - sprint sem restrição.", MessageType.None);
            return;
        }

        var model = controller.StaminaBar.Model;
        EditorGUILayout.LabelField("Min", model.Min.ToString("F1"));
        EditorGUILayout.LabelField("Max", model.Max.ToString("F1"));
        EditorGUILayout.LabelField("Atual", model.Current.ToString("F1"));
        EditorGUILayout.LabelField("Normalizado", model.Normalized.ToString("P0"));
    }
}