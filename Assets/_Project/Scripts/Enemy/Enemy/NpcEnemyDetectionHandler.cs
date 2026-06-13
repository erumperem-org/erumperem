// ============================================================
// NpcEnemyDetectionHandler.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Responsabilidade única: gerenciar o polling do Detector
// e traduzir eventos de detecção em notificações para a
// máquina de estados.
//
// Não conhece coroutines de comportamento, navegação ou pool.
// ============================================================

using System;
using System.Collections;
using DetectionSystem.Core;
using Systems.NPC.Enemy.Contracts;
using Systems.NPC.Enemy.StateMachine;
using UnityEngine;

namespace Systems.NPC.Enemy
{
    public sealed class NpcEnemyDetectionHandler
    {
        // ── Dependências ──────────────────────────────────────────────────

        private readonly Detector             _detector;
        private readonly NpcEnemyStateMachine _stateMachine;
        private readonly NpcEnemy             _npcEnemy;   // para NotifyPlayerContact
        private readonly MonoBehaviour        _owner;      // dono das coroutines

        // ── Configuração ──────────────────────────────────────────────────

        private const float DetectionPollInterval = 0.15f;

        // ── Coroutine ─────────────────────────────────────────────────────

        private Coroutine _pollingCoroutine;

        // ── Construtor ────────────────────────────────────────────────────

        public NpcEnemyDetectionHandler(
            Detector detector,
            NpcEnemyStateMachine stateMachine,
            NpcEnemy npcEnemy,
            MonoBehaviour owner)
        {
            _detector     = detector;
            _stateMachine = stateMachine;
            _npcEnemy     = npcEnemy;
            _owner        = owner;
        }

        // ── Ciclo de vida ─────────────────────────────────────────────────

        public void StartPolling()
        {
            StopPolling();
            _detector.OnDetectorEnter += OnDetectorEnter;
            _detector.OnDetectorExit  += OnDetectorExit;
            _pollingCoroutine = _owner.StartCoroutine(PollingCoroutine());
        }

        public void StopPolling()
        {
            _detector.OnDetectorEnter -= OnDetectorEnter;
            _detector.OnDetectorExit  -= OnDetectorExit;

            if (_pollingCoroutine == null) return;
            _owner.StopCoroutine(_pollingCoroutine);
            _pollingCoroutine = null;
        }

        // ── Coroutine interna ─────────────────────────────────────────────

        private IEnumerator PollingCoroutine()
        {
            var wait = new WaitForSeconds(DetectionPollInterval);
            while (true)
            {
                _detector.Scan();
                yield return wait;
            }
        }

        // ── Handlers do Detector ──────────────────────────────────────────

        private void OnDetectorEnter(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (_stateMachine.Is(NpcEnemyState.ReturningToPool)) return;
            if (!IsPlayerCollider(detected)) return;

            if (shapeLabel == "Perception" && _stateMachine.Is(NpcEnemyState.Wander))
                _stateMachine.ToChase(ResolvePlayerTransform(detected));

            if (shapeLabel == "Contact")
                _npcEnemy.NotifyPlayerContact();
        }

        private void OnDetectorExit(Collider detected, string shapeLabel, int shapeIndex)
        {
            if (!_stateMachine.Is(NpcEnemyState.Chase)) return;
            if (!IsPlayerCollider(detected)) return;

            if (shapeLabel == "Perception")
                _stateMachine.ToWander();
        }

        private static bool IsPlayerCollider(Collider collider)
        {
            if (collider == null) return false;

            Transform current = collider.transform;
            while (current != null)
            {
                if (current.CompareTag("Player")) return true;
                current = current.parent;
            }

            return collider.GetComponentInParent<PlayableCharacter>() != null;
        }

        private static Transform ResolvePlayerTransform(Collider detected)
        {
            PlayableCharacter playableCharacter = detected.GetComponentInParent<PlayableCharacter>();
            return playableCharacter != null ? playableCharacter.Transform : detected.transform;
        }
    }
}
