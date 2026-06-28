using System;
using System.IO;
using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Deleta todos os arquivos de save do jogo:
///   • exploration_save.json
///   • corruption_save.json
///   • inventory_save.json
///   • player_skill_progression.json
///   • ShopState/*.sav
/// </summary>
public sealed class SaveFileWiper : MonoBehaviour
{
    [Header("IO Settings")]
    [SerializeField] private string _saveFolderName = "Saves";

    private string SaveDirectory =>
        Path.Combine(Application.persistentDataPath, _saveFolderName);

    private string ShopStateDirectory =>
        Path.Combine(Application.persistentDataPath, "ShopState");

    private string SkillProgressionFilePath =>
        Path.Combine(Application.persistentDataPath, "player_skill_progression.json");

    private string[] SaveFileNames => new[]
    {
        "exploration_save.json",
        "corruption_save.json",
        "inventory_save.json",
    };

    // ── API pública ───────────────────────────────────────────────────────

    public void WipeAllSaves()
    {
        foreach (var fileName in SaveFileNames)
            DeleteFile(Path.Combine(SaveDirectory, fileName));

        DeleteFile(SkillProgressionFilePath);
        DeleteDirectory(ShopStateDirectory);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[SaveFileWiper] Todos os arquivos de save deletados.",
            LogCategory.Player);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void DeleteFile(string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[SaveFileWiper] Arquivo não encontrado (ignorado): {fullPath}",
                    LogCategory.Player);
                return;
            }

            File.Delete(fullPath);
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SaveFileWiper] Deletado: {fullPath}",
                LogCategory.Player);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                $"[SaveFileWiper] Falha ao deletar '{fullPath}': {ex.Message}",
                LogCategory.Player);
        }
    }

    private static void DeleteDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[SaveFileWiper] Diretório não encontrado (ignorado): {directoryPath}",
                    LogCategory.Player);
                return;
            }

            Directory.Delete(directoryPath, recursive: true);
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SaveFileWiper] Diretório deletado: {directoryPath}",
                LogCategory.Player);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                $"[SaveFileWiper] Falha ao deletar diretório '{directoryPath}': {ex.Message}",
                LogCategory.Player);
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(SaveFileWiper))]
    private class SaveFileWiperEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            UnityEditor.EditorGUILayout.Space(8);
            UnityEngine.GUI.enabled = Application.isPlaying;

            var prevColor = UnityEngine.GUI.backgroundColor;
            UnityEngine.GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            if (GUILayout.Button("Wipe All Saves"))
                ((SaveFileWiper)target).WipeAllSaves();

            UnityEngine.GUI.backgroundColor = prevColor;
            UnityEngine.GUI.enabled = true;

            if (!Application.isPlaying)
                UnityEditor.EditorGUILayout.HelpBox(
                    "Entre em Play Mode para usar o botão acima.",
                    UnityEditor.MessageType.Info);
        }
    }
#endif
}