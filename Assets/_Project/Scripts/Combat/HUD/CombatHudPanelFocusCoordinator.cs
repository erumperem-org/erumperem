using System;
using System.Linq;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// Resolve qual combatente mostrar nos painéis esquerdo (aliado) e direito (foco contextual) da HUD.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class CombatHudPanelFocusCoordinator : MonoBehaviour
    {
        [SerializeField] private GameObject combatLogicCenter;
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        private CombatSessionHub _sessionHub;
        private CombatPrototypeController _combatSession;

        private bool _isActionPresentationActive;
        private string _presentationActorCombatantId = string.Empty;
        private string _presentationTargetCombatantId = string.Empty;

        public string LeftAllyCombatantId { get; private set; } = string.Empty;
        public string RightFocusCombatantId { get; private set; } = string.Empty;

        private void Awake()
        {
            ResolveCombatServices();
        }

        private void OnEnable()
        {
            if (_sessionHub == null)
            {
                return;
            }

            _sessionHub.OnCombatSessionReadyForUi += HandleCombatSessionReady;
            _sessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
            _sessionHub.OnCombatSkillExecutionPresentationStarted += HandleSkillPresentationStarted;
            _sessionHub.OnActionPresentationEnded += HandleActionPresentationEnded;
        }

        private void OnDisable()
        {
            if (_sessionHub == null)
            {
                return;
            }

            _sessionHub.OnCombatSessionReadyForUi -= HandleCombatSessionReady;
            _sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
            _sessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillPresentationStarted;
            _sessionHub.OnActionPresentationEnded -= HandleActionPresentationEnded;
        }

        private void LateUpdate()
        {
            if (_combatSession == null || !_combatSession.IsBattleOngoing)
            {
                LeftAllyCombatantId = string.Empty;
                RightFocusCombatantId = string.Empty;
                return;
            }

            LeftAllyCombatantId = ResolveLeftAllyCombatantId();
            RightFocusCombatantId = ResolveRightFocusCombatantId();
        }

        private void ResolveCombatServices()
        {
            if (combatLogicCenter == null)
            {
                var sessionHubInScene = FindFirstObjectByType<CombatSessionHub>();
                if (sessionHubInScene != null)
                {
                    combatLogicCenter = sessionHubInScene.gameObject;
                }
            }

            if (combatLogicCenter != null)
            {
                _sessionHub = combatLogicCenter.GetComponent<CombatSessionHub>();
            }

            if (skillButtonBarUIManager == null)
            {
                skillButtonBarUIManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
            }
        }

        private void HandleCombatSessionReady(CombatPrototypeController controller)
        {
            _combatSession = controller;
        }

        private void HandleCombatSessionClosed()
        {
            _combatSession = null;
            _isActionPresentationActive = false;
            _presentationActorCombatantId = string.Empty;
            _presentationTargetCombatantId = string.Empty;
            LeftAllyCombatantId = string.Empty;
            RightFocusCombatantId = string.Empty;
        }

        private void HandleSkillPresentationStarted(string actorCombatantId, string targetCombatantId)
        {
            _isActionPresentationActive = true;
            _presentationActorCombatantId = actorCombatantId ?? string.Empty;
            _presentationTargetCombatantId = targetCombatantId ?? string.Empty;
        }

        private void HandleActionPresentationEnded()
        {
            _isActionPresentationActive = false;
            _presentationActorCombatantId = string.Empty;
            _presentationTargetCombatantId = string.Empty;
        }

        private string ResolveLeftAllyCombatantId()
        {
            if (_isActionPresentationActive)
            {
                if (IsAllyCombatantId(_presentationActorCombatantId))
                {
                    return _presentationActorCombatantId;
                }

                if (IsAllyCombatantId(_presentationTargetCombatantId))
                {
                    return _presentationTargetCombatantId;
                }
            }

            var pendingPlayerCombatantId = _combatSession.PendingPlayerCombatantId;
            if (!string.IsNullOrEmpty(pendingPlayerCombatantId) &&
                IsLivingCombatant(pendingPlayerCombatantId))
            {
                return pendingPlayerCombatantId;
            }

            return FindFirstLivingAllyCombatantId();
        }

        private string ResolveRightFocusCombatantId()
        {
            if (_isActionPresentationActive)
            {
                if (IsAllyCombatantId(_presentationActorCombatantId))
                {
                    return _presentationTargetCombatantId;
                }

                if (IsEnemyCombatantId(_presentationActorCombatantId))
                {
                    return _presentationActorCombatantId;
                }
            }

            var playerTurnTargetId = TryResolvePlayerTurnTargetCombatantId();
            if (!string.IsNullOrEmpty(playerTurnTargetId))
            {
                return playerTurnTargetId;
            }

            var selectedEnemy = _combatSession.CurrentSelectedEnemy;
            if (selectedEnemy != null && !selectedEnemy.Health.IsDead)
            {
                return selectedEnemy.Identity.Id;
            }

            return FindFirstLivingEnemyCombatantId();
        }

        private string TryResolvePlayerTurnTargetCombatantId()
        {
            var pendingPlayerCombatantId = _combatSession.PendingPlayerCombatantId;
            if (string.IsNullOrEmpty(pendingPlayerCombatantId))
            {
                return null;
            }

            var actingAlly = _combatSession.FindCombatantById(pendingPlayerCombatantId);
            if (actingAlly == null || !_combatSession.IsPlayerCommandingCombatant(actingAlly))
            {
                return null;
            }

            _combatSession.GetSkillBarSelection(out var selectedSlot, out var skillBarOwnerCombatantId);
            if (selectedSlot.HasValue &&
                string.Equals(skillBarOwnerCombatantId, pendingPlayerCombatantId, StringComparison.Ordinal) &&
                TryResolveValidSkillTargetForSlot(actingAlly, selectedSlot.Value, out var skillTargetCombatantId))
            {
                return skillTargetCombatantId;
            }

            return null;
        }

        private bool TryResolveValidSkillTargetForSlot(
            Combatant actingAlly,
            int zeroBasedSlot,
            out string skillTargetCombatantId)
        {
            skillTargetCombatantId = null;

            if (skillButtonBarUIManager != null &&
                skillButtonBarUIManager.TryGetHoveredLivingCombatant(out var hoveredCombatant) &&
                PlayerActionBuilder.TryCreate(
                    _combatSession.BattleState,
                    _combatSession.BattleSimulator,
                    actingAlly,
                    zeroBasedSlot,
                    hoveredCombatant) != null)
            {
                skillTargetCombatantId = hoveredCombatant.Identity.Id;
                return true;
            }

            var battleState = _combatSession.BattleState;
            var skillIds = actingAlly.SkillLoadout.Skills
                .Where(skillId => battleState.SkillsById.ContainsKey(skillId))
                .Take(7)
                .ToList();
            if (zeroBasedSlot < 0 || zeroBasedSlot >= skillIds.Count)
            {
                return false;
            }

            var selectedSkill = battleState.SkillsById[skillIds[zeroBasedSlot]];
            switch (selectedSkill.TargetKind)
            {
                case SkillTargetKind.Self:
                    skillTargetCombatantId = actingAlly.Identity.Id;
                    return true;

                case SkillTargetKind.Ally:
                    var allyTarget = battleState.Allies.FirstOrDefault(allyCandidate =>
                        !allyCandidate.Health.IsDead &&
                        PlayerActionBuilder.TryCreate(
                            battleState,
                            _combatSession.BattleSimulator,
                            actingAlly,
                            zeroBasedSlot,
                            allyCandidate) != null);
                    if (allyTarget != null)
                    {
                        skillTargetCombatantId = allyTarget.Identity.Id;
                        return true;
                    }

                    return false;

                case SkillTargetKind.Enemy:
                default:
                    var selectedEnemy = _combatSession.CurrentSelectedEnemy;
                    if (selectedEnemy != null &&
                        !selectedEnemy.Health.IsDead &&
                        PlayerActionBuilder.TryCreate(
                            battleState,
                            _combatSession.BattleSimulator,
                            actingAlly,
                            zeroBasedSlot,
                            selectedEnemy) != null)
                    {
                        skillTargetCombatantId = selectedEnemy.Identity.Id;
                        return true;
                    }

                    var firstValidEnemy = battleState.Enemies.FirstOrDefault(enemyCandidate =>
                        !enemyCandidate.Health.IsDead &&
                        PlayerActionBuilder.TryCreate(
                            battleState,
                            _combatSession.BattleSimulator,
                            actingAlly,
                            zeroBasedSlot,
                            enemyCandidate) != null);
                    if (firstValidEnemy != null)
                    {
                        skillTargetCombatantId = firstValidEnemy.Identity.Id;
                        return true;
                    }

                    return false;
            }
        }

        private bool IsLivingCombatant(string combatantId)
        {
            var combatant = _combatSession.FindCombatantById(combatantId);
            return combatant != null && !combatant.Health.IsDead;
        }

        private bool IsAllyCombatantId(string combatantId) =>
            !string.IsNullOrEmpty(combatantId) &&
            combatantId.StartsWith("ally", StringComparison.OrdinalIgnoreCase);

        private bool IsEnemyCombatantId(string combatantId) =>
            !string.IsNullOrEmpty(combatantId) &&
            combatantId.StartsWith("enemy", StringComparison.OrdinalIgnoreCase);

        private string FindFirstLivingAllyCombatantId()
        {
            var battleState = _combatSession.BattleState;
            if (battleState == null)
            {
                return string.Empty;
            }

            return battleState.Allies
                .FirstOrDefault(ally => !ally.Health.IsDead)?
                .Identity.Id ?? string.Empty;
        }

        private string FindFirstLivingEnemyCombatantId()
        {
            var battleState = _combatSession.BattleState;
            if (battleState == null)
            {
                return string.Empty;
            }

            return battleState.Enemies
                .FirstOrDefault(enemy => !enemy.Health.IsDead)?
                .Identity.Id ?? string.Empty;
        }
    }
}
