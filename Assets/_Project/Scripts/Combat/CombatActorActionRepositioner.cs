using System;
using DG.Tweening;
using Erumperem.Combat.Runtime;
using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Reposiciona o personagem em ação (Aliado ou Inimigo) até a âncora de destaque (Stage)
    /// e o devolve à formação original ao término da apresentação.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(25)]
    public sealed class CombatActorActionRepositioner : MonoBehaviour
    {
        private const string ActionRockTweenId = "CombatActionRock";
        private const float MinActionDurationThreshold = 0.2f;

        [Header("Referências Centrais")]
        [SerializeField] private CombatSessionHub sessionHub;
        [SerializeField] private CombatPrototypeController combatController;

        [Header("Pontos de Ação (Âncoras Invisíveis)")]
        [Tooltip("Objeto onde os heróis se posicionam para atacar.")]
        [SerializeField] private Transform allyStagingAnchor;

        [Tooltip("Objeto onde os monstros se posicionam para atacar.")]
        [SerializeField] private Transform enemyStagingAnchor;

        [Header("Filtros")]
        [SerializeField] private bool repositionAllies = true;
        [SerializeField] private bool repositionEnemies = true;

        [Header("Rotação e Orientação")]
        [SerializeField] private bool faceTargetWhenActing = true;
        [SerializeField] private bool matchAnchorRotation = false;

        [Header("Animação (DOTween)")]
        [SerializeField] private float moveToStagingDuration = 0.22f;
        [SerializeField] private Ease moveToStagingEase = Ease.OutQuad;

        [SerializeField] private float returnToOriginDuration = 0.22f;
        [SerializeField] private Ease returnToOriginEase = Ease.OutQuad;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Estado do personagem no palco
        private Transform _stagedTransform;
        private Vector3 _originalWorldPosition;
        private Quaternion _originalWorldRotation;
        private string _currentStagedActorId;
        private Animator _affectedAnimator;
        private bool _originalRootMotionState;
        private float _lastSkillStartTime;
        private int _actionSequenceCounter;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            RestoreStagedImmediate();
        }

        private void ResolveReferences()
        {
            if (sessionHub == null)
            {
                sessionHub = GetComponent<CombatSessionHub>() ?? FindFirstObjectByType<CombatSessionHub>();
            }

            if (combatController == null)
            {
                combatController = GetComponent<CombatPrototypeController>() ?? FindFirstObjectByType<CombatPrototypeController>();
            }
        }

        private void SubscribeEvents()
        {
            if (sessionHub == null) return;

            sessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillStarted;
            sessionHub.OnCombatSkillExecutionPresentationStarted += HandleSkillStarted;

            sessionHub.OnActionPresentationEnded -= HandleActionEnded;
            sessionHub.OnActionPresentationEnded += HandleActionEnded;

            sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
            sessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
        }

        private void UnsubscribeEvents()
        {
            if (sessionHub == null) return;

            sessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillStarted;
            sessionHub.OnActionPresentationEnded -= HandleActionEnded;
            sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
        }

        private void HandleCombatSessionClosed()
        {
            RestoreStagedImmediate();
        }

        private void HandleSkillStarted(string actorId, string targetId)
        {
            if (combatController == null) ResolveReferences();
            if (combatController == null || string.IsNullOrEmpty(actorId)) return;

            // Se ainda houver alguém no palco da rodada anterior, garante o retorno imediato
            if (_stagedTransform != null)
            {
                RestoreStagedImmediate();
            }

            // 1. Identifica o lado/facção
            var actorCombatant = combatController.FindCombatantById(actorId);
            bool isAlly = false;
            bool isEnemy = false;

            if (actorCombatant != null)
            {
                isAlly = actorCombatant.Position?.Side == Side.Allies || actorCombatant.Identity.Faction == Faction.Player;
                isEnemy = actorCombatant.Position?.Side == Side.Enemies || actorCombatant.Identity.Faction == Faction.Enemy;
            }
            else
            {
                isAlly = actorId.StartsWith("ally", StringComparison.OrdinalIgnoreCase);
                isEnemy = actorId.StartsWith("enemy", StringComparison.OrdinalIgnoreCase);
            }

            if (isAlly && !repositionAllies) return;
            if (isEnemy && !repositionEnemies) return;

            // 2. Resolve a âncora
            Transform anchor = isAlly ? allyStagingAnchor : (enemyStagingAnchor != null ? enemyStagingAnchor : allyStagingAnchor);
            if (anchor == null)
            {
                if (enableDebugLogs) Debug.LogWarning($"[Stage] Âncora de Stage não configurada para {(isAlly ? "Aliado" : "Inimigo")}.");
                return;
            }

            // 3. Obtém o visual
            Transform unitVisual = combatController.TryGetUnitVisualRoot(actorId);
            if (unitVisual == null)
            {
                if (enableDebugLogs) Debug.LogWarning($"[Stage] Visual não encontrado para '{actorId}'.");
                return;
            }

            Transform targetToMove = ResolveMovementTarget(unitVisual);

            // 4. Marca o tempo desta nova ação para bloquear cancelamentos fantasmas
            _actionSequenceCounter++;
            _lastSkillStartTime = Time.unscaledTime;
            _stagedTransform = targetToMove;
            _currentStagedActorId = actorId;
            _originalWorldPosition = targetToMove.position;
            _originalWorldRotation = targetToMove.rotation;

            // Desativa temporariamente o Root Motion para que a animação não trave a posição
            _affectedAnimator = targetToMove.GetComponentInChildren<Animator>(true);
            if (_affectedAnimator != null)
            {
                _originalRootMotionState = _affectedAnimator.applyRootMotion;
                _affectedAnimator.applyRootMotion = false;
            }

            // Cancela tweens concorrentes no objeto (como o CombatActionRock nativo)
            DOTween.Kill(ActionRockTweenId, false);
            targetToMove.DOKill(false);

            // 5. Calcula rotação para encarar o alvo
            Quaternion targetRotation = targetToMove.rotation;
            if (faceTargetWhenActing && !string.IsNullOrEmpty(targetId))
            {
                Transform targetVisual = combatController.TryGetUnitVisualRoot(targetId);
                if (targetVisual != null)
                {
                    Vector3 lookDir = targetVisual.position - anchor.position;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        targetRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                    }
                }
            }
            else if (matchAnchorRotation)
            {
                targetRotation = anchor.rotation;
            }

            if (enableDebugLogs)
            {
                string tagColor = isAlly ? "#38BDF8" : "#F87171";
                Debug.Log($"<color={tagColor}>[Stage]</color> Movendo <b>{targetToMove.name}</b> ({actorId}) até <b>{anchor.name}</b>.");
            }

            // 6. Inicia o deslocamento imediatamente (sem yield)
            if (moveToStagingDuration > 0f)
            {
                targetToMove.DOMove(anchor.position, moveToStagingDuration)
                    .SetEase(moveToStagingEase)
                    .SetLink(targetToMove.gameObject);

                targetToMove.DORotateQuaternion(targetRotation, moveToStagingDuration)
                    .SetEase(moveToStagingEase)
                    .SetLink(targetToMove.gameObject);
            }
            else
            {
                targetToMove.position = anchor.position;
                targetToMove.rotation = targetRotation;
            }
        }

        private void HandleActionEnded()
        {
            if (_stagedTransform == null) return;

            // SEGURANÇA CRÍTICA: Se este evento foi disparado menos de 0.2s após o início da ação,
            // trata-se do evento atrasado da ação ANTERIOR! Nós o descartamos para não cancelar o novo ator.
            if (Time.unscaledTime - _lastSkillStartTime < MinActionDurationThreshold)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=#FBBF24>[Stage]</color> Descartando evento de término atrasado pertencente à ação anterior.");
                }
                return;
            }

            var target = _stagedTransform;
            var returnPos = _originalWorldPosition;
            var returnRot = _originalWorldRotation;
            var anim = _affectedAnimator;
            bool rootMotion = _originalRootMotionState;

            _stagedTransform = null;
            _currentStagedActorId = null;
            _affectedAnimator = null;

            DOTween.Kill(ActionRockTweenId, false);
            target.DOKill(false);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=#4ADE80>[Stage]</color> Retornando <b>{target.name}</b> para a formação original.");
            }

            if (returnToOriginDuration > 0f)
            {
                target.DOMove(returnPos, returnToOriginDuration)
                    .SetEase(returnToOriginEase)
                    .SetLink(target.gameObject)
                    .OnComplete(() =>
                    {
                        if (anim != null) anim.applyRootMotion = rootMotion;
                    });

                target.DORotateQuaternion(returnRot, returnToOriginDuration)
                    .SetEase(returnToOriginEase)
                    .SetLink(target.gameObject);
            }
            else
            {
                target.position = returnPos;
                target.rotation = returnRot;
                if (anim != null) anim.applyRootMotion = rootMotion;
            }
        }

        private Transform ResolveMovementTarget(Transform unitVisual)
        {
            if (unitVisual.parent != null &&
                unitVisual.parent != transform &&
                !unitVisual.parent.name.Equals("Units", StringComparison.OrdinalIgnoreCase) &&
                !unitVisual.parent.name.Equals("CombatSceneCore", StringComparison.OrdinalIgnoreCase))
            {
                return unitVisual.parent;
            }

            return unitVisual;
        }

        private void RestoreStagedImmediate()
        {
            if (_stagedTransform == null) return;

            DOTween.Kill(ActionRockTweenId, false);
            _stagedTransform.DOKill(false);

            _stagedTransform.position = _originalWorldPosition;
            _stagedTransform.rotation = _originalWorldRotation;

            if (_affectedAnimator != null)
            {
                _affectedAnimator.applyRootMotion = _originalRootMotionState;
                _affectedAnimator = null;
            }

            _stagedTransform = null;
            _currentStagedActorId = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (allyStagingAnchor != null)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
                Gizmos.DrawWireSphere(allyStagingAnchor.position, 0.4f);
                Gizmos.DrawLine(allyStagingAnchor.position, allyStagingAnchor.position + allyStagingAnchor.forward * 1.5f);
            }

            if (enemyStagingAnchor != null)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(enemyStagingAnchor.position, 0.4f);
                Gizmos.DrawLine(enemyStagingAnchor.position, enemyStagingAnchor.position + enemyStagingAnchor.forward * 1.5f);
            }
        }
#endif
    }
}