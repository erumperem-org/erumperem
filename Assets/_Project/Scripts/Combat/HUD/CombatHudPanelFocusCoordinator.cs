using System;
using System.Linq;
using Erumperem.Combat.Runtime;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// Coordena quais combatentes aparecem nos painéis da HUD:
    /// - Rodada do Player:
    ///     Esquerda = Jogador agindo.
    ///     Direita = Alvo (inimigo focado, ou aliado/self se a skill mirar em suporte/cura).
    /// - Rodada do Inimigo:
    ///     Direita = Inimigo agindo (atualiza para cada inimigo 1, 2, 3 e 4).
    ///     Esquerda = Alvo atacado (personagem player ou outro inimigo se for buff/cura).
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class CombatHudPanelFocusCoordinator : MonoBehaviour
    {
        [SerializeField] private GameObject combatLogicCenter;
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        private CombatSessionHub _sessionHub;
        private readonly CombatSessionHubSubscription _sessionHubSubscription = new();
        private CombatPrototypeController _combatSession;

        // Armazena a ação atual que está em apresentação ou foi recentemente despachada
        private string _activePresentationActorId = string.Empty;
        private string _activePresentationTargetId = string.Empty;
        private bool _isActionPresentationActive;

        public string LeftAllyCombatantId { get; private set; } = string.Empty;
        public string RightFocusCombatantId { get; private set; } = string.Empty;

        /// <summary>
        /// Disparado quando os IDs em foco mudam, permitindo que os painéis atualizem imediatamente.
        /// </summary>
        public event Action OnFocusChanged;

        private void Awake()
        {
            ResolveCombatServices();
        }

        private void OnEnable()
        {
            ResolveCombatServices();
            _sessionHubSubscription.Subscribe(_sessionHub, HandleCombatSessionReady, HandleCombatSessionClosed);
            SubscribeToSessionHubEvents();
            _sessionHubSubscription.TryCatchUpWithActiveCombatSession(_combatSession);
        }

        private void OnDisable()
        {
            _sessionHubSubscription.Unsubscribe();
            UnsubscribeFromSessionHubEvents();
        }

        private void SubscribeToSessionHubEvents()
        {
            if (_sessionHub == null) return;

            _sessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillExecutionStarted;
            _sessionHub.OnCombatSkillExecutionPresentationStarted += HandleSkillExecutionStarted;

            _sessionHub.OnActionPresentationEnded -= HandleActionPresentationEnded;
            _sessionHub.OnActionPresentationEnded += HandleActionPresentationEnded;

            _sessionHub.OnTurnStarted -= HandleTurnOrRoundAdvanced;
            _sessionHub.OnTurnStarted += HandleTurnOrRoundAdvanced;
        }

        private void UnsubscribeFromSessionHubEvents()
        {
            if (_sessionHub == null) return;

            _sessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillExecutionStarted;
            _sessionHub.OnActionPresentationEnded -= HandleActionPresentationEnded;
            _sessionHub.OnTurnStarted -= HandleTurnOrRoundAdvanced;
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
            RefreshFocusNow();
        }

        private void HandleCombatSessionClosed()
        {
            _combatSession = null;
            _isActionPresentationActive = false;
            _activePresentationActorId = string.Empty;
            _activePresentationTargetId = string.Empty;
            LeftAllyCombatantId = string.Empty;
            RightFocusCombatantId = string.Empty;
            OnFocusChanged?.Invoke();
        }

        private void HandleSkillExecutionStarted(string actorCombatantId, string targetCombatantId)
        {
            _isActionPresentationActive = true;
            _activePresentationActorId = actorCombatantId ?? string.Empty;
            _activePresentationTargetId = targetCombatantId ?? string.Empty;
            RefreshFocusNow();
        }

        private void HandleActionPresentationEnded()
        {
            _isActionPresentationActive = false;
            RefreshFocusNow();
        }

        private void HandleTurnOrRoundAdvanced()
        {
            RefreshFocusNow();
        }

        private void LateUpdate()
        {
            if (_combatSession == null || !_combatSession.IsBattleOngoing)
            {
                return;
            }

            ResolvePanelCombatants(out var newLeftId, out var newRightId);

            if (!string.Equals(LeftAllyCombatantId, newLeftId, StringComparison.Ordinal) ||
                !string.Equals(RightFocusCombatantId, newRightId, StringComparison.Ordinal))
            {
                LeftAllyCombatantId = newLeftId;
                RightFocusCombatantId = newRightId;
                OnFocusChanged?.Invoke();
            }
        }

        public void RefreshFocusNow()
        {
            if (_combatSession == null || !_combatSession.IsBattleOngoing)
            {
                LeftAllyCombatantId = string.Empty;
                RightFocusCombatantId = string.Empty;
                OnFocusChanged?.Invoke();
                return;
            }

            ResolvePanelCombatants(out var newLeftId, out var newRightId);
            LeftAllyCombatantId = newLeftId;
            RightFocusCombatantId = newRightId;
            OnFocusChanged?.Invoke();
        }

        private void ResolvePanelCombatants(out string leftId, out string rightId)
        {
            // 1. Prioridade: Se estiver em apresentação de ação (seja Player ou Inimigo)
            if (_isActionPresentationActive && !string.IsNullOrEmpty(_activePresentationActorId))
            {
                var actor = _combatSession.FindCombatantById(_activePresentationActorId);
                var isEnemyAction = actor != null && actor.Position.Side == Side.Enemies;

                if (isEnemyAction)
                {
                    // Na rodada do inimigo:
                    // Direita = Inimigo agindo (Inimigo 1, 2, 3 ou 4)
                    // Esquerda = Alvo (Herói ou outro inimigo)
                    rightId = _activePresentationActorId;
                    leftId = !string.IsNullOrEmpty(_activePresentationTargetId)
                        ? _activePresentationTargetId
                        : FindFirstLivingAllyCombatantId();
                    return;
                }
                else
                {
                    // Na rodada do player atacando:
                    // Esquerda = Herói agindo
                    // Direita = Alvo (inimigo ou aliado)
                    leftId = _activePresentationActorId;
                    rightId = !string.IsNullOrEmpty(_activePresentationTargetId)
                        ? _activePresentationTargetId
                        : _activePresentationActorId;
                    return;
                }
            }

            // 2. Consulta quem é o combatente atual do combate
            var pendingPlayerId = _combatSession.PendingPlayerCombatantId;

            // Turno do Jogador (esperando input):
            if (!string.IsNullOrEmpty(pendingPlayerId) && IsLivingCombatant(pendingPlayerId))
            {
                leftId = pendingPlayerId;
                rightId = ResolvePlayerTurnTargetFocus(pendingPlayerId);
                return;
            }

            // Turno do Inimigo (calculando ou aguardando próximo inimigo da rodada):
            // Descobre o inimigo que acabou de agir ou que é o próximo da fila
            var currentEnemyActorId = !string.IsNullOrEmpty(_activePresentationActorId) && IsEnemyCombatantId(_activePresentationActorId)
                ? _activePresentationActorId
                : ResolveCurrentRoundEnemyActorId();

            rightId = currentEnemyActorId;
            leftId = !string.IsNullOrEmpty(_activePresentationTargetId) && IsLivingCombatant(_activePresentationTargetId)
                ? _activePresentationTargetId
                : ResolveEnemyTargetFocus();
        }

        private string ResolvePlayerTurnTargetFocus(string actingAllyId)
        {
            var actingAlly = _combatSession.FindCombatantById(actingAllyId);

            // A) Hover com o mouse sobre qualquer unidade viva válida (inimigo OU aliado)
            if (skillButtonBarUIManager != null &&
                skillButtonBarUIManager.TryGetHoveredLivingCombatant(out var hovered))
            {
                return hovered.Identity.Id;
            }

            // B) Alvo pela skill selecionada na hotbar
            _combatSession.GetSkillBarSelection(out var selectedSlot, out var ownerId);
            if (selectedSlot.HasValue &&
                string.Equals(ownerId, actingAllyId, StringComparison.Ordinal) &&
                actingAlly != null)
            {
                var battleState = _combatSession.BattleState;
                var skillIds = actingAlly.SkillLoadout.Skills
                    .Where(id => battleState.SkillsById.ContainsKey(id))
                    .Take(7)
                    .ToList();

                if (selectedSlot.Value >= 0 && selectedSlot.Value < skillIds.Count)
                {
                    var selectedSkill = battleState.SkillsById[skillIds[selectedSlot.Value]];

                    if (SkillTargetKindRules.IsSelfOnly(selectedSkill.TargetKind))
                    {
                        return actingAllyId;
                    }

                    if (SkillTargetKindRules.DirectsPrimaryDamageAtAllies(selectedSkill.TargetKind))
                    {
                        var preferredAlly = SkillTargetResolver.ResolvePreferredSelection(
                            battleState,
                            actingAlly,
                            selectedSkill,
                            actingAlly);

                        if (preferredAlly != null && !preferredAlly.Health.IsDead)
                        {
                            return preferredAlly.Identity.Id;
                        }

                        return actingAllyId;
                    }
                }
            }

            // C) Inimigo selecionado anteriormente por clique
            var selectedEnemy = _combatSession.CurrentSelectedEnemy;
            if (selectedEnemy != null && !selectedEnemy.Health.IsDead)
            {
                return selectedEnemy.Identity.Id;
            }

            // D) Fallback
            var firstEnemy = FindFirstLivingEnemyCombatantId();
            return !string.IsNullOrEmpty(firstEnemy) ? firstEnemy : actingAllyId;
        }

        private string ResolveEnemyTargetFocus()
        {
            var battleState = _combatSession?.BattleState;
            if (battleState == null) return FindFirstLivingAllyCombatantId();

            // Aliado com Taunt tem prioridade de foco
            var tauntAlly = battleState.Allies.FirstOrDefault(a => !a.Health.IsDead && a.Tokens.GetStacks(TokenType.Taunt) > 0);
            if (tauntAlly != null)
            {
                return tauntAlly.Identity.Id;
            }

            return FindFirstLivingAllyCombatantId();
        }

        private string ResolveCurrentRoundEnemyActorId()
        {
            var battleState = _combatSession?.BattleState;
            if (battleState == null) return string.Empty;

            return FindFirstLivingEnemyCombatantId();
        }

        private bool IsLivingCombatant(string combatantId)
        {
            var combatant = _combatSession?.FindCombatantById(combatantId);
            return combatant != null && !combatant.Health.IsDead;
        }

        private static bool IsEnemyCombatantId(string combatantId) =>
            !string.IsNullOrEmpty(combatantId) &&
            combatantId.StartsWith("enemy", StringComparison.OrdinalIgnoreCase);

        private string FindFirstLivingAllyCombatantId()
        {
            var battleState = _combatSession?.BattleState;
            if (battleState == null) return string.Empty;

            return battleState.Allies
                .FirstOrDefault(ally => !ally.Health.IsDead)?
                .Identity.Id ?? string.Empty;
        }

        private string FindFirstLivingEnemyCombatantId()
        {
            var battleState = _combatSession?.BattleState;
            if (battleState == null) return string.Empty;

            return battleState.Enemies
                .FirstOrDefault(enemy => !enemy.Health.IsDead)?
                .Identity.Id ?? string.Empty;
        }
    }
}