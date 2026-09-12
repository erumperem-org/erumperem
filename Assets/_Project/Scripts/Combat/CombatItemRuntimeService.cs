using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Erumperem.Combat.Authoring;
using Erumperem.Progression;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Items;
using Game.Core.Progression;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Applies combat items from the existing inventory Execute button.
    /// Status/thematic items record onto the bonus ledger (characterId of current Main).
    /// Utility items reset skill trees through PlayerProgressionService.
    /// Inventory slot rarity backgrounds are optional placeholders if the prefab has no frame Image.
    /// </summary>
    public static class CombatItemRuntimeService
    {
        public static void ExecuteFromInventory(CombatItemAsset itemAsset)
        {
            if (itemAsset == null)
            {
                return;
            }

            var itemDefinition = itemAsset.ToRuntimeDefinition();
            if (itemDefinition.Kind == CombatItemKind.Utility)
            {
                ExecuteUtilityReset(itemDefinition);
                return;
            }

            ExecuteStatItem(itemDefinition);
        }

        private static void ExecuteUtilityReset(CombatItemDefinition itemDefinition)
        {
            var characterIdsToReset = SkillTreeResetRules.ResolveCharacterIdsToReset(itemDefinition.UtilityKind);
            var progressionService = PlayerProgressionService.Instance ??
                                     UnityEngine.Object.FindFirstObjectByType<PlayerProgressionService>(FindObjectsInactive.Include);
            if (progressionService == null)
            {
                Debug.LogError("CombatItemRuntimeService: PlayerProgressionService missing; skill-tree reset skipped.");
                return;
            }

            if (itemDefinition.UtilityKind == CombatItemUtilityKind.ResetAllSkillTrees)
            {
                progressionService.ResetAllCharacters();
                return;
            }

            foreach (var characterId in characterIdsToReset)
            {
                progressionService.ResetCharacter(characterId);
            }
        }

        private static void ExecuteStatItem(CombatItemDefinition itemDefinition)
        {
            var characterId = ResolveTargetProgressionCharacterId();
            if (string.IsNullOrWhiteSpace(characterId))
            {
                Debug.LogError("CombatItemRuntimeService: no playable characterId; stat item was not applied.");
                return;
            }

            var ledger = CombatItemBonusPersistentStore.Load();
            ledger.RecordAppliedItem(characterId, itemDefinition.Id);
            CombatItemBonusPersistentStore.Save(ledger);

            TryApplyHealthToExplorationBar(characterId, ledger);
        }

        private static string ResolveTargetProgressionCharacterId()
        {
            var playableCharactersManager = UnityEngine.Object.FindFirstObjectByType<PlayableCharactersManager>(FindObjectsInactive.Include);
            if (playableCharactersManager?.Main is PlayableCharacter mainCharacter)
            {
                if (mainCharacter.definition != null &&
                    !string.IsNullOrWhiteSpace(mainCharacter.definition.ProgressionCharacterId))
                {
                    return mainCharacter.definition.ProgressionCharacterId;
                }

                if (!string.IsNullOrWhiteSpace(mainCharacter.CharacterName))
                {
                    return mainCharacter.CharacterName.Trim().ToLowerInvariant();
                }
            }

            return SkillTreeResetRules.WulfricCharacterId;
        }

        private static void TryApplyHealthToExplorationBar(string characterId, CombatItemBonusLedger ledger)
        {
            var playableCharactersManager = UnityEngine.Object.FindFirstObjectByType<PlayableCharactersManager>(FindObjectsInactive.Include);
            if (playableCharactersManager == null)
            {
                return;
            }

            var targetCharacter = ResolvePlayableCharacterForProgressionId(playableCharactersManager, characterId);
            if (targetCharacter?.HealthBar == null || targetCharacter.definition == null)
            {
                return;
            }

            var statDefinition = targetCharacter.definition;
            var itemsById = LoadItemsById();
            var baseline = new CombatantBaseStatSnapshot
            {
                MaxHp = statDefinition.MaxHitPoints,
                DefenseChance = statDefinition.DefenseChance,
                CritChance = statDefinition.CritChance,
                Speed = statDefinition.Speed,
                Accuracy = statDefinition.Accuracy,
            };
            var modified = ledger.ComputeModifiedStats(characterId, baseline, itemsById);
            var previousMaxHealth = targetCharacter.HealthBar.MaxHealth;
            targetCharacter.HealthBar.SetMaxHealth(modified.MaxHp, keepRatio: false);
            var gainedHitPoints = modified.MaxHp - previousMaxHealth;
            if (gainedHitPoints > 0f)
            {
                targetCharacter.HealthBar.Heal(gainedHitPoints);
            }

            UnityEngine.Object.FindFirstObjectByType<CharacterViewHud>()?.RefreshAll();
            ExplorationLoadContext.Instance?.SaveState();
        }

        private static PlayableCharacter ResolvePlayableCharacterForProgressionId(
            PlayableCharactersManager playableCharactersManager,
            string characterId)
        {
            if (playableCharactersManager.Main is PlayableCharacter mainCharacter &&
                MatchesProgressionCharacter(mainCharacter, characterId))
            {
                return mainCharacter;
            }

            if (playableCharactersManager.Companion is PlayableCharacter companionCharacter &&
                MatchesProgressionCharacter(companionCharacter, characterId))
            {
                return companionCharacter;
            }

            return playableCharactersManager.Main as PlayableCharacter;
        }

        private static bool MatchesProgressionCharacter(PlayableCharacter playableCharacter, string characterId)
        {
            if (playableCharacter.definition != null &&
                string.Equals(
                    playableCharacter.definition.ProgressionCharacterId,
                    characterId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(playableCharacter.CharacterName, characterId, StringComparison.OrdinalIgnoreCase);
        }

        public static IReadOnlyDictionary<string, CombatItemDefinition> LoadItemsById()
        {
            try
            {
                var itemsPath = Path.Combine(Application.streamingAssetsPath, "Data", "items.json");
                if (File.Exists(itemsPath))
                {
                    return CombatDataLoader.LoadItems(itemsPath)
                        .ToDictionary(item => item.Id, System.StringComparer.Ordinal);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"CombatItemRuntimeService: items.json load failed — {exception.Message}");
            }

            return CombatItemCatalogFactory.CreateDefaultCatalogById();
        }
    }
}
