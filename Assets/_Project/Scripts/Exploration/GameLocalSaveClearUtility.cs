using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Apaga ficheiros de save conhecidos em <see cref="Application.persistentDataPath"/>.
/// </summary>
public static class GameLocalSaveClearUtility
{
    public static async Task ClearAllKnownLocalSaveFilesAsync()
    {
        var persistentRoot = Application.persistentDataPath;
        var relativePaths = BuildKnownSaveRelativePaths();

        foreach (var relativePath in relativePaths)
        {
            await TryDeleteFileAsync(Path.Combine(persistentRoot, relativePath));
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[SAVE] Saves locais limpos em '{persistentRoot}'.",
            LogCategory.Player);
    }

    static IReadOnlyList<string> BuildKnownSaveRelativePaths()
    {
        return new[]
        {
            Path.Combine("Saves", "exploration_save.json"),
            "inventory_losable.json",
            "inventory_permanent.json",
            "wallet.json",
            "skillpoint_shop.json",
            Path.Combine("Saves", "enemy_almanac.json"),
            "player_skill_progression.json",
            Path.Combine("Saves", "exploration_corruption.json"),
        };
    }

    static async Task TryDeleteFileAsync(string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                return;
            }

            await Task.Run(() => File.Delete(fullPath));
        }
        catch (Exception exception)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                $"[SAVE] Falha ao apagar '{fullPath}': {exception.Message}",
                LogCategory.Player);
        }
    }
}
