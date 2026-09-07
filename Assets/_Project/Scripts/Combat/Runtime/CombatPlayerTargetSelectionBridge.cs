using System;
using System.Linq;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Handles pointer-based ally/enemy selection and casting the UI skill-bar selection on a target.
    /// </summary>
    public sealed class CombatPlayerTargetSelectionBridge
    {
        private readonly CombatSessionRuntime _session;
        private readonly CombatPointerRaycastService _pointerRaycast;
        private readonly CombatSessionHub _sessionHub;
        private readonly Func<Combatant, int> _findAllyIndex;
        private readonly Action<Combatant, int> _publishPlayerSkillHelpForAlly;
        private readonly Action<ChosenAction, Action> _presentPlayerChosenAction;

        public CombatPlayerTargetSelectionBridge(
            CombatSessionRuntime session,
            CombatPointerRaycastService pointerRaycast,
            CombatSessionHub sessionHub,
            Func<Combatant, int> findAllyIndex,
            Action<Combatant, int> publishPlayerSkillHelpForAlly,
            Action<ChosenAction, Action> presentPlayerChosenAction)
        {
            _session = session;
            _pointerRaycast = pointerRaycast;
            _sessionHub = sessionHub;
            _findAllyIndex = findAllyIndex;
            _publishPlayerSkillHelpForAlly = publishPlayerSkillHelpForAlly;
            _presentPlayerChosenAction = presentPlayerChosenAction;
        }

        public void TryDeselectSkillBarWithRightButton(bool rightClickPressedThisFrame)
        {
            if (!rightClickPressedThisFrame || !_session.HasSkillBarSelectionPendingUse())
            {
                return;
            }

            ClearSkillBarSelection();
        }

        public void PickTargetFromMouse(bool leftClickPressedThisFrame, Vector2 pointerScreenPosition, bool hasPointerScreenPosition)
        {
            if (!leftClickPressedThisFrame || !hasPointerScreenPosition)
            {
                return;
            }

            if (!_pointerRaycast.TryRaycastCombatCapsuleTag(
                    pointerScreenPosition,
                    out var capsuleTag,
                    out _))
            {
                return;
            }

            var hitAlly = _session.State.Allies.FirstOrDefault(ally =>
                ally.Identity.Id == capsuleTag.combatantId && !ally.Health.IsDead);
            if (hitAlly != null)
            {
                HandleAllyClick(hitAlly);
                return;
            }

            var hitEnemy = _session.State.Enemies.FirstOrDefault(enemy =>
                enemy.Identity.Id == capsuleTag.combatantId && !enemy.Health.IsDead);
            if (hitEnemy == null)
            {
                return;
            }

            HandleEnemyClick(hitEnemy);
        }

        public void ClearSkillBarSelection()
        {
            if (!_session.SkillBarSelectedSlot.HasValue && string.IsNullOrEmpty(_session.SkillBarSelectedOwnerId))
            {
                return;
            }

            _session.SkillBarSelectedSlot = null;
            _session.SkillBarSelectedOwnerId = null;
            _sessionHub?.RaiseSkillBarSelectionClearedBySession();
        }

        private void HandleAllyClick(Combatant hitAlly)
        {
            if (_session.HasSkillBarSelectionPendingUse() && TryCastUiSelectedSkillOnTarget(hitAlly))
            {
                return;
            }

            if (_session.HasSkillBarSelectionPendingUse())
            {
                Debug.LogWarning("Skill (UI) inválida para este aliado.");
                _publishPlayerSkillHelpForAlly(_session.PendingPlayerActor, _findAllyIndex(_session.PendingPlayerActor));
                return;
            }

            var allyIndex = 0;
            for (var allySearchIndex = 0; allySearchIndex < _session.State.Allies.Count; allySearchIndex++)
            {
                if (ReferenceEquals(_session.State.Allies[allySearchIndex], hitAlly))
                {
                    allyIndex = allySearchIndex;
                    break;
                }
            }

            CombatSkillBarDebug.LogHotbar(hitAlly, allyIndex, _session.State);
            _publishPlayerSkillHelpForAlly(hitAlly, allyIndex);
        }

        private void HandleEnemyClick(Combatant hitEnemy)
        {
            if (_session.HasSkillBarSelectionPendingUse() && TryCastUiSelectedSkillOnTarget(hitEnemy))
            {
                return;
            }

            if (_session.HasSkillBarSelectionPendingUse())
            {
                Debug.LogWarning("Skill (UI) inválida para este inimigo.");
                _publishPlayerSkillHelpForAlly(_session.PendingPlayerActor, _findAllyIndex(_session.PendingPlayerActor));
                return;
            }

            _session.SelectedEnemyTarget = hitEnemy;
            Debug.Log(
                $"Alvo: {_session.SelectedEnemyTarget.Identity.Id} " +
                $"(HP {_session.SelectedEnemyTarget.Health.CurrentHp}/{_session.SelectedEnemyTarget.Health.MaxHp})");
            if (_session.NeedsPlayerInput && _session.PendingPlayerActor != null)
            {
                _publishPlayerSkillHelpForAlly(
                    _session.PendingPlayerActor,
                    _findAllyIndex(_session.PendingPlayerActor));
            }
        }

        private bool TryCastUiSelectedSkillOnTarget(Combatant target)
        {
            if (!_session.NeedsPlayerInput || _session.PendingPlayerActor == null || _session.PresentationBusy)
            {
                return false;
            }

            if (!_session.HasSkillBarSelectionPendingUse())
            {
                return false;
            }

            if (!string.Equals(
                    _session.SkillBarSelectedOwnerId,
                    _session.PendingPlayerActor.Identity.Id,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var action = PlayerActionBuilder.TryCreate(
                _session.State,
                _session.Simulator,
                _session.PendingPlayerActor,
                _session.SkillBarSelectedSlot.Value,
                target);
            if (action == null)
            {
                return false;
            }

            if (_session.State.Enemies.Any(enemy => enemy.Identity.Id == target.Identity.Id && !enemy.Health.IsDead))
            {
                _session.SelectedEnemyTarget = target;
            }
            else
            {
                _session.SelectedEnemyTarget = null;
            }

            _session.NeedsPlayerInput = false;
            _session.PendingPlayerActor = null;
            _session.PresentationBusy = true;
            ClearSkillBarSelection();
            _presentPlayerChosenAction(
                action,
                () =>
                {
                    _session.ActorIndex++;
                    _session.PreparedThisStep = false;
                });
            return true;
        }
    }
}
