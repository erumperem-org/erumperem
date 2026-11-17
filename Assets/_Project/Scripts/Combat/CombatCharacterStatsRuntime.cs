
using System;
using System.Collections.Generic;
using Core.CharacterStats;
using Erumperem.Characters;
using Erumperem.Combat.Runtime;
using Game.Core.Domain;
using Game.Core.Items;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Integra Character Definitions e modificadores de itens antes do primeiro turno.
    /// O instalador localiza o CombatSessionHub em execução: não altera cenas.
    /// </summary>
    public sealed class CombatCharacterStatsRuntime : MonoBehaviour
    {
        private CombatSessionHub currentHub;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            GameObject go = new GameObject("CombatCharacterStatsRuntime");
            DontDestroyOnLoad(go);
            go.AddComponent<CombatCharacterStatsRuntime>();
        }

        private void Update()
        {
            if (currentHub != null)
            {
                return;
            }

            CombatSessionHub found = FindFirstObjectByType<CombatSessionHub>();

            if (found != null)
            {
                currentHub = found;
                currentHub.OnBattleInitiativeResolved += HandleInitiative;
            }
        }

        private void OnDisable()
        {
            if (currentHub != null)
            {
                currentHub.OnBattleInitiativeResolved -= HandleInitiative;
                currentHub = null;
            }
        }

        private void HandleInitiative(Side side)
        {
            CombatPrototypeController controller = FindFirstObjectByType<CombatPrototypeController>();

            if (controller == null || controller.BattleState == null)
            {
                return;
            }

            AllyCharacterStatCatalog catalog = CombatCatalogLocator.ResolveAllyCharacterStatCatalog(null);

            if (catalog == null)
            {
                Debug.LogError("[CombatCharacterStatsRuntime] AllyCharacterStatCatalog não encontrado.");
                return;
            }

            IReadOnlyList<string> party = CombatPartyResolver.GetCombatAllyCharacterNames();
            CombatItemBonusLedger oldLedger = CombatItemBonusPersistentStore.Load();
            var oldItems = CombatItemRuntimeService.LoadItemsById();

            for (int index = 0; index < controller.BattleState.Allies.Count && index < party.Count; index++)
            {
                string characterName = party[index];
                Combatant ally = controller.BattleState.Allies[index];

                if (!catalog.TryGetDefinition(characterName, out AllyCharacterStatDefinition definition))
                {
                    Debug.LogWarning("[CombatCharacterStatsRuntime] Definition ausente: " + characterName);
                    continue;
                }

                CharacterStatsData delta = CharacterStatsRepository.IsAvailable
                    ? CharacterStatsRepository.GetStatsForCharacter(definition.CharacterId)
                    : new CharacterStatsData();

                CombatantBaseStatSnapshot baseline = new CombatantBaseStatSnapshot
                {
                    MaxHp = Math.Max(1, (int)Math.Round(definition.MaxHitPoints + delta.Get(StatType.Health))),
                    DefenseChance = Math.Clamp(definition.DefenseChance + delta.Get(StatType.Defense), 0d, 1d),
                    CritChance = Math.Clamp(definition.CritChance + delta.Get(StatType.Critical), 0d, 1d),
                    Speed = definition.Speed,
                    Accuracy = definition.Accuracy
                };

                string id = definition.ProgressionCharacterId;
                CombatantBaseStatSnapshot modified = string.IsNullOrWhiteSpace(id)
                    ? baseline
                    : oldLedger.ComputeModifiedStats(id, baseline, oldItems);

                CombatItemStatApplier.WriteStatsToCombatant(ally, ally.Health.MaxHp, modified);
            }
        }
    }
}
