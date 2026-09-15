using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Testbed de editor do SaveSlotManager: gera/confere caminhos de slot,
/// verifica existência em disco, lista os arquivos salvos dentro do slot,
/// seleciona/deleta slots e mostra o estado atual do CurrentSlotData - tudo
/// sem precisar estar em Play Mode, já que SaveSlotManager só depende de IO
/// de arquivo e de um ScriptableObject, não de nenhum estado de gameplay.
/// </summary>
[CustomEditor(typeof(SaveSlotManager))]
public class SaveSlotManagerEditor : Editor
{
    private int testSlotIndex;
    private Vector2 fileListScrollPosition;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (SaveSlotManager)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Teste de Slots", EditorStyles.boldLabel);

        testSlotIndex = EditorGUILayout.IntField("Índice de teste", testSlotIndex);

        if (testSlotIndex < 0)
        {
            EditorGUILayout.HelpBox("Índice inválido - deve ser >= 0.", MessageType.Warning);
        }
        else
        {
            string previewPath = manager.GetSlotPath(testSlotIndex);
            bool exists = manager.SlotExistsOnDisk(testSlotIndex);

            EditorGUILayout.LabelField("Caminho gerado", previewPath);
            EditorGUILayout.LabelField("Existe no disco?", exists ? "Sim" : "Não");

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Selecionar este slot"))
                {
                    manager.SelectSlot(testSlotIndex);
                }

                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("Abrir pasta no explorador"))
                    {
                        EditorUtility.RevealInFinder(previewPath);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(!exists))
            {
                if (GUILayout.Button("Deletar este slot"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Deletar slot",
                        $"Isso apaga permanentemente a pasta:\n{previewPath}\n\nEsta ação não pode ser desfeita.",
                        "Deletar",
                        "Cancelar");

                    if (confirmed)
                    {
                        manager.DeleteSlot(testSlotIndex);
                    }
                }
            }

            EditorGUILayout.Space();
            DrawSlotFileList(previewPath, exists);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Raiz dos slots", manager.SlotsRootPath);

        DrawCurrentSlotDataState();
    }

    private void DrawSlotFileList(string slotPath, bool slotExists)
    {
        EditorGUILayout.LabelField("Arquivos no slot", EditorStyles.boldLabel);

        if (!slotExists)
        {
            EditorGUILayout.HelpBox("Pasta do slot ainda não existe.", MessageType.None);
            return;
        }

        string[] files;

        try
        {
            files = Directory.GetFiles(slotPath, "*", SearchOption.AllDirectories);
        }
        catch (System.Exception exception)
        {
            EditorGUILayout.HelpBox($"Falha ao listar arquivos: {exception.Message}", MessageType.Error);
            return;
        }

        if (files.Length == 0)
        {
            EditorGUILayout.HelpBox("Pasta do slot existe, mas está vazia.", MessageType.None);
            return;
        }

        using (var scrollScope = new EditorGUILayout.ScrollViewScope(fileListScrollPosition, GUILayout.MaxHeight(160)))
        {
            fileListScrollPosition = scrollScope.scrollPosition;

            foreach (string filePath in files)
            {
                string relativePath = Path.GetRelativePath(slotPath, filePath);
                long sizeInBytes = new FileInfo(filePath).Length;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(relativePath, GUILayout.ExpandWidth(true));
                    EditorGUILayout.LabelField(FormatFileSize(sizeInBytes), GUILayout.Width(70));

                    if (GUILayout.Button("Abrir", GUILayout.Width(50)))
                    {
                        EditorUtility.RevealInFinder(filePath);
                    }
                }
            }
        }

        EditorGUILayout.LabelField($"{files.Length} arquivo(s) encontrado(s).", EditorStyles.miniLabel);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        float kb = bytes / 1024f;
        return kb < 1024f ? $"{kb:F1} KB" : $"{kb / 1024f:F1} MB";
    }

    private void DrawCurrentSlotDataState()
    {
        var currentSlotDataProperty = serializedObject.FindProperty("currentSlotData");
        var currentSlotData = currentSlotDataProperty?.objectReferenceValue as CurrentSlotData;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Estado atual do CurrentSlotData", EditorStyles.boldLabel);

        if (currentSlotData == null)
        {
            EditorGUILayout.HelpBox("Nenhum CurrentSlotData atribuído no Inspector.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Slot selecionado?", currentSlotData.HasSlotSelected ? "Sim" : "Não");

        if (currentSlotData.HasSlotSelected)
        {
            EditorGUILayout.LabelField("Índice", currentSlotData.SlotIndex.ToString());
            EditorGUILayout.LabelField("Diretório", currentSlotData.SlotDirectory);
        }
    }
}