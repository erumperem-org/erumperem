using System;
using System.Collections.Generic;
using System.IO;
using Erumperem.Characters;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.CharacterStats
{
    /// <summary>
    /// Reads and writes the JSON file holding every character's SAVED
    /// MODIFICATIONS (deltas over the base stats defined in
    /// AllyCharacterStatDefinition). Not a MonoBehaviour — a plain static
    /// service, since items call it synchronously from ExecuteItemEffect().
    ///
    /// The file lives inside the ACTIVE SAVE SLOT
    /// (SaveSlotAccess.Data.SlotDirectory), so each slot has its own stats,
    /// independent of the others. With no slot selected, reads return empty
    /// data, writes are aborted, and <see cref="IsAvailable"/> is false.
    ///
    /// Characters are keyed by AllyCharacterStatDefinition.CharacterId, so
    /// every save / edit / delete goes to the correct entry.
    ///
    /// I/O is synchronous on purpose (items need the result immediately);
    /// each ApplyModifiers call is a full read-modify-write cycle.
    /// </summary>
    public static class CharacterStatsRepository
    {
        private const string FileName = "character_stats.json";

        /// <summary>True when a save slot is selected, i.e. stats can be read and written.</summary>
        public static bool IsAvailable => TryGetFilePath(out _);

        private static bool TryGetFilePath(out string path)
        {
            var slotData = SaveSlotAccess.Data;

            if (slotData == null || !slotData.HasSlotSelected)
            {
                path = null;
                return false;
            }

            path = Path.Combine(slotData.SlotDirectory, FileName);
            return true;
        }

        public static CharacterStatsFileData LoadAll()
        {
            if (!TryGetFilePath(out string path))
            {
                Log(LogLevel.Error, "No save slot selected (SaveSlotAccess.Data) — returning empty stats.");
                return new CharacterStatsFileData();
            }

            if (!File.Exists(path))
                return new CharacterStatsFileData();

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<CharacterStatsFileData>(json) ?? new CharacterStatsFileData();
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to load character stats file '{path}': {ex.Message}");
                return new CharacterStatsFileData();
            }
        }

        /// <summary>Returns false (and writes nothing) if no slot is selected or the write fails.</summary>
        public static bool SaveAll(CharacterStatsFileData data)
        {
            if (!TryGetFilePath(out string path))
            {
                Log(LogLevel.Error, "No save slot selected (SaveSlotAccess.Data) — save aborted.");
                return false;
            }

            try
            {
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save character stats file '{path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>Deletes the stats file of the ACTIVE slot only (every character in it).</summary>
        public static void DeleteFile()
        {
            if (!TryGetFilePath(out string path))
            {
                Log(LogLevel.Error, "No save slot selected (SaveSlotAccess.Data) — nothing deleted.");
                return;
            }

            if (!File.Exists(path)) return;

            try
            {
                File.Delete(path);
                Log(LogLevel.Debug, $"Character stats file '{path}' deleted.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to delete character stats file: {ex.Message}");
            }
        }

        /// <summary>Removes only one character's saved modifications from the active slot, leaving the others intact.</summary>
        public static void DeleteCharacter(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || !IsAvailable) return;

            var fileData = LoadAll();
            int removed = fileData.Characters.RemoveAll(c => c.CharacterId == characterId);
            if (removed == 0) return;

            if (SaveAll(fileData))
                Log(LogLevel.Debug, $"Saved stats of character '{characterId}' deleted.");
        }

        /// <summary>
        /// Saved modifications (deltas) of one character. Zeroed if the
        /// character has no entry yet (or no slot is selected). This is NOT
        /// the current value — see <see cref="GetEffectiveStat"/>.
        /// </summary>
        public static CharacterStatsData GetStatsForCharacter(string characterId)
        {
            var fileData = LoadAll();
            int index = fileData.Characters.FindIndex(c => c.CharacterId == characterId);

            if (index < 0)
                return new CharacterStatsData();

            return ToStatsData(fileData.Characters[index]);
        }

        /// <summary>Current value = base (definition) + saved modification (file).</summary>
        public static float GetEffectiveStat(AllyCharacterStatDefinition definition, StatType stat)
        {
            if (definition == null) return 0f;
            return CharacterStatResolver.GetEffective(definition, GetStatsForCharacter(definition.CharacterId), stat);
        }

        /// <summary>
        /// Applies every resolved modifier cumulatively against the character's
        /// current value (base + stored delta, loaded fresh from disk) and
        /// persists immediately into the active slot. Returns false if nothing
        /// was saved. Does NOT evaluate applicability rules — items must check
        /// <see cref="StatApplicabilityRule"/> first.
        /// </summary>
        public static bool ApplyModifiers(AllyCharacterStatDefinition target, IReadOnlyList<ResolvedStatModifier> modifiers)
        {
            if (target == null || string.IsNullOrEmpty(target.CharacterId)) return false;
            if (modifiers == null || modifiers.Count == 0) return false;

            if (!IsAvailable)
            {
                Log(LogLevel.Error, "No save slot selected (SaveSlotAccess.Data) — modifiers NOT applied.");
                return false;
            }

            string characterId = target.CharacterId;
            var fileData = LoadAll();
            int index = fileData.Characters.FindIndex(c => c.CharacterId == characterId);

            var saved = index >= 0 ? ToStatsData(fileData.Characters[index]) : new CharacterStatsData();

            foreach (var modifier in modifiers)
            {
                float baseValue = CharacterStatResolver.GetBase(target, modifier.Stat);
                float delta = saved.Get(modifier.Stat);

                saved.Set(modifier.Stat, delta + StatModifierMath.ComputeDeltaChange(baseValue, delta, modifier));
            }

            var updatedEntry = ToFileEntry(characterId, saved);

            if (index >= 0)
                fileData.Characters[index] = updatedEntry;
            else
                fileData.Characters.Add(updatedEntry);

            if (!SaveAll(fileData)) return false;

            Log(LogLevel.Debug, $"Applied {modifiers.Count} modifier(s) to character '{characterId}'.");
            return true;
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