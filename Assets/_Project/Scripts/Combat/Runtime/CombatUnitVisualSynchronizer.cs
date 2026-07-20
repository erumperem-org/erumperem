using System;
using System.Collections.Generic;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Keeps unit visual roots aligned with combatant health/death; caches
    /// <see cref="EnemyAnimationController"/> per combatant at bind time (AUDITORIA hot-path fix).
    /// </summary>
    public sealed class CombatUnitVisualSynchronizer
    {
        private readonly Dictionary<string, EnemyAnimationController> _animationControllerByCombatantId =
            new(StringComparer.Ordinal);

        private readonly Dictionary<string, bool> _hasEnemyAnimationControllerByCombatantId =
            new(StringComparer.Ordinal);

        public void Clear()
        {
            _animationControllerByCombatantId.Clear();
            _hasEnemyAnimationControllerByCombatantId.Clear();
        }

        public void RegisterUnitVisual(string combatantId, Transform unitRoot)
        {
            if (string.IsNullOrEmpty(combatantId) || unitRoot == null)
            {
                return;
            }

            var enemyAnimationController = unitRoot.GetComponent<EnemyAnimationController>() ??
                                           unitRoot.GetComponentInChildren<EnemyAnimationController>(true);
            if (enemyAnimationController != null)
            {
                _animationControllerByCombatantId[combatantId] = enemyAnimationController;
                _hasEnemyAnimationControllerByCombatantId[combatantId] = true;
            }
            else
            {
                _animationControllerByCombatantId.Remove(combatantId);
                _hasEnemyAnimationControllerByCombatantId[combatantId] = false;
            }
        }

        public bool TryGetAnimationController(
            string combatantId,
            out EnemyAnimationController enemyAnimationController)
        {
            if (string.IsNullOrEmpty(combatantId))
            {
                enemyAnimationController = null;
                return false;
            }

            return _animationControllerByCombatantId.TryGetValue(combatantId, out enemyAnimationController) &&
                   enemyAnimationController != null;
        }

        public void SyncUnitVisuals(
            IReadOnlyDictionary<string, Transform> unitVisualRootsByCombatantId,
            Func<string, Combatant> findCombatantById,
            float enemyDeathClipMarginSeconds,
            bool syncHpAsVerticalScale,
            ISet<string> damageFeedbackBusyCombatantIds)
        {
            foreach (var combatantIdAndUnitRoot in unitVisualRootsByCombatantId)
            {
                var combatantId = combatantIdAndUnitRoot.Key;
                var unitRoot = combatantIdAndUnitRoot.Value;
                if (unitRoot == null)
                {
                    continue;
                }

                var combatant = findCombatantById(combatantId);
                if (combatant == null)
                {
                    continue;
                }

                if (combatant.Health.IsDead)
                {
                    if (TryGetAnimationController(combatantId, out var enemyAnimationController))
                    {
                        enemyAnimationController.EnsureDeathVisualSequenceStarted(enemyDeathClipMarginSeconds);
                        if (!enemyAnimationController.IsDeathVisualSequenceFinished)
                        {
                            continue;
                        }
                    }

                    unitRoot.gameObject.SetActive(false);
                }
                else
                {
                    unitRoot.gameObject.SetActive(true);
                    var skipHpVerticalScale = _hasEnemyAnimationControllerByCombatantId.TryGetValue(
                                                  combatantId,
                                                  out var hasEnemyAnimationController) &&
                                              hasEnemyAnimationController;
                    if (syncHpAsVerticalScale &&
                        !skipHpVerticalScale &&
                        damageFeedbackBusyCombatantIds != null &&
                        !damageFeedbackBusyCombatantIds.Contains(combatantId))
                    {
                        unitRoot.localScale = new Vector3(
                            1f,
                            Mathf.Max(0.3f, combatant.Health.CurrentHp / (float)combatant.Health.MaxHp),
                            1f);
                    }
                }
            }
        }
    }
}
