using System.Collections.Generic;
using System.Text;
using Erumperem.Combat.HealthBars;
using Erumperem.Combat.Runtime;
using Game.Core.Almanac;
using Game.Core.Domain;
using Game.Core.Models;
using TMPro;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Fills EnemyInfo (or any wired TMP fields) with element, revealed actives, and passives.
    /// Wire these on the existing EnemyInfo prefab in Play Mode; unassigned fields are placeholders.
    /// </summary>
    [DefaultExecutionOrder(35)]
    public sealed class EnemyAlmanacPanelPresenter : MonoBehaviour
    {
        [SerializeField] private CombatHudPanelFocusCoordinator panelFocusCoordinator;
        [SerializeField] private TextMeshProUGUI elementLabel;
        [SerializeField] private TextMeshProUGUI activeSkillsLabel;
        [SerializeField] private TextMeshProUGUI passivesLabel;
        [SerializeField] private TextMeshProUGUI combinedBodyLabel;

        private CombatSessionHub _sessionHub;
        private readonly CombatSessionHubSubscription _sessionHubSubscription = new();
        private CombatPrototypeController _combatSession;
        private string _lastRenderedSignature = string.Empty;

        private void Awake()
        {
            _sessionHub = FindFirstObjectByType<CombatSessionHub>();
            panelFocusCoordinator ??= FindFirstObjectByType<CombatHudPanelFocusCoordinator>();
        }

        private void OnEnable()
        {
            _sessionHub ??= FindFirstObjectByType<CombatSessionHub>();
            _sessionHubSubscription.Subscribe(_sessionHub, HandleCombatSessionReady, HandleCombatSessionClosed);
            _sessionHubSubscription.TryCatchUpWithActiveCombatSession(_combatSession);
        }

        private void OnDisable()
        {
            _sessionHubSubscription.Unsubscribe();
        }

        private void LateUpdate()
        {
            RefreshAlmanacPanel();
        }

        private void HandleCombatSessionReady(CombatPrototypeController combatSession)
        {
            _combatSession = combatSession;
        }

        private void HandleCombatSessionClosed()
        {
            if (_combatSession?.BattleState != null)
            {
                EnemyAlmanacPersistentStore.Save(_combatSession.BattleState.EnemyAlmanac);
            }

            _combatSession = null;
            _lastRenderedSignature = string.Empty;
        }

        private void RefreshAlmanacPanel()
        {
            if (_combatSession == null || !_combatSession.IsBattleOngoing || panelFocusCoordinator == null)
            {
                return;
            }

            var focusCombatantId = panelFocusCoordinator.RightFocusCombatantId;
            var focusedCombatant = _combatSession.FindCombatantById(focusCombatantId);
            if (focusedCombatant == null || focusedCombatant.Identity.Faction != Faction.Enemy)
            {
                return;
            }

            var battleState = _combatSession.BattleState;
            if (battleState == null)
            {
                return;
            }

            if (!EnemyCatalogIdentity.TryResolveEnemyCatalogId(focusedCombatant, out var enemyCatalogId))
            {
                enemyCatalogId = focusedCombatant.Identity.DisplayName;
            }

            battleState.EnemyDefinitionsById.TryGetValue(enemyCatalogId, out var catalogDefinition);
            var entry = EnemyAlmanacEntryBuilder.Build(
                enemyCatalogId,
                battleState.EnemyAlmanac,
                catalogDefinition,
                focusedCombatant,
                EnemyAlmanacSkillMap.AsReadOnly(battleState.SkillsById),
                battleState.PassivesById);

            var signature = $"{enemyCatalogId}|{entry.ActiveSkills.Count}|{entry.Element}";
            foreach (var activeSkill in entry.ActiveSkills)
            {
                signature += activeSkill.IsRevealed ? $"|{activeSkill.SkillId}" : "|?";
            }

            if (string.Equals(signature, _lastRenderedSignature, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastRenderedSignature = signature;
            ApplyEntryToLabels(entry);
            EnemyAlmanacPersistentStore.Save(battleState.EnemyAlmanac);
        }

        private void ApplyEntryToLabels(EnemyAlmanacEntry entry)
        {
            if (elementLabel != null)
            {
                elementLabel.text = entry.Element.ToString();
            }

            if (activeSkillsLabel != null)
            {
                var activeText = new StringBuilder();
                foreach (var activeSkill in entry.ActiveSkills)
                {
                    activeText.AppendLine(activeSkill.IsRevealed
                        ? activeSkill.FullDescription
                        : $"{activeSkill.DisplayName}: {activeSkill.FullDescription}");
                }

                activeSkillsLabel.text = activeText.Length == 0 ? "None" : activeText.ToString().TrimEnd();
            }

            if (passivesLabel != null)
            {
                passivesLabel.text = entry.PassiveSummaries.Count == 0
                    ? "None"
                    : string.Join("\n", entry.PassiveSummaries);
            }

            if (combinedBodyLabel != null)
            {
                combinedBodyLabel.text = EnemyAlmanacEntryBuilder.FormatPlayerFacingText(entry);
            }
        }
    }

    internal static class EnemyAlmanacSkillMap
    {
        public static IReadOnlyDictionary<string, SkillDefinition> AsReadOnly(
            IDictionary<string, SkillDefinition> skillsById)
        {
            if (skillsById is IReadOnlyDictionary<string, SkillDefinition> readOnlySkills)
            {
                return readOnlySkills;
            }

            return new Dictionary<string, SkillDefinition>(skillsById);
        }
    }
}
