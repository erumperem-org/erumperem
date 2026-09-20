#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace InteractionSystem.Editor
{
    /// <summary>
    /// Editor genérico para qualquer InteractableBase (editorForChildClasses:
    /// true garante que Button/Chest/Npc herdem este inspector automaticamente).
    /// </summary>
    [CustomEditor(typeof(InteractableBase), true)]
    public class InteractableBaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var interactable = (InteractableBase)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Entre em Play Mode para testar a interação.", MessageType.Info);
                return;
            }

            var prevColor = GUI.color;
            GUI.color = interactable.CanInteract ? Color.green : new Color(1f, 0.4f, 0.4f);
            EditorGUILayout.LabelField("Disponível para interação:", interactable.CanInteract.ToString());
            GUI.color = prevColor;

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!interactable.CanInteract))
            {
                if (GUILayout.Button("Testar Interação (instigator = próprio GameObject)"))
                {
                    bool success = interactable.TryInteract(interactable.gameObject, out var context);
                    Debug.Log(success
                        ? $"[Interaction] OK. Contexto gerado: {context.GetType().Name}"
                        : "[Interaction] Falhou (CanInteractWith recusou).");
                }
            }

            if (GUILayout.Button("Forçar Disponível (SetAvailable(true))"))
            {
                interactable.SetAvailable(true);
            }
        }
    }
}
#endif
