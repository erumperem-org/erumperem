using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Resolves a chosen action in the simulator and publishes presentation (camera, narrative, VFX, audio).
    /// </summary>
    public sealed class CombatActionPresentationOrchestrator
    {
        private const string ActionRockTweenId = "CombatActionRock";
        private const string CorruptionPulseTweenId = "CombatCorruptionPulse";

        private readonly MonoBehaviour _coroutineHost;
        private readonly CombatSessionRuntime _session;
        private readonly CombatSessionHub _sessionHub;
        private readonly CombatUnitVisualSynchronizer _unitVisualSynchronizer;
        private readonly CombatActionPresentationSettings _settings;
        private readonly bool _logEventsToConsole;

        private Transform _actionRockTransform;
        private Vector3 _actionRockBaseLocalPosition;

        public CombatActionPresentationOrchestrator(
            MonoBehaviour coroutineHost,
            CombatSessionRuntime session,
            CombatSessionHub sessionHub,
            CombatUnitVisualSynchronizer unitVisualSynchronizer,
            CombatActionPresentationSettings settings,
            bool logEventsToConsole)
        {
            _coroutineHost = coroutineHost;
            _session = session;
            _sessionHub = sessionHub;
            _unitVisualSynchronizer = unitVisualSynchronizer;
            _settings = settings;
            _logEventsToConsole = logEventsToConsole;
        }

        public void PresentChosenAction(ChosenAction action, Action onStepComplete) =>
            _coroutineHost.StartCoroutine(PresentActionRoutine(action, onStepComplete));

        public void StopActorActionRock()
        {
            DOTween.Kill(ActionRockTweenId, false);
            RestoreActorActionRockLocal();
        }

        public void PlayDamageVisualFeedback(string targetCombatantId)
        {
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX("Damage");
            }

            if (!_session.UnitVisualRootsByCombatantId.TryGetValue(targetCombatantId, out var unitRoot) ||
                unitRoot == null)
            {
                return;
            }

            var combatant = _session.FindCombatantById(targetCombatantId);
            if (combatant == null || combatant.Health.IsDead)
            {
                return;
            }

            _session.DamageFeedbackBusy.Add(targetCombatantId);
            unitRoot.DOKill(false);
            var sequence = DOTween.Sequence();
            sequence.SetTarget(unitRoot);
            sequence.Append(
                unitRoot.DOPunchScale(
                    _settings.DamagePunchScale,
                    _settings.DamagePunchDuration,
                    _settings.DamagePunchVibrato,
                    _settings.DamagePunchElasticity));
            if (_settings.SyncHpAsVerticalScale)
            {
                var targetY = Mathf.Max(0.3f, combatant.Health.CurrentHp / (float)combatant.Health.MaxHp);
                sequence.Append(unitRoot.DOScaleY(targetY, _settings.DamageShrinkDuration).SetEase(Ease.OutCubic));
            }

            // Aplica a escala de velocidade do combate à sequência de feedback de dano
            sequence.timeScale = CombatSpeedSettings.SpeedMultiplier;
            sequence.OnComplete(() => _session.DamageFeedbackBusy.Remove(targetCombatantId));
        }

        private IEnumerator PresentActionRoutine(ChosenAction action, Action onStepComplete)
        {
            Animator actorAnimator = null;
            Animator targetAnimator = null;
            float speedMultiplier = CombatSpeedSettings.SpeedMultiplier;

            try
            {
                StopActorActionRock();
                _sessionHub?.RaiseCinemachineFocusEnded();
                GetTimingForSkill(action.Skill.Id, out var playSeconds, out var postPauseSeconds);
                EnemyAnimationController enemyActorVisual = null;
                if (action.Actor.Identity.Faction == Faction.Enemy &&
                    _unitVisualSynchronizer.TryGetAnimationController(
                        action.Actor.Identity.Id,
                        out enemyActorVisual))
                {
                    var attackHoldSeconds = enemyActorVisual.ComputeAttackPresentationDurationSeconds(
                        _settings.EnemyAttackClipMarginSeconds);
                    playSeconds = Mathf.Max(playSeconds, attackHoldSeconds);
                }

                // Ajusta as durações de espera de acordo com a velocidade de combate
                playSeconds /= speedMultiplier;
                postPauseSeconds /= speedMultiplier;

                // Captura e acelera dinamicamente os Animators do atacante e do defensor
                if (_session.UnitVisualRootsByCombatantId.TryGetValue(action.Actor.Identity.Id, out var actorRoot) && actorRoot != null)
                {
                    actorAnimator = actorRoot.GetComponent<Animator>() ?? actorRoot.GetComponentInChildren<Animator>();
                    if (actorAnimator != null)
                    {
                        actorAnimator.speed = speedMultiplier;
                    }
                }

                if (_session.UnitVisualRootsByCombatantId.TryGetValue(action.Target.Identity.Id, out var targetRoot) && targetRoot != null)
                {
                    targetAnimator = targetRoot.GetComponent<Animator>() ?? targetRoot.GetComponentInChildren<Animator>();
                    if (targetAnimator != null)
                    {
                        targetAnimator.speed = speedMultiplier;
                    }
                }

                _sessionHub?.RaiseActionPresentationStarted();
                _session.OngoingPresentationActorCombatantId = action.Actor.Identity.Id;
                _session.OngoingPresentationTargetCombatantId = action.Target.Identity.Id;
                _sessionHub?.RaiseCombatSkillExecutionPresentationStarted(
                    _session.OngoingPresentationActorCombatantId,
                    _session.OngoingPresentationTargetCombatantId);
                enemyActorVisual?.NotifyAttackPresentationBegin(playSeconds);
                var rockDuration = Mathf.Max(0f, playSeconds + postPauseSeconds);

                var startEventIndex = _session.EventCollector.Events.Count;
                _session.Simulator.ResolveChosenAction(_session.State, action);
                var endEventIndex = _session.EventCollector.Events.Count;
                var eventCount = endEventIndex - startEventIndex;
                if (eventCount > 0)
                {
                    var eventSlice = _session.EventCollector.Events.GetRange(startEventIndex, eventCount);
                    var narrativeLines = CombatNarrativeFormatter.BuildLines(_session.State, action, eventSlice).ToList();
                    if (narrativeLines.Count > 0)
                    {
                        _sessionHub?.RaiseNarrativeLines(narrativeLines);
                    }

                    foreach (var combatEvent in eventSlice)
                    {
                        if (combatEvent.EventType == BattleEventType.CorruptionAdjusted)
                        {
                            PublishCorruptionPresentation(combatEvent);
                        }

                        if (combatEvent.EventType == BattleEventType.CombatantDied &&
                            !string.IsNullOrEmpty(combatEvent.TargetId))
                        {
                            _sessionHub?.RaiseCombatantPresentationDeath(combatEvent.TargetId);
                            if (_unitVisualSynchronizer.TryGetAnimationController(
                                    combatEvent.TargetId,
                                    out var deadEnemyVisual))
                            {
                                deadEnemyVisual.EnsureDeathVisualSequenceStarted(
                                    _settings.EnemyDeathClipMarginSeconds);
                            }
                        }

                        if (combatEvent.EventType == BattleEventType.DamageApplied && combatEvent.DamageAmount > 0)
                        {
                            PlayDamageVisualFeedback(combatEvent.TargetId);

                            if (_unitVisualSynchronizer.TryGetAnimationController(
                                    combatEvent.TargetId,
                                    out var hitEnemyAnimationController))
                            {
                                hitEnemyAnimationController.NotifyHitTakenPresentationBegin(
                                    hitEnemyAnimationController.ComputeHitTakenPresentationDurationSeconds(0f));
                            }
                        }
                    }

                    LogLastCombatEvent();
                }

                var actorAfter = _session.FindCombatantById(action.Actor.Identity.Id);
                if (actorAfter != null &&
                    !actorAfter.Health.IsDead &&
                    _session.UnitVisualRootsByCombatantId.TryGetValue(action.Actor.Identity.Id, out var actorVisualRoot))
                {
                    _session.UnitVisualRootsByCombatantId.TryGetValue(
                        action.Target.Identity.Id,
                        out var targetVisualRoot);
                    _sessionHub?.RaiseCinemachineFocusBegan(actorVisualRoot, targetVisualRoot);
                }

                if (actorAfter != null && !actorAfter.Health.IsDead && rockDuration > 0.02f)
                {
                    BeginActorActionRock(action, rockDuration);
                }

                if (playSeconds > 0f)
                {
                    yield return new WaitForSeconds(playSeconds);
                }

                if (_session.BattleEnded)
                {
                    yield break;
                }

                if (postPauseSeconds > 0f)
                {
                    yield return new WaitForSeconds(postPauseSeconds);
                }
            }
            finally
            {
                // Restaura a velocidade padrão dos animators ao encerrar o turno
                if (actorAnimator != null)
                {
                    actorAnimator.speed = 1.0f;
                }
                if (targetAnimator != null)
                {
                    targetAnimator.speed = 1.0f;
                }

                _sessionHub?.RaiseCinemachineFocusEnded();
                StopActorActionRock();
                _session.OngoingPresentationActorCombatantId = string.Empty;
                _session.OngoingPresentationTargetCombatantId = string.Empty;
                _session.PresentationBusy = false;
                onStepComplete?.Invoke();
                _sessionHub?.RaiseTurnEnded();
                _coroutineHost.StartCoroutine(NotifyPresentationEndedDeferred());
            }
        }

        private IEnumerator NotifyPresentationEndedDeferred()
        {
            yield return null;
            _sessionHub?.RaiseActionPresentationEnded();
        }

        private void BeginActorActionRock(ChosenAction action, float totalDurationSeconds)
        {
            StopActorActionRock();
            if (totalDurationSeconds <= 0.02f)
            {
                return;
            }

            if (!_session.UnitVisualRootsByCombatantId.TryGetValue(action.Actor.Identity.Id, out var actorRoot) ||
                actorRoot == null)
            {
                return;
            }

            _actionRockTransform = actorRoot;
            _actionRockBaseLocalPosition = actorRoot.localPosition;
            var punch = actorRoot.DOPunchPosition(
                    _settings.ActorActionRockPunch,
                    totalDurationSeconds,
                    _settings.ActorActionRockVibrato,
                    _settings.ActorActionRockElasticity)
                .SetRelative(true)
                .SetId(ActionRockTweenId)
                .SetTarget(actorRoot)
                .OnKill(RestoreActorActionRockLocal)
                .OnComplete(RestoreActorActionRockLocal);

            // Aplica escala de velocidade do combate ao Tween de rocking
            punch.timeScale = CombatSpeedSettings.SpeedMultiplier;
        }

        private void RestoreActorActionRockLocal()
        {
            if (_actionRockTransform == null)
            {
                return;
            }

            _actionRockTransform.localPosition = _actionRockBaseLocalPosition;
            _actionRockTransform = null;
        }

        private void PublishCorruptionPresentation(CombatEvent combatEvent)
        {
            if (combatEvent.CorruptionDelta > 1e-9)
            {
                PlayCorruptionIncreaseFeedback();
                _sessionHub?.RaiseBattleCorruptionIncreasePulse(combatEvent.CorruptionDelta);
            }

            _sessionHub?.RaiseBattleCorruptionAdjusted(
                combatEvent.CorruptionDelta,
                combatEvent.CorruptionValue,
                combatEvent.PreviousCorruptionTier,
                combatEvent.CorruptionTier);

            if (combatEvent.PreviousCorruptionTier.HasValue &&
                combatEvent.PreviousCorruptionTier.Value != combatEvent.CorruptionTier)
            {
                _sessionHub?.RaiseBattleCorruptionTierReached(
                    combatEvent.PreviousCorruptionTier.Value,
                    combatEvent.CorruptionTier);
            }

            CorruptionManager.Instance?.NotifyCombatCorruptionAdjusted(combatEvent);
        }

        private void PlayCorruptionIncreaseFeedback()
        {
            if (_settings.CorruptionIncreaseFeedbackRoot == null)
            {
                return;
            }

            DOTween.Kill(CorruptionPulseTweenId, false);
            var punch = _settings.CorruptionIncreaseFeedbackRoot.DOPunchScale(
                    _settings.CorruptionPulseScale,
                    _settings.CorruptionPulseDuration,
                    _settings.CorruptionPulseVibrato,
                    _settings.CorruptionPulseElasticity)
                .SetId(CorruptionPulseTweenId)
                .SetLink(_settings.CorruptionIncreaseFeedbackRoot.gameObject);

            // Aplica escala de velocidade do combate ao Tween de corrupção
            punch.timeScale = CombatSpeedSettings.SpeedMultiplier;
        }

        private void GetTimingForSkill(string skillId, out float playSeconds, out float postPauseSeconds)
        {
            playSeconds = _settings.DefaultPlaySeconds;
            postPauseSeconds = _settings.DefaultPostPauseSeconds;
            if (_settings.SkillTimings == null)
            {
                return;
            }

            foreach (var timingEntry in _settings.SkillTimings)
            {
                if (timingEntry == null || string.IsNullOrEmpty(timingEntry.skillId))
                {
                    continue;
                }

                if (!string.Equals(timingEntry.skillId, skillId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                playSeconds = Mathf.Max(0f, timingEntry.playSeconds);
                postPauseSeconds = Mathf.Max(0f, timingEntry.postPauseSeconds);
                return;
            }
        }

        private void LogLastCombatEvent()
        {
            if (!_logEventsToConsole || _session.EventCollector.Events.Count == 0)
            {
                return;
            }

            var lastEvent = _session.EventCollector.Events[^1];
            Debug.Log(
                $"[Combat] {lastEvent.EventType} turn={lastEvent.Turn} actor={lastEvent.ActorId} " +
                $"target={lastEvent.TargetId} skill={lastEvent.SkillId} dmg={lastEvent.DamageAmount}");
        }
    }

    public sealed class CombatActionPresentationSettings
    {
        public float DefaultPlaySeconds { get; set; }
        public float DefaultPostPauseSeconds { get; set; }
        public CombatSkillPresentationTiming[] SkillTimings { get; set; }
        public float EnemyAttackClipMarginSeconds { get; set; }
        public float EnemyDeathClipMarginSeconds { get; set; }
        public Vector3 DamagePunchScale { get; set; }
        public float DamagePunchDuration { get; set; }
        public int DamagePunchVibrato { get; set; }
        public float DamagePunchElasticity { get; set; }
        public float DamageShrinkDuration { get; set; }
        public bool SyncHpAsVerticalScale { get; set; }
        public Transform CorruptionIncreaseFeedbackRoot { get; set; }
        public Vector3 CorruptionPulseScale { get; set; }
        public float CorruptionPulseDuration { get; set; }
        public int CorruptionPulseVibrato { get; set; }
        public float CorruptionPulseElasticity { get; set; }
        public Vector3 ActorActionRockPunch { get; set; }
        public int ActorActionRockVibrato { get; set; }
        public float ActorActionRockElasticity { get; set; }
    }
}