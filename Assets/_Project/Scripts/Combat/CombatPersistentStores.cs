using System.IO;
using System.Text.Json;
using Game.Core.Almanac;
using Game.Core.Items;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Shared save channel for enemy almanac reveals. Combat records uses; hub UI can read the same file.
    /// File: persistentDataPath/Saves/enemy_almanac.json
    /// </summary>
    public static class EnemyAlmanacPersistentStore
    {
        private const string SaveFolderName = "Saves";
        private const string SaveFileName = "enemy_almanac.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        public static string SaveFilePath =>
            Path.Combine(Application.persistentDataPath, SaveFolderName, SaveFileName);

        public static EnemyAlmanacProgress Load()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    return new EnemyAlmanacProgress();
                }

                var json = File.ReadAllText(SaveFilePath);
                var dto = JsonSerializer.Deserialize<EnemyAlmanacSaveDto>(json, JsonOptions);
                return EnemyAlmanacProgress.FromSaveDto(dto);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"EnemyAlmanacPersistentStore: failed to load — {exception.Message}");
                return new EnemyAlmanacProgress();
            }
        }

        public static void Save(EnemyAlmanacProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(progress.ToSaveDto(), JsonOptions);
                File.WriteAllText(SaveFilePath, json);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"EnemyAlmanacPersistentStore: failed to save — {exception.Message}");
            }
        }
    }

    /// <summary>Persistent consumed combat items per characterId. File: persistentDataPath/Saves/combat_item_bonuses.json</summary>
    public static class CombatItemBonusPersistentStore
    {
        private const string SaveFolderName = "Saves";
        private const string SaveFileName = "combat_item_bonuses.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        };

        public static string SaveFilePath =>
            Path.Combine(Application.persistentDataPath, SaveFolderName, SaveFileName);

        public static CombatItemBonusLedger Load()
        {
            try
            {
                if (!File.Exists(SaveFilePath))
                {
                    return new CombatItemBonusLedger();
                }

                var json = File.ReadAllText(SaveFilePath);
                var dto = JsonSerializer.Deserialize<CombatItemBonusLedgerSaveDto>(json, JsonOptions);
                return dto?.ToLedger() ?? new CombatItemBonusLedger();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CombatItemBonusPersistentStore: failed to load — {exception.Message}");
                return new CombatItemBonusLedger();
            }
        }

        public static void Save(CombatItemBonusLedger ledger)
        {
            if (ledger == null)
            {
                return;
            }

            try
            {
                var directory = Path.GetDirectoryName(SaveFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(CombatItemBonusLedgerSaveDto.FromLedger(ledger), JsonOptions);
                File.WriteAllText(SaveFilePath, json);
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"CombatItemBonusPersistentStore: failed to save — {exception.Message}");
            }
        }
    }
}
