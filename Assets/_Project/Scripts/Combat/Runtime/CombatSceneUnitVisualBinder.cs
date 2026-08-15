using System;
using System.Collections.Generic;
using Erumperem.Characters;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Binds scene visual roots and catalog prefabs to combatants in <see cref="BattleState"/>.
    /// </summary>
    public sealed class CombatSceneUnitVisualBinder
    {
        private const string HorseBossCharacterStatId = "HorseBoss";
        private static readonly string[] RandomEncounterExcludedCharacterStatIds = { HorseBossCharacterStatId };

        private readonly CombatSessionRuntime _session;
        private readonly CombatUnitVisualSynchronizer _unitVisualSynchronizer;
        private readonly CombatSceneUnitVisualBinderSettings _settings;

        public CombatSceneUnitVisualBinder(
            CombatSessionRuntime session,
            CombatUnitVisualSynchronizer unitVisualSynchronizer,
            CombatSceneUnitVisualBinderSettings settings)
        {
            _session = session;
            _unitVisualSynchronizer = unitVisualSynchronizer;
            _settings = settings;
        }

        public bool TryBindSceneViewsToBattle()
        {
            var allyCount = _session.State.Allies.Count;
            var enemyCount = _session.State.Enemies.Count;

            if (_settings.AllyVisualRoots == null || _settings.AllyVisualRoots.Length != allyCount)
            {
                Debug.LogError(
                    $"CombatPrototypeController: esperados {allyCount} Ally Visual Roots (ally_1..ally_{allyCount}). " +
                    $"Atual: {(_settings.AllyVisualRoots == null ? 0 : _settings.AllyVisualRoots.Length)}.");
                return false;
            }

            if (_settings.EnemyVisualRoots == null || _settings.EnemyVisualRoots.Length != enemyCount)
            {
                Debug.LogError(
                    $"CombatPrototypeController: esperados {enemyCount} Enemy Visual Roots (enemy_1..enemy_{enemyCount}). " +
                    $"Atual: {(_settings.EnemyVisualRoots == null ? 0 : _settings.EnemyVisualRoots.Length)}.");
                return false;
            }

            for (var allyIndex = 0; allyIndex < allyCount; allyIndex++)
            {
                var slotRoot = _settings.AllyVisualRoots[allyIndex];
                if (slotRoot == null)
                {
                    Debug.LogError($"CombatPrototypeController: Ally Visual Roots[{allyIndex}] está vazio.");
                    return false;
                }

                var ally = _session.State.Allies[allyIndex];
                var partyCharacterNames = CombatPartyResolver.GetCombatAllyCharacterNames();
                var characterName = allyIndex < partyCharacterNames.Count
                    ? partyCharacterNames[allyIndex]
                    : null;
                var allyViewRoot = slotRoot;

                if (_settings.AllyCharacterStatCatalog != null &&
                    !string.IsNullOrWhiteSpace(characterName) &&
                    _settings.AllyCharacterStatCatalog.TryGetDefinition(characterName, out var allyCharacterStatDefinition))
                {
                    if (allyCharacterStatDefinition.BattlePrefab != null)
                    {
                        var instantiatedAllyRoot = BattleVisualInstaller.InstantiateAllyUnderSlot(
                            slotRoot,
                            allyCharacterStatDefinition.BattlePrefab);
                        if (instantiatedAllyRoot != null)
                        {
                            allyViewRoot = instantiatedAllyRoot;
                            Debug.Log(
                                $"CombatPrototypeController: modelo '{characterName}' instanciado em {slotRoot.name}.",
                                allyCharacterStatDefinition.BattlePrefab);
                        }
                        else
                        {
                            Debug.LogError(
                                $"CombatPrototypeController: falha ao instanciar battlePrefab de '{characterName}' em {slotRoot.name}.",
                                allyCharacterStatDefinition.BattlePrefab);
                        }
                    }
                    else
                    {
                        BattleVisualInstaller.ClearSlotForBattlePrefab(slotRoot);
                        Debug.LogWarning(
                            $"CombatPrototypeController: '{characterName}' não tem battlePrefab no catálogo; " +
                            $"slot {slotRoot.name} sem modelo visual.",
                            _settings.AllyCharacterStatCatalog);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(characterName))
                {
                    Debug.LogError(
                        $"CombatPrototypeController: definição de aliado '{characterName}' não encontrada no catálogo.",
                        _settings.AllyCharacterStatCatalog);
                }

                if (!string.IsNullOrWhiteSpace(characterName))
                {
                    var existingIdentity = ally.Identity;
                    ally.Identity = new IdentityComponent
                    {
                        Id = existingIdentity.Id,
                        DisplayName = characterName,
                        Faction = existingIdentity.Faction,
                        Tags = existingIdentity.Tags,
                    };
                }

                EnsureCombatCapsuleTagOnUnit(allyViewRoot, ally.Identity.Id);
                BattleVisualInstaller.PrepareAllyVisualForCombat(allyViewRoot);
                BattleVisualInstaller.EnsureCombatSelectionCollider(allyViewRoot, characterName);
                RegisterUnitVisual(ally.Identity.Id, allyViewRoot);
            }

            ExplorationLoadContext.EnsureRuntimeInstance();

            var horseBossEnemySlotIndex = -1;
            var hasHorseBossEncounter = CombatExplorationBridge.TryConsumePendingHorseBossEncounter(
                out horseBossEnemySlotIndex);

            if (hasHorseBossEncounter)
            {
                Debug.Log(
                    $"CombatPrototypeController: encounter Horse Boss — slot enemy_{horseBossEnemySlotIndex + 1}.",
                    _settings.LogContext);
            }
            else
            {
                Debug.Log(
                    "CombatPrototypeController: combate normal (sem encounter Horse Boss pendente).",
                    _settings.LogContext);
            }

            for (var enemyIndex = 0; enemyIndex < enemyCount; enemyIndex++)
            {
                var slotRoot = _settings.EnemyVisualRoots[enemyIndex];
                if (slotRoot == null)
                {
                    Debug.LogError($"CombatPrototypeController: Enemy Visual Roots[{enemyIndex}] está vazio.");
                    return false;
                }

                var enemy = _session.State.Enemies[enemyIndex];
                var enemyViewRoot = slotRoot;

                if (_settings.SpawnEnemyModelsFromCatalog &&
                    _settings.EnemyVisualSpawnCatalog != null &&
                    TrySpawnRandomCatalogEnemyAtSlot(slotRoot, enemy, out var catalogEnemyViewRoot))
                {
                    enemyViewRoot = catalogEnemyViewRoot;
                }

                if (hasHorseBossEncounter && enemyIndex == horseBossEnemySlotIndex)
                {
                    if (!TryReplaceEnemySlotWithHorseBoss(slotRoot, enemy, out var horseBossViewRoot))
                    {
                        Debug.LogError(
                            $"CombatPrototypeController: falha ao substituir enemy_{enemyIndex + 1} pelo Horse Boss.",
                            _settings.LogContext);
                        return false;
                    }

                    enemyViewRoot = horseBossViewRoot;
                }

                EnsureCombatCapsuleTagOnUnit(enemyViewRoot, enemy.Identity.Id);
                RegisterUnitVisual(enemy.Identity.Id, enemyViewRoot);
            }

            return true;
        }

        public void TrySpawnSummonedEnemyVisual(CombatEvent combatEvent)
        {
            if (combatEvent.EventType != BattleEventType.CombatantSpawned ||
                string.IsNullOrEmpty(combatEvent.TargetId))
            {
                return;
            }

            var spawnedCombatant = _session.FindCombatantById(combatEvent.TargetId);
            if (spawnedCombatant == null)
            {
                return;
            }

            var archetypeId = combatEvent.SkillId;
            if (!TryResolveEnemyVisualDefinitionByArchetypeId(archetypeId, out var enemyVisualDefinition) ||
                enemyVisualDefinition.battlePrefab == null)
            {
                Debug.LogWarning(
                    $"CombatPrototypeController: sem visual para arquétipo invocado '{archetypeId}'.",
                    _settings.LogContext);
                return;
            }

            var rankIndex = ResolveEnemyVisualRootIndex(spawnedCombatant, combatEvent.PassiveAuxInt);
            if (_settings.EnemyVisualRoots == null || rankIndex < 0 || rankIndex >= _settings.EnemyVisualRoots.Length)
            {
                Debug.LogWarning(
                    $"CombatPrototypeController: slot inválido {rankIndex} para spawn de '{archetypeId}' " +
                    $"(combatente '{spawnedCombatant.Identity.Id}').",
                    _settings.LogContext);
                return;
            }

            var slotRoot = _settings.EnemyVisualRoots[rankIndex];
            if (slotRoot == null)
            {
                return;
            }

            EnemyVisualBattleInstaller.ClearSlotForEnemyVisualPrefab(slotRoot);
            var alliesFacingReference = ResolveAlliesFacingReference();
            var instantiatedEnemyRoot = EnemyVisualBattleInstaller.InstantiateEnemyUnderSlot(
                slotRoot,
                enemyVisualDefinition.battlePrefab,
                alliesFacingReference);
            if (instantiatedEnemyRoot == null)
            {
                Debug.LogError(
                    $"CombatPrototypeController: falha ao instanciar prefab de '{archetypeId}' no rank {combatEvent.PassiveAuxInt}.",
                    enemyVisualDefinition);
                return;
            }

            OverrideEnemySkillLoadoutFromVisualDefinition(spawnedCombatant, enemyVisualDefinition);
            ApplyEnemyCharacterStatsFromCatalog(spawnedCombatant, enemyVisualDefinition);
            ApplyEnemyPassiveIdsFromVisualDefinition(spawnedCombatant, enemyVisualDefinition);
            EnsureCombatCapsuleTagOnUnit(instantiatedEnemyRoot, spawnedCombatant.Identity.Id);
            RegisterUnitVisual(spawnedCombatant.Identity.Id, instantiatedEnemyRoot);
            _session.EnemyVisualByCombatantId[spawnedCombatant.Identity.Id] = enemyVisualDefinition;
        }

        private void RegisterUnitVisual(string combatantId, Transform unitRoot)
        {
            _session.UnitVisualRootsByCombatantId[combatantId] = unitRoot;
            _unitVisualSynchronizer.RegisterUnitVisual(combatantId, unitRoot);
        }

        private bool TrySpawnRandomCatalogEnemyAtSlot(
            Transform slotRoot,
            Combatant enemy,
            out Transform enemyViewRoot)
        {
            enemyViewRoot = slotRoot;
            if (_settings.EnemyVisualSpawnCatalog == null)
            {
                return false;
            }

            if (!_settings.EnemyVisualSpawnCatalog.TryPickDefinitionExcludingCharacterStatIds(
                    _session.Random,
                    RandomEncounterExcludedCharacterStatIds,
                    out var enemyVisualDefinition) ||
                enemyVisualDefinition.battlePrefab == null)
            {
                return false;
            }

            var alliesFacingReference = ResolveAlliesFacingReference();
            var instantiatedEnemyRoot = EnemyVisualBattleInstaller.InstantiateEnemyUnderSlot(
                slotRoot,
                enemyVisualDefinition.battlePrefab,
                alliesFacingReference);
            if (instantiatedEnemyRoot != null)
            {
                enemyViewRoot = instantiatedEnemyRoot;
            }

            OverrideEnemySkillLoadoutFromVisualDefinition(enemy, enemyVisualDefinition);
            ApplyEnemyCharacterStatsFromCatalog(enemy, enemyVisualDefinition);
            ApplyEnemyPassiveIdsFromVisualDefinition(enemy, enemyVisualDefinition);
            _session.EnemyVisualByCombatantId[enemy.Identity.Id] = enemyVisualDefinition;
            return true;
        }

        private bool TryReplaceEnemySlotWithHorseBoss(
            Transform slotRoot,
            Combatant enemy,
            out Transform horseBossViewRoot)
        {
            horseBossViewRoot = slotRoot;
            if (!TryResolveHorseBossVisualDefinition(out var horseBossVisual) ||
                horseBossVisual.battlePrefab == null)
            {
                Debug.LogError(
                    "CombatPrototypeController: HorseBossVisualDefinition ou battlePrefab em falta.",
                    _settings.LogContext);
                return false;
            }

            EnemyVisualBattleInstaller.ClearSlotForEnemyVisualPrefab(slotRoot);

            if (!EnemySpawnHelper.TryApplyEnemyArchetypeToCombatant(
                    _session.State,
                    enemy,
                    "horse_boss",
                    BattleFactory.DefaultEnemySkillIds))
            {
                Debug.LogWarning(
                    "CombatPrototypeController: template horse_boss não encontrado; " +
                    "aplicando só visual/stats do Horse Boss.",
                    _settings.LogContext);
            }

            var alliesFacingReference = ResolveAlliesFacingReference();
            var instantiatedHorseBossRoot = EnemyVisualBattleInstaller.InstantiateEnemyUnderSlot(
                slotRoot,
                horseBossVisual.battlePrefab,
                alliesFacingReference);
            if (instantiatedHorseBossRoot == null)
            {
                Debug.LogError(
                    "CombatPrototypeController: falha ao instanciar prefab do Horse Boss.",
                    horseBossVisual.battlePrefab);
                return false;
            }

            horseBossViewRoot = instantiatedHorseBossRoot;
            OverrideEnemySkillLoadoutFromVisualDefinition(enemy, horseBossVisual);
            ApplyEnemyCharacterStatsFromCatalog(enemy, horseBossVisual);
            ApplyEnemyPassiveIdsFromVisualDefinition(enemy, horseBossVisual);
            _session.EnemyVisualByCombatantId[enemy.Identity.Id] = horseBossVisual;

            Debug.Log(
                $"CombatPrototypeController: Horse Boss aplicado a {enemy.Identity.Id} " +
                $"(display '{enemy.Identity.DisplayName}').",
                horseBossVisual);
            return true;
        }

        private void OverrideEnemySkillLoadoutFromVisualDefinition(
            Combatant enemy,
            EnemyVisualDefinition enemyVisualDefinition)
        {
            if (enemyVisualDefinition.enemySkillIds == null || enemyVisualDefinition.enemySkillIds.Length == 0)
            {
                return;
            }

            var validSkillIds = new List<string>();
            foreach (var candidateSkillId in enemyVisualDefinition.enemySkillIds)
            {
                if (string.IsNullOrWhiteSpace(candidateSkillId))
                {
                    continue;
                }

                if (!_session.State.SkillsById.ContainsKey(candidateSkillId))
                {
                    Debug.LogWarning(
                        $"EnemyVisualDefinition '{enemyVisualDefinition.name}': skill '{candidateSkillId}' não está em skills.json — ignorada.",
                        enemyVisualDefinition);
                    continue;
                }

                validSkillIds.Add(candidateSkillId);
            }

            if (validSkillIds.Count == 0)
            {
                return;
            }

            enemy.SkillLoadout.Skills.Clear();
            enemy.SkillLoadout.Skills.AddRange(validSkillIds);
        }

        private void ApplyEnemyCharacterStatsFromCatalog(
            Combatant enemy,
            EnemyVisualDefinition enemyVisualDefinition)
        {
            if (_settings.EnemyCharacterStatCatalog == null || enemy == null || enemyVisualDefinition == null)
            {
                return;
            }

            var characterStatId = enemyVisualDefinition.ResolveCharacterStatId();
            if (string.IsNullOrWhiteSpace(characterStatId))
            {
                return;
            }

            if (!_settings.EnemyCharacterStatCatalog.TryGetDefinition(
                    characterStatId,
                    out var enemyCharacterStatDefinition))
            {
                return;
            }

            enemyCharacterStatDefinition.ApplyToCombatant(enemy);
        }

        private void ApplyEnemyPassiveIdsFromVisualDefinition(
            Combatant enemy,
            EnemyVisualDefinition enemyVisualDefinition)
        {
            if (enemy == null || enemyVisualDefinition?.enemyPassiveIds == null)
            {
                return;
            }

            foreach (var passiveId in enemyVisualDefinition.enemyPassiveIds)
            {
                if (string.IsNullOrWhiteSpace(passiveId))
                {
                    continue;
                }

                enemy.Progression.UnlockedNodes[passiveId] = true;
            }
        }

        private bool TryResolveHorseBossVisualDefinition(out EnemyVisualDefinition resolvedHorseBossVisualDefinition)
        {
            if (_settings.HorseBossVisualDefinition != null)
            {
                resolvedHorseBossVisualDefinition = _settings.HorseBossVisualDefinition;
                return true;
            }

            if (TryResolveEnemyVisualDefinitionByArchetypeId("HorseBoss", out resolvedHorseBossVisualDefinition))
            {
                return true;
            }

            var loadedHorseBossDefinitions = Resources.FindObjectsOfTypeAll<EnemyVisualDefinition>();
            for (var definitionIndex = 0; definitionIndex < loadedHorseBossDefinitions.Length; definitionIndex++)
            {
                var candidateDefinition = loadedHorseBossDefinitions[definitionIndex];
                if (candidateDefinition == null)
                {
                    continue;
                }

                if (!string.Equals(
                        candidateDefinition.ResolveCharacterStatId(),
                        HorseBossCharacterStatId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                resolvedHorseBossVisualDefinition = candidateDefinition;
                return candidateDefinition.battlePrefab != null;
            }

            resolvedHorseBossVisualDefinition = null;
            return false;
        }

        private int ResolveEnemyVisualRootIndex(Combatant spawnedCombatant, int fallbackOneBasedSlotFromEvent)
        {
            if (_session.State?.Enemies == null || spawnedCombatant == null)
            {
                return fallbackOneBasedSlotFromEvent - 1;
            }

            for (var enemyIndex = 0; enemyIndex < _session.State.Enemies.Count; enemyIndex++)
            {
                if (ReferenceEquals(_session.State.Enemies[enemyIndex], spawnedCombatant))
                {
                    return enemyIndex;
                }
            }

            if (TryParseEnemySlotIndexFromCombatantId(spawnedCombatant.Identity.Id, out var slotIndexFromId))
            {
                return slotIndexFromId;
            }

            return fallbackOneBasedSlotFromEvent - 1;
        }

        private static bool TryParseEnemySlotIndexFromCombatantId(string combatantId, out int slotIndex)
        {
            slotIndex = -1;
            if (string.IsNullOrEmpty(combatantId) || !combatantId.StartsWith("enemy_", StringComparison.Ordinal))
            {
                return false;
            }

            if (!int.TryParse(combatantId.AsSpan("enemy_".Length), out var oneBasedSlotNumber))
            {
                return false;
            }

            slotIndex = oneBasedSlotNumber - 1;
            return slotIndex >= 0;
        }

        private bool TryResolveEnemyVisualDefinitionByArchetypeId(
            string archetypeId,
            out EnemyVisualDefinition enemyVisualDefinition)
        {
            enemyVisualDefinition = null;
            if (string.IsNullOrWhiteSpace(archetypeId) || _settings.EnemyVisualSpawnCatalog?.Definitions == null)
            {
                return false;
            }

            foreach (var candidateDefinition in _settings.EnemyVisualSpawnCatalog.Definitions)
            {
                if (candidateDefinition == null)
                {
                    continue;
                }

                if (string.Equals(
                        candidateDefinition.ResolveCharacterStatId(),
                        archetypeId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    enemyVisualDefinition = candidateDefinition;
                    return true;
                }
            }

            return false;
        }

        private Transform ResolveAlliesFacingReference()
        {
            if (_settings.AllyVisualRoots == null || _settings.AllyVisualRoots.Length == 0)
            {
                return null;
            }

            for (var allyIndex = 0; allyIndex < _settings.AllyVisualRoots.Length; allyIndex++)
            {
                var allyVisualRoot = _settings.AllyVisualRoots[allyIndex];
                if (allyVisualRoot != null)
                {
                    return allyVisualRoot;
                }
            }

            return null;
        }

        private static void EnsureCombatCapsuleTagOnUnit(Transform unitRoot, string combatantId)
        {
            DestroyCombatCapsuleTagsOnDescendants(unitRoot);
            var tag = unitRoot.GetComponent<CombatCapsuleTag>();
            if (tag == null)
            {
                tag = unitRoot.gameObject.AddComponent<CombatCapsuleTag>();
            }

            tag.combatantId = combatantId;
        }

        private static void DestroyCombatCapsuleTagsOnDescendants(Transform parentTransform)
        {
            for (var childIndex = 0; childIndex < parentTransform.childCount; childIndex++)
            {
                var childTransform = parentTransform.GetChild(childIndex);
                foreach (var combatCapsuleTag in childTransform.GetComponents<CombatCapsuleTag>())
                {
                    UnityEngine.Object.Destroy(combatCapsuleTag);
                }

                DestroyCombatCapsuleTagsOnDescendants(childTransform);
            }
        }
    }

    public sealed class CombatSceneUnitVisualBinderSettings
    {
        public Transform[] AllyVisualRoots { get; set; }
        public Transform[] EnemyVisualRoots { get; set; }
        public bool SpawnEnemyModelsFromCatalog { get; set; }
        public EnemyVisualSpawnCatalog EnemyVisualSpawnCatalog { get; set; }
        public EnemyVisualDefinition HorseBossVisualDefinition { get; set; }
        public AllyCharacterStatCatalog AllyCharacterStatCatalog { get; set; }
        public EnemyCharacterStatCatalog EnemyCharacterStatCatalog { get; set; }
        public UnityEngine.Object LogContext { get; set; }
    }
}
