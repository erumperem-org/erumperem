using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Apresentação visual deste inimigo: Attack com duração guiada pelo combate; morte com clip + margem + encolher (DOTween).
    /// Não subscreve eventos globais do hub — o <see cref="CombatPrototypeController"/> chama métodos públicos só desta instância.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator unitAnimator;

        [Header("Animator state names (Base Layer)")]
        [SerializeField] private string idleStateName = "Idle";
        [SerializeField] private string attackStateName = "Attack";
        [SerializeField] private string deathStateName = "Death";

        [SerializeField] private float crossFadeSeconds = 0.08f;

        [Header("Duração quando o clip não é encontrado pelo nome")]
        [SerializeField] private float attackClipLengthFallbackSeconds = 2f;
        [SerializeField] private float deathClipLengthFallbackSeconds = 2f;

        [Header("Mortes: encolher após o clip")]
        [SerializeField] private float deathDespawnPunchDurationSeconds = 0.12f;
        [SerializeField] private Vector3 deathDespawnPunchScale = new(0.12f, 0.18f, 0.12f);
        [SerializeField] private int deathDespawnPunchVibrato = 6;
        [SerializeField] private float deathDespawnPunchElasticity = 0.45f;
        [SerializeField] private float deathDespawnScaleDownDurationSeconds = 0.35f;

        private Coroutine _attackReturnToIdleRoutine;
        private Coroutine _deathVisualRoutine;
        private bool _deathVisualSequenceStarted;
        private bool _deathVisualSequenceFinished;

        /// <summary>Quando true, <see cref="CombatPrototypeController.SyncUnitVisuals"/> pode desativar o root.</summary>
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
        }

        /// <summary>Duração do clip de Attack (nome do estado ou do clip) + margem.</summary>
        public float ComputeAttackPresentationDurationSeconds(float marginSeconds)
        {
            return Mathf.Max(0.05f, TryResolveClipLengthSeconds(attackStateName, attackClipLengthFallbackSeconds) + marginSeconds);
        }

        /// <summary>Duração do clip de Death + margem após o clip (ex.: 1 s).</summary>
        public float ComputeDeathPresentationWaitSeconds(float marginAfterClipSeconds)
        {
            return Mathf.Max(0.05f, TryResolveClipLengthSeconds(deathStateName, deathClipLengthFallbackSeconds) + marginAfterClipSeconds);
        }

        /// <summary>Chamado uma vez por ação do ator: reproduz Attack durante <paramref name="holdAttackStateSeconds"/> e volta a Idle.</summary>
        public void NotifyAttackPresentationBegin(float holdAttackStateSeconds)
        {
            if (_deathVisualSequenceStarted || unitAnimator == null)
            {
                return;
            }

            StopAttackReturnRoutine();
            unitAnimator.CrossFade(attackStateName, crossFadeSeconds, 0, 0f);
            _attackReturnToIdleRoutine = StartCoroutine(AttackHoldThenIdleRoutine(holdAttackStateSeconds));
        }

        /// <summary>Inicia sequência de morte (idempotente). Chamado a partir da resolução da ação ou do sync se a morte veio fora da apresentação.</summary>
        public void EnsureDeathVisualSequenceStarted(float marginAfterClipSeconds)
        {
            if (_deathVisualSequenceStarted || unitAnimator == null)
            {
                return;
            }

            _deathVisualSequenceStarted = true;
            StopAttackReturnRoutine();
            unitAnimator.CrossFade(deathStateName, crossFadeSeconds, 0, 0f);
            var waitSeconds = ComputeDeathPresentationWaitSeconds(marginAfterClipSeconds);
            _deathVisualRoutine = StartCoroutine(DeathWaitThenDespawnRoutine(waitSeconds));
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

        private IEnumerator AttackHoldThenIdleRoutine(float holdAttackStateSeconds)
        {
            yield return new WaitForSeconds(holdAttackStateSeconds);
            _attackReturnToIdleRoutine = null;
            if (!_deathVisualSequenceStarted && unitAnimator != null)
            {
                unitAnimator.CrossFade(idleStateName, crossFadeSeconds, 0, 0f);
            }
        }

        private IEnumerator DeathWaitThenDespawnRoutine(float waitBeforeShrinkSeconds)
        {
            yield return new WaitForSeconds(waitBeforeShrinkSeconds);
            var despawnTweenId = GetInstanceID();
            transform.DOKill(false);
            transform.DOPunchScale(
                    deathDespawnPunchScale,
                    deathDespawnPunchDurationSeconds,
                    deathDespawnPunchVibrato,
                    deathDespawnPunchElasticity)
                .SetId(despawnTweenId)
                .SetLink(gameObject)
                .OnComplete(PlayDeathScaleDownTween);
            _deathVisualRoutine = null;
        }

        private void PlayDeathScaleDownTween()
        {
            var despawnTweenId = GetInstanceID();
            transform.DOKill(false);
            transform.DOScale(Vector3.zero, deathDespawnScaleDownDurationSeconds)
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
