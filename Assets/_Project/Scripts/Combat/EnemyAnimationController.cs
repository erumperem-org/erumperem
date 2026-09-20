using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Erumperem.Combat
{
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator unitAnimator;

        [Header("Animator state names (Base Layer)")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string hitTakenStateName = "HitTaken";
        [SerializeField] private string deathStateName = "Death";

        [SerializeField] private float crossFadeSeconds = 0.08f;

        [Header("Duração quando o clip não é encontrado pelo nome")]
        [SerializeField] private float attackClipLengthFallbackSeconds = 2f;
        [SerializeField] private float hitTakenClipLengthFallbackSeconds = 0.5f;
        [SerializeField] private float deathClipLengthFallbackSeconds = 2f;

        [Header("Mortes: encolher após o clip")]
        [SerializeField] private float deathDespawnPunchDurationSeconds = 0.12f;
        [SerializeField] private Vector3 deathDespawnPunchScale = new(0.12f, 0.18f, 0.12f);
        [SerializeField] private int deathDespawnPunchVibrato = 6;
        [SerializeField] private float deathDespawnPunchElasticity = 0.45f;
        [SerializeField] private float deathDespawnScaleDownDurationSeconds = 0.35f;

        [Header("Hit Taken VFX")]
        [SerializeField] private GameObject hitTakenVfxPrefab;
        [SerializeField] private Vector3 hitTakenVfxOffset = Vector3.zero;
        [SerializeField] private bool destroySpawnedHitVfx = true;
        [SerializeField] private float destroyHitVfxAfterSeconds = 5f;

        private Coroutine _attackReturnToIdleRoutine;
        private Coroutine _deathVisualRoutine;
        private Coroutine _hitTakenReturnToIdleRoutine;
        private bool _deathVisualSequenceStarted;
        private bool _deathVisualSequenceFinished;

        public bool IsDeathVisualSequenceFinished => _deathVisualSequenceFinished;

        private void Awake()
        {
            if (unitAnimator == null)
            {
                unitAnimator = GetComponentInChildren<Animator>(true);
            }
        }

        private void OnDisable()
        {
            StopPresentationCoroutines();
            transform.DOKill(false);
            if (unitAnimator != null)
            {
                unitAnimator.speed = 1f;
            }
        }

        public void SetPlaybackSpeed(float speedMultiplier)
        {
            if (unitAnimator != null)
            {
                unitAnimator.speed = Mathf.Max(0.1f, speedMultiplier);
            }
        }

        public void ResetPlaybackSpeed()
        {
            if (unitAnimator != null)
            {
                unitAnimator.speed = 1f;
            }
        }

        public float ComputeAttackPresentationDurationSeconds(float marginSeconds)
        {
            return Mathf.Max(0.05f, TryResolveClipLengthSeconds(attackStateName, attackClipLengthFallbackSeconds) + marginSeconds);
        }

        public float ComputeHitTakenPresentationDurationSeconds(float marginSeconds)
        {
            return Mathf.Max(0.05f, TryResolveClipLengthSeconds(hitTakenStateName, hitTakenClipLengthFallbackSeconds) + marginSeconds);
        }

        public float ComputeDeathPresentationWaitSeconds(float marginAfterClipSeconds)
        {
            return Mathf.Max(0.05f, TryResolveClipLengthSeconds(deathStateName, deathClipLengthFallbackSeconds) + marginAfterClipSeconds);
        }

        public void NotifyAttackPresentationBegin(float holdAttackStateSeconds, float speedMultiplier = 1f)
        {
            if (_deathVisualSequenceStarted || unitAnimator == null)
            {
                return;
            }

            StopAttackReturnRoutine();
            SetPlaybackSpeed(speedMultiplier);
            unitAnimator.CrossFade(attackStateName, crossFadeSeconds / Mathf.Max(0.1f, speedMultiplier), 0, 0f);
            _attackReturnToIdleRoutine = StartCoroutine(AttackHoldThenIdleRoutine(holdAttackStateSeconds));
        }

        public void NotifyHitTakenPresentationBegin(float holdHitTakenStateSeconds, float speedMultiplier = 1f)
        {
            if (_deathVisualSequenceStarted || unitAnimator == null)
            {
                return;
            }

            StopHitTakenReturnRoutine();
            SpawnHitTakenVfx();
            SetPlaybackSpeed(speedMultiplier);
            unitAnimator.CrossFade(hitTakenStateName, crossFadeSeconds / Mathf.Max(0.1f, speedMultiplier), 0, 0f);
            _hitTakenReturnToIdleRoutine = StartCoroutine(HitTakenHoldThenIdleRoutine(holdHitTakenStateSeconds));
        }

        private void SpawnHitTakenVfx()
        {
            if (hitTakenVfxPrefab == null) return;

            var spawnedVfx = Instantiate(hitTakenVfxPrefab, transform.position + hitTakenVfxOffset, Quaternion.identity);
            if (destroySpawnedHitVfx)
            {
                Destroy(spawnedVfx, destroyHitVfxAfterSeconds);
            }
        }

        public void EnsureDeathVisualSequenceStarted(float marginAfterClipSeconds, float speedMultiplier = 1f)
        {
            if (_deathVisualSequenceStarted || unitAnimator == null) return;

            _deathVisualSequenceStarted = true;
            StopAttackReturnRoutine();
            SetPlaybackSpeed(speedMultiplier);
            unitAnimator.CrossFade(deathStateName, crossFadeSeconds / Mathf.Max(0.1f, speedMultiplier), 0, 0f);
            var waitSeconds = (ComputeDeathPresentationWaitSeconds(marginAfterClipSeconds)) / Mathf.Max(0.1f, speedMultiplier);
            _deathVisualRoutine = StartCoroutine(DeathWaitThenDespawnRoutine(waitSeconds, speedMultiplier));
        }

        private void StopPresentationCoroutines()
        {
            StopAttackReturnRoutine();
            if (_deathVisualRoutine != null)
            {
                StopCoroutine(_deathVisualRoutine);
                _deathVisualRoutine = null;
            }
        }

        private void StopAttackReturnRoutine()
        {
            if (_attackReturnToIdleRoutine != null)
            {
                StopCoroutine(_attackReturnToIdleRoutine);
                _attackReturnToIdleRoutine = null;
            }
        }

        private void StopHitTakenReturnRoutine()
        {
            if (_hitTakenReturnToIdleRoutine != null)
            {
                StopCoroutine(_hitTakenReturnToIdleRoutine);
                _hitTakenReturnToIdleRoutine = null;
            }
        }

        private IEnumerator AttackHoldThenIdleRoutine(float holdAttackStateSeconds)
        {
            yield return new WaitForSeconds(holdAttackStateSeconds);
            _attackReturnToIdleRoutine = null;
            ResetPlaybackSpeed();
            if (!_deathVisualSequenceStarted && unitAnimator != null)
            {
                unitAnimator.CrossFade(idleStateName, crossFadeSeconds, 0, 0f);
            }
        }

        private IEnumerator HitTakenHoldThenIdleRoutine(float holdHitTakenStateSeconds)
        {
            yield return new WaitForSeconds(holdHitTakenStateSeconds);
            _hitTakenReturnToIdleRoutine = null;
            ResetPlaybackSpeed();
            if (!_deathVisualSequenceStarted && unitAnimator != null)
            {
                unitAnimator.CrossFade(idleStateName, crossFadeSeconds, 0, 0f);
            }
        }

        private IEnumerator DeathWaitThenDespawnRoutine(float waitBeforeShrinkSeconds, float speedMultiplier)
        {
            yield return new WaitForSeconds(waitBeforeShrinkSeconds);
            var despawnTweenId = GetInstanceID();
            transform.DOKill(false);
            float punchDur = deathDespawnPunchDurationSeconds / Mathf.Max(0.1f, speedMultiplier);
            transform.DOPunchScale(
                    deathDespawnPunchScale,
                    punchDur,
                    deathDespawnPunchVibrato,
                    deathDespawnPunchElasticity)
                .SetId(despawnTweenId)
                .SetLink(gameObject)
                .OnComplete(() => PlayDeathScaleDownTween(speedMultiplier));
            _deathVisualRoutine = null;
        }

        private void PlayDeathScaleDownTween(float speedMultiplier)
        {
            var despawnTweenId = GetInstanceID();
            transform.DOKill(false);
            float scaleDownDur = deathDespawnScaleDownDurationSeconds / Mathf.Max(0.1f, speedMultiplier);
            transform.DOScale(Vector3.zero, scaleDownDur)
                .SetEase(Ease.InCubic)
                .SetId(despawnTweenId)
                .SetLink(gameObject)
                .OnComplete(FinishDeathVisualSequence);
        }

        private void FinishDeathVisualSequence()
        {
            _deathVisualSequenceFinished = true;
            gameObject.SetActive(false);
        }

        private float TryResolveClipLengthSeconds(string stateOrClipName, float fallbackDurationSeconds)
        {
            if (unitAnimator == null || unitAnimator.runtimeAnimatorController == null)
            {
                return fallbackDurationSeconds;
            }

            var animationClips = unitAnimator.runtimeAnimatorController.animationClips;
            if (animationClips == null || animationClips.Length == 0)
            {
                return fallbackDurationSeconds;
            }

            foreach (var animationClip in animationClips)
            {
                if (animationClip != null &&
                    string.Equals(animationClip.name, stateOrClipName, StringComparison.OrdinalIgnoreCase))
                {
                    return animationClip.length;
                }
            }

            foreach (var animationClip in animationClips)
            {
                if (animationClip != null &&
                    animationClip.name.IndexOf(stateOrClipName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return animationClip.length;
                }
            }

            return fallbackDurationSeconds;
        }
    }
}