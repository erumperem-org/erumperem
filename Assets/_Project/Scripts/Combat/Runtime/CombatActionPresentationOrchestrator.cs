using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Presentation;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    public sealed class CombatActionPresentationOrchestrator
    {
        private const string ActionRockTweenId = "CombatActionRock";
        private const float MultiHitDamageStaggerSeconds = 0.12f;
        private const string CorruptionPulseTweenId = "CombatCorruptionPulse";

        private readonly MonoBehaviour _coroutineHost;
        private readonly CombatSessionRuntime _session;
        private readonly CombatSessionHub _sessionHub;
        private readonly CombatUnitVisualSynchronizer _unitVisualSynchronizer;
        private readonly CombatActionPresentationSettings _settings;
        private readonly bool _logEventsToConsole;

        private Transform _actionRockTransform;
        private Vector3 _actionRockBaseLocalPosition;
        private Animator _currentActingAnimator;

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

        public void PlayDamageVisualFeedback(string targetCombatantId, float delaySeconds = 0f)
        {
            if (AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX("Damage");
            }

            if (!_session.UnitVisualRootsByCombatantId.TryGetValue(targetCombatantId, out var unitRoot) || unitRoot == null)
            {
                return;
            }

            var combatant = _session.FindCombatantById(targetCombatantId);
            if (combatant == null || combatant.Health.IsDead)
            {
                return;
            }

            float speedMultiplier = Mathf.Max(0.2f, CombatSpeedSettings.SpeedMultiplier);

            _session.DamageFeedbackBusy.Add(targetCombatantId);
            var sequence = DOTween.Sequence();
            sequence.SetTarget(unitRoot);
            if (delaySeconds > 0f)
            {
                sequence.AppendInterval(delaySeconds / speedMultiplier);
            }

            sequence.Append(
                unitRoot.DOPunchScale(
                    _settings.DamagePunchScale,
                    _settings.DamagePunchDuration / speedMultiplier,
                    _settings.DamagePunchVibrato,
                    _settings.DamagePunchElasticity));
            if (_settings.SyncHpAsVerticalScale)
            {
                var targetY = Mathf.Max(0.3f, combatant.Health.CurrentHp / (float)combatant.Health.MaxHp);
                sequence.Append(unitRoot.DOScaleY(targetY, _settings.DamageShrinkDuration / speedMultiplier).SetEase(Ease.OutCubic));
            }

            sequence.OnComplete(() => _session.DamageFeedbackBusy.Remove(targetCombatantId));
        }

        /// <summary>
        /// Action Shot: Actor Focus (zoom + ataque) → Target Focus no Impact (snap + reações) → Return.
        /// A simulação resolve no início, mas todo o feedback fica segurado até o Impact.
        /// </summary>
        private IEnumerator PresentActionRoutine(ChosenAction action, Action onStepComplete)
        {
            float speedMultiplier = Mathf.Max(0.2f, CombatSpeedSettings.SpeedMultiplier);
            CombatAttackImpactRelay impactRelay = null;
            var impactReceived = false;
            void HandleImpact() => impactReceived = true;

            try
            {
                StopActorActionRock();
                GetTimingForSkill(action.Skill.Id, out var playSeconds, out var postPauseSeconds);

                EnemyAnimationController enemyActorVisual = null;
                if (action.Actor.Identity.Faction == Faction.Enemy &&
                    _unitVisualSynchronizer.TryGetAnimationController(action.Actor.Identity.Id, out enemyActorVisual))
                {
                    var attackHoldSeconds = enemyActorVisual.ComputeAttackPresentationDurationSeconds(_settings.EnemyAttackClipMarginSeconds);
                    playSeconds = Mathf.Max(playSeconds, attackHoldSeconds);
                }

                // Ajusta as esperas e acelera o Animator do ator sem alterar Time.timeScale
                playSeconds /= speedMultiplier;
                postPauseSeconds /= speedMultiplier;

                if (_session.UnitVisualRootsByCombatantId.TryGetValue(action.Actor.Identity.Id, out var actorVisual))
                {
                    _currentActingAnimator = actorVisual.GetComponentInChildren<Animator>(true);
                    if (_currentActingAnimator != null)
                    {
                        _currentActingAnimator.speed = speedMultiplier;
                        if (!_currentActingAnimator.TryGetComponent(out impactRelay))
                        {
                            impactRelay = _currentActingAnimator.gameObject.AddComponent<CombatAttackImpactRelay>();
                        }

                        impactRelay.Impacted += HandleImpact;
                    }
                }

                _sessionHub?.RaiseActionPresentationStarted();
                _session.OngoingPresentationActorCombatantId = action.Actor.Identity.Id;
                _session.OngoingPresentationTargetCombatantId = action.Target.Identity.Id;

                // Passo 1: zoom no ator.
                if (actorVisual != null)
                {
                    _sessionHub?.RaiseActionShotActorFocus(actorVisual);
                }

                // Passo 2: animação de ataque.
                _sessionHub?.RaiseCombatSkillExecutionPresentationStarted(
                    _session.OngoingPresentationActorCombatantId,
                    _session.OngoingPresentationTargetCombatantId,
                    action.Skill?.Id ?? string.Empty);
                enemyActorVisual?.NotifyAttackPresentationBegin(playSeconds, speedMultiplier);
                if (playSeconds > 0.02f)
                {
                    BeginActorActionRock(action, playSeconds);
                }

                var startEventIndex = _session.EventCollector.Events.Count;
                _session.Simulator.ResolveChosenAction(_session.State, action);
                var eventSlice = _session.EventCollector.Events.GetRange(
                    startEventIndex,
                    _session.EventCollector.Events.Count - startEventIndex);
                var plan = ActionShotPlan.Build(action.Actor.Identity.Id, action.Target.Identity.Id, eventSlice);

                var secondsUntilImpact = 0f;
                while (!impactReceived && secondsUntilImpact < playSeconds)
                {
                    secondsUntilImpact += Time.deltaTime;
                    yield return null;
                }

                // Passo 3: snap para todos os afetados.
                if (plan.HasTargetFocus)
                {
                    _sessionHub?.RaiseActionShotTargetFocus(ResolveVisualRoots(plan.AffectedCombatantIds));
                }

                // Passo 4: reações (HitTaken, dano, morte, narrativa).
                var targetHoldSeconds = Mathf.Max(postPauseSeconds, ApplyImpactFeedback(action, eventSlice, speedMultiplier));
                var deathHoldCapSeconds = _settings.ActionShotDeathHoldCapSeconds / speedMultiplier;
                var heldSeconds = 0f;
                while (heldSeconds < targetHoldSeconds ||
                       (heldSeconds < deathHoldCapSeconds && AnyDeathVisualPending(plan.DiedCombatantIds)))
                {
                    heldSeconds += Time.deltaTime;
                    yield return null;
                }
            }
            finally
            {
                if (impactRelay != null)
                {
                    impactRelay.Impacted -= HandleImpact;
                }

                if (_currentActingAnimator != null)
                {
                    _currentActingAnimator.speed = 1f;
                    _currentActingAnimator = null;
                }

                // Passo 5: Return. O próximo turno pode começar durante o blend.
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

        /// <summary>Publica o feedback segurado até o Impact. Retorna quanto tempo a câmera precisa ficar no alvo.</summary>
        private float ApplyImpactFeedback(ChosenAction action, IReadOnlyList<CombatEvent> eventSlice, float speedMultiplier)
        {
            if (eventSlice.Count == 0)
            {
                return 0f;
            }

            var narrativeLines = CombatNarrativeFormatter.BuildLines(_session.State, action, eventSlice).ToList();
            if (narrativeLines.Count > 0)
            {
                _sessionHub?.RaiseNarrativeLines(narrativeLines);
            }

            var requiredHoldSeconds = 0f;
            var damageFeedbackEventCount = 0;
            foreach (var combatEvent in eventSlice)
            {
                if (combatEvent.EventType == BattleEventType.CorruptionAdjusted)
                {
                    PublishCorruptionPresentation(combatEvent);
                }

                if (combatEvent.EventType == BattleEventType.CombatantDied && !string.IsNullOrEmpty(combatEvent.TargetId))
                {
                    _sessionHub?.RaiseCombatantPresentationDeath(combatEvent.TargetId);
                    if (_unitVisualSynchronizer.TryGetAnimationController(combatEvent.TargetId, out var deadEnemyVisual))
                    {
                        deadEnemyVisual.EnsureDeathVisualSequenceStarted(_settings.EnemyDeathClipMarginSeconds, speedMultiplier);
                    }
                }

                if (combatEvent.EventType == BattleEventType.DamageApplied && combatEvent.DamageAmount > 0)
                {
                    damageFeedbackEventCount++;
                    var hitDelaySeconds = (damageFeedbackEventCount - 1) * MultiHitDamageStaggerSeconds;
                    PlayDamageVisualFeedback(combatEvent.TargetId, hitDelaySeconds);

                    var hitCombatantId = combatEvent.TargetId;
                    var hitTakenSeconds = 0f;
                    _unitVisualSynchronizer.TryGetAnimationController(hitCombatantId, out var hitEnemyAnimationController);
                    if (hitEnemyAnimationController != null)
                    {
                        hitTakenSeconds = hitEnemyAnimationController.ComputeHitTakenPresentationDurationSeconds(0f) / speedMultiplier;
                    }

                    var scaledHitDelaySeconds = hitDelaySeconds / speedMultiplier;
                    DOVirtual.DelayedCall(scaledHitDelaySeconds, () =>
                    {
                        _sessionHub?.RaiseCombatantPresentationHitTaken(hitCombatantId);
                        if (hitEnemyAnimationController != null)
                        {
                            hitEnemyAnimationController.NotifyHitTakenPresentationBegin(hitTakenSeconds, speedMultiplier);
                        }
                    });

                    requiredHoldSeconds = Mathf.Max(
                        requiredHoldSeconds,
                        scaledHitDelaySeconds + Mathf.Max(hitTakenSeconds, _settings.DamagePunchDuration / speedMultiplier));
                }
            }

            LogLastCombatEvent();
            return requiredHoldSeconds;
        }

        private List<Transform> ResolveVisualRoots(IReadOnlyList<string> combatantIds)
        {
            var visualRoots = new List<Transform>(combatantIds.Count);
            foreach (var combatantId in combatantIds)
            {
                if (_session.UnitVisualRootsByCombatantId.TryGetValue(combatantId, out var visualRoot) && visualRoot != null)
                {
                    visualRoots.Add(visualRoot);
                }
            }

            return visualRoots;
        }

        private bool AnyDeathVisualPending(IReadOnlyList<string> diedCombatantIds)
        {
            foreach (var combatantId in diedCombatantIds)
            {
                if (_unitVisualSynchronizer.TryGetAnimationController(combatantId, out var enemyVisual) &&
                    !enemyVisual.IsDeathVisualSequenceFinished)
                {
                    return true;
                }
            }

            return false;
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

            if (!_session.UnitVisualRootsByCombatantId.TryGetValue(action.Actor.Identity.Id, out var actorRoot) || actorRoot == null)
            {
                return;
            }

            _actionRockTransform = actorRoot;
            _actionRockBaseLocalPosition = actorRoot.localPosition;
            actorRoot.DOPunchPosition(
                    _settings.ActorActionRockPunch,
                    totalDurationSeconds,
                    _settings.ActorActionRockVibrato,
                    _settings.ActorActionRockElasticity)
                .SetRelative(true)
                .SetId(ActionRockTweenId)
                .SetTarget(actorRoot)
                .OnKill(RestoreActorActionRockLocal)
                .OnComplete(RestoreActorActionRockLocal);
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

            float speedMultiplier = Mathf.Max(0.2f, CombatSpeedSettings.SpeedMultiplier);
            DOTween.Kill(CorruptionPulseTweenId, false);
            _settings.CorruptionIncreaseFeedbackRoot.DOPunchScale(
                    _settings.CorruptionPulseScale,
                    _settings.CorruptionPulseDuration / speedMultiplier,
                    _settings.CorruptionPulseVibrato,
                    _settings.CorruptionPulseElasticity)
                .SetId(CorruptionPulseTweenId)
                .SetLink(_settings.CorruptionIncreaseFeedbackRoot.gameObject);
        }

        private void GetTimingForSkill(string skillId, out float playSeconds, out float postPauseSeconds)
        {
            playSeconds = _settings.DefaultPlaySeconds;
            postPauseSeconds = _settings.DefaultPostPauseSeconds;
            if (_settings.SkillTimings == null) return;

            foreach (var timingEntry in _settings.SkillTimings)
            {
                if (timingEntry == null || string.IsNullOrEmpty(timingEntry.skillId)) continue;
                if (!string.Equals(timingEntry.skillId, skillId, StringComparison.OrdinalIgnoreCase)) continue;

                playSeconds = Mathf.Max(0f, timingEntry.playSeconds);
                postPauseSeconds = Mathf.Max(0f, timingEntry.postPauseSeconds);
                return;
            }
        }

        private void LogLastCombatEvent()
        {
            if (!_logEventsToConsole || _session.EventCollector.Events.Count == 0) return;

            var lastEvent = _session.EventCollector.Events[^1];
            Debug.Log(
                $"[Combat] {lastEvent.EventType} turn={lastEvent.Turn} actor={lastEvent.ActorId} " +
                $"target={lastEvent.TargetId} skill={lastEvent.SkillId} dmg={lastEvent.DamageAmount}");
        }
    }

    /// <summary>
    /// Configurações e referências visuais para apresentação de ações em combate.
    /// </summary>
    public sealed class CombatActionPresentationSettings
    {
        public float DefaultPlaySeconds { get; set; }
        public float DefaultPostPauseSeconds { get; set; }
        public CombatSkillPresentationTiming[] SkillTimings { get; set; }
        public float EnemyAttackClipMarginSeconds { get; set; }
        public float EnemyDeathClipMarginSeconds { get; set; }
        public float ActionShotDeathHoldCapSeconds { get; set; }
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