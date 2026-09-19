using System;
using System.Collections.Generic;
using System.IO;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.CharacterStats
{
    /// <summary>
    /// Reads and writes the single shared JSON file holding every
    /// character's base stats. Not a MonoBehaviour — a plain static
    /// service, since status-modifying items call it synchronously from
    /// ExecuteItemEffect() (a void method with no async support).
    ///
    /// Each ApplyModifiers call performs a full read-modify-write cycle:
    /// load the whole file, find or create the target character's entry,
    /// apply every modifier cumulatively against the current stored value,
    /// then save the whole file back. This is deliberately synchronous and
    /// unbatched — using several status items in quick succession triggers
    /// one full file read/write per item, not a single batched write. This
    /// keeps each application correct and simple; if this ever becomes a
    /// performance concern, batching multiple modifiers into one
    /// read-modify-write call (already supported by ApplyModifiers'
    /// signature) is the way to do it, since it already accepts a list.
    /// </summary>
    public static class CharacterStatsRepository
    {
        private const string FileName = "character_stats.json";

        private static string FullPath => Path.Combine(Application.persistentDataPath, FileName);

        public static CharacterStatsFileData LoadAll()
        {
            if (!File.Exists(FullPath))
                return new CharacterStatsFileData();

            try
            {
                string json = File.ReadAllText(FullPath);
                return JsonUtility.FromJson<CharacterStatsFileData>(json) ?? new CharacterStatsFileData();
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to load character stats file: {ex.Message}");
                return new CharacterStatsFileData();
            }
        }

        public static void SaveAll(CharacterStatsFileData data)
        {
            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(FullPath, json);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save character stats file: {ex.Message}");
            }
        }

        public static void DeleteFile()
        {
            if (!File.Exists(FullPath)) return;

            try
            {
                File.Delete(FullPath);
                Log(LogLevel.Debug, $"Character stats file '{FullPath}' deleted.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to delete character stats file: {ex.Message}");
            }
        }

        /// <summary>
        /// Unpacks a single character's stats from the shared file. Returns
        /// a zeroed CharacterStatsData (not yet persisted) if the character
        /// has no entry in the file yet.
        /// </summary>
        public static CharacterStatsData GetStatsForCharacter(string characterId)
        {
            var fileData = LoadAll();
            int index = fileData.Characters.FindIndex(c => c.CharacterId == characterId);

            if (index < 0)
                return new CharacterStatsData();

            return ToStatsData(fileData.Characters[index]);
        }

        /// <summary>
        /// Applies every modifier cumulatively against the character's
        /// current stored value (loaded fresh from disk), then persists the
        /// result immediately. Absolute adds a flat delta; Relative adds a
        /// percentage of the current value. E.g. repeated Absolute +1 on a
        /// stat starting at 0 yields 1, then 2, then 3 — each use reads the
        /// latest persisted value before adding.
        /// </summary>
        public static void ApplyModifiers(string characterId, IReadOnlyList<StatModifierEntry> modifiers)
        {
            if (string.IsNullOrEmpty(characterId) || modifiers == null || modifiers.Count == 0) return;

            var fileData = LoadAll();
            int index = fileData.Characters.FindIndex(c => c.CharacterId == characterId);

            var statsData = index >= 0 ? ToStatsData(fileData.Characters[index]) : new CharacterStatsData();

            foreach (var modifier in modifiers)
            {
                float current = statsData.Get(modifier.StatType);
                float updated = modifier.ModifierType switch
                {
                    StatModifierType.Absolute => current + modifier.Magnitude,
                    StatModifierType.Relative => current + current * (modifier.Magnitude / 100f),
                    _ => current
                };

                statsData.Set(modifier.StatType, updated);
            }

            var updatedEntry = ToFileEntry(characterId, statsData);

            if (index >= 0)
                fileData.Characters[index] = updatedEntry;
            else
                fileData.Characters.Add(updatedEntry);

            SaveAll(fileData);

            Log(LogLevel.Debug, $"Applied {modifiers.Count} modifier(s) to character '{characterId}'.");
        }

        private static CharacterStatsData ToStatsData(CharacterStatsFileData.CharacterEntry entry)
        {
            var data = new CharacterStatsData();
            if (entry.Stats == null) return data;

            foreach (var stat in entry.Stats)
                data.Set(stat.StatType, stat.Value);

            return data;
        }

        private static CharacterStatsFileData.CharacterEntry ToFileEntry(string characterId, CharacterStatsData data)
        {
            var stats = new List<CharacterStatsFileData.StatEntry>();
            foreach (var (statType, value) in data.AsReadOnlyDictionary())
                stats.Add(new CharacterStatsFileData.StatEntry { StatType = statType, Value = value });

            return new CharacterStatsFileData.CharacterEntry { CharacterId = characterId, Stats = stats };
        }

        private static void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[CharacterStatsRepository] {msg}", LogCategory.Inventory);
    }
}
