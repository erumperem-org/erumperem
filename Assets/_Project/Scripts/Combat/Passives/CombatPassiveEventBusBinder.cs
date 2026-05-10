using System;
using System.Collections.Generic;
using Game.Core.Models;
using Game.Core.Passives;
using UnityEngine;
using Erumperem.Progression;

namespace Erumperem.Combat.Passives
{
    /// <summary>
    /// Subscreve só a nós <see cref="SkillTreeNodeAsset"/> marcados como passivos; dados e UnityEvents vivem nesse SO.
    /// </summary>
    public sealed class CombatPassiveEventBusBinder : MonoBehaviour
    {
        [SerializeField] private CombatSessionHub _sessionHub;
        [SerializeField] private SkillTreeNodeAsset[] _passiveNodeAssets = Array.Empty<SkillTreeNodeAsset>();

        private CombatPrototypeController _boundSession;

        private readonly List<Action<PassiveTrigger, BattleState, CombatPassiveEventContext>> _subscribedListeners =
            new();

        private void OnEnable()
        {
            if (_sessionHub != null)
            {
                _sessionHub.OnCombatSessionReadyForUi += HandleCombatSessionReadyForUi;
            }
        }

        private void OnDisable()
        {
            if (_sessionHub != null)
            {
                _sessionHub.OnCombatSessionReadyForUi -= HandleCombatSessionReadyForUi;
            }

            UnbindCurrentSession();
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController controller)
        {
            UnbindCurrentSession();
            _boundSession = controller;
            var passiveBus = controller.BattleState.PassiveBus;

            foreach (var nodeAsset in _passiveNodeAssets)
            {
                if (nodeAsset == null || !nodeAsset.IsPassiveNode || string.IsNullOrWhiteSpace(nodeAsset.NodeId))
                {
                    continue;
                }

                var nodeId = nodeAsset.NodeId;
                Action<PassiveTrigger, BattleState, CombatPassiveEventContext> listener = (trigger, state, context) =>
                {
                    if (!nodeAsset.ShouldFirePassiveTrigger(trigger))
                    {
                        return;
                    }

                    if (!IsPassiveRelevantToTrigger(nodeId, trigger, state, context))
                    {
                        return;
                    }

                    nodeAsset.InvokePassiveDispatch();
                };

                _subscribedListeners.Add(listener);
                passiveBus.Subscribe(listener);
            }
        }

        private void UnbindCurrentSession()
        {
            if (_boundSession == null)
            {
                return;
            }

            var passiveBus = _boundSession.BattleState.PassiveBus;
            foreach (var listener in _subscribedListeners)
            {
                passiveBus.Unsubscribe(listener);
            }

            _subscribedListeners.Clear();
            _boundSession = null;
        }

        private static bool IsPassiveRelevantToTrigger(
            string passiveNodeId,
            PassiveTrigger trigger,
            BattleState state,
            CombatPassiveEventContext context)
        {
            if (!state.PassivesById.ContainsKey(passiveNodeId))
            {
                return false;
            }

            return trigger switch
            {
                PassiveTrigger.CombatantSlain =>
                    HasUnlockedPassive(context.Killer, passiveNodeId) ||
                    HasUnlockedPassive(context.Victim, passiveNodeId),
                PassiveTrigger.ComboConsumed =>
                    HasUnlockedPassive(context.Self, passiveNodeId) ||
                    HasUnlockedPassive(context.Other, passiveNodeId),
                _ => HasUnlockedPassive(context.Self, passiveNodeId),
            };
        }

        private static bool HasUnlockedPassive(Combatant? combatant, string passiveNodeId)
        {
            if (combatant == null)
            {
                return false;
            }

            return combatant.Progression.UnlockedNodes.TryGetValue(passiveNodeId, out var unlocked) && unlocked;
        }
    }
}
