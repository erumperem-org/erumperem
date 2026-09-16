using System;
using System.Collections.Generic;
using System.IO;
using Game.Core.Data;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Loads the exported combat catalog (skills + passives) for UI that must not
    /// trust stale <c>SkillTreeNodeAsset</c> combat fields.
    /// </summary>
    public static class CombatCatalogRuntimeLookup
    {
        private static Dictionary<string, SkillDefinition> _skillsById;
        private static Dictionary<string, PassiveDefinition> _passivesById;
        private static bool _hasAttemptedLoad;

        public static bool TryGetSkill(string abilityId, out SkillDefinition skillDefinition)
        {
            EnsureCatalogLoaded();
            skillDefinition = null;
            return !string.IsNullOrWhiteSpace(abilityId) &&
                   _skillsById != null &&
                   _skillsById.TryGetValue(abilityId, out skillDefinition);
        }

        public static bool TryGetPassive(string abilityId, out PassiveDefinition passiveDefinition)
        {
            EnsureCatalogLoaded();
            passiveDefinition = null;
            return !string.IsNullOrWhiteSpace(abilityId) &&
                   _passivesById != null &&
                   _passivesById.TryGetValue(abilityId, out passiveDefinition);
        }

        public static IReadOnlyDictionary<string, SkillDefinition> SkillsById
        {
            get
            {
                EnsureCatalogLoaded();
                return _skillsById ?? new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static IReadOnlyDictionary<string, PassiveDefinition> PassivesById
        {
            get
            {
                EnsureCatalogLoaded();
                return _passivesById ?? new Dictionary<string, PassiveDefinition>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void EnsureCatalogLoaded()
        {
            if (_hasAttemptedLoad)
            {
                return;
            }

            _hasAttemptedLoad = true;
            _skillsById = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
            _passivesById = new Dictionary<string, PassiveDefinition>(StringComparer.OrdinalIgnoreCase);

            TryLoadSkills(ResolveCatalogFilePath("skills.json"));
            TryLoadPassives(ResolveCatalogFilePath("passives.json"));
        }

        private static string ResolveCatalogFilePath(string fileName)
        {
            var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "Data", fileName);
            if (File.Exists(streamingAssetsPath))
            {
                return streamingAssetsPath;
            }

            try
            {
                return CombatDataLoader.ResolveDefaultDataPath(fileName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"CombatCatalogRuntimeLookup: {fileName} not found in StreamingAssets. {exception.Message}");
                return string.Empty;
            }
        }

        private static void TryLoadSkills(string skillsPath)
        {
            if (string.IsNullOrEmpty(skillsPath) || !File.Exists(skillsPath))
            {
                return;
            }

            try
            {
                foreach (var skillDefinition in CombatDataLoader.LoadSkills(skillsPath))
                {
                    if (!string.IsNullOrWhiteSpace(skillDefinition.Id))
                    {
                        _skillsById[skillDefinition.Id] = skillDefinition;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"CombatCatalogRuntimeLookup: failed to load skills.json — {exception.Message}");
            }
        }

        private static void TryLoadPassives(string passivesPath)
        {
            if (string.IsNullOrEmpty(passivesPath) || !File.Exists(passivesPath))
            {
                return;
            }

            try
            {
                foreach (var passiveDefinition in CombatDataLoader.LoadPassives(passivesPath))
                {
                    if (!string.IsNullOrWhiteSpace(passiveDefinition.Id))
                    {
                        _passivesById[passiveDefinition.Id] = passiveDefinition;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"CombatCatalogRuntimeLookup: failed to load passives.json — {exception.Message}");
            }
        }
    }
}
