#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Chests.Editor
{
    [CustomEditor(typeof(Chest))]
    public sealed class ChestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var chest = (Chest)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Testing (Play Mode)", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to test interaction.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Consumed", chest.IsConsumed.ToString());
            EditorGUILayout.LabelField("Has content", chest.HasContent.ToString());

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);

            var contents = chest.DebugContents;

            if (contents == null || contents.Count == 0)
            {
                EditorGUILayout.HelpBox("Empty — no loot assigned (or already consumed).", MessageType.None);
            }
            else
            {
                foreach (var (storageable, amount) in contents)
                {
                    string id = storageable != null ? storageable.StorageableId : "<null>";
                    EditorGUILayout.LabelField(id, $"x{amount}");
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!chest.CanInteract))
            {
                if (GUILayout.Button("Interact"))
                {
                    bool success = chest.TryInteract(chest.gameObject, out var context);
                    Debug.Log(success
                        ? "[ChestEditor] Interação executada com sucesso."
                        : "[ChestEditor] Falhou (CanInteract == false).");
                }
            }
        }
    }
}
#endif