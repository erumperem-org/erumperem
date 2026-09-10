using UnityEditor;
using UnityEngine;

/// <summary>
/// Readout dos papéis atuais e botões de teste em Play Mode: executa cada
/// uma das três operações de troca com um id fornecido no Inspector, além
/// de forçar Save/Load manualmente.
/// </summary>
[CustomEditor(typeof(PlayableCharacterController))]
public class PlayableCharacterControllerEditor : Editor
{
    private string testCharacterId;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (PlayableCharacterController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Em Jogo", string.IsNullOrEmpty(controller.InGameCharacterId) ? "-" : controller.InGameCharacterId);
        EditorGUILayout.LabelField("Companheiro", string.IsNullOrEmpty(controller.CompanionCharacterId) ? "-" : controller.CompanionCharacterId);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.Space();
            testCharacterId = EditorGUILayout.TextField("Id de teste", testCharacterId);

            if (GUILayout.Button("Promover a Em Jogo"))
            {
                controller.ExecuteOperation(new PromoteToInGameOperation(testCharacterId));
            }

            if (GUILayout.Button("Promover a Companheiro"))
            {
                controller.ExecuteOperation(new PromoteToCompanionOperation(testCharacterId));
            }

            if (GUILayout.Button("Trocar Em Jogo <-> Companheiro"))
            {
                controller.ExecuteOperation(new SwapInGameAndCompanionOperation());
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Forçar Save agora"))
            {
                _ = controller.SaveAsync();
            }

            if (GUILayout.Button("Forçar Load agora"))
            {
                controller.Editor_ForceReload();
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Testes de troca/save disponíveis apenas em Play Mode.", MessageType.Info);
            }
        }
    }
}
