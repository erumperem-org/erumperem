using System;
using System.Collections;
using System.Threading;
using Services.Navigation;
using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(NavMeshAgentAdapter))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _projectionDistance = 3f;
        [SerializeField] private float _stoppingDistance = 0.1f;
        [SerializeField] private PlayableAnimationController animationController;

        [Header("Rotação")]
        [Tooltip("Velocidade de rotação suave (graus/s equivalente).")]
        [SerializeField] private float _rotationSpeed = 10f;

        private INavMeshService _navMesh;
        private NavMeshAgentAdapter _adapter;
        private Coroutine _movementCoroutine;
        private CancellationTokenSource _linkCts;
        private bool _isTraversingLink;

        [HideInInspector] public PlayerInputReader _inputReader;

        public bool IsMoving => _navMesh != null && _adapter != null && _navMesh.IsMoving(_adapter);

        // ── Ciclo de vida ─────────────────────────────────────────────────

        private void Awake()
        {
            _adapter = GetComponent<NavMeshAgentAdapter>();
            _navMesh = new NavMeshService();
        }

        private void Start()
        {
            if (_navMesh == null)
            {
                Debug.LogError("[PlayerMovementController] INavMeshService não injetado.", this);
                enabled = false;
                return;
            }

            _navMesh.SetSpeed(_adapter, _speed);
            _navMesh.SetStoppingDistance(_adapter, _stoppingDistance);
            _adapter.SetUpdateRotation(false);
        }

        private void OnDisable() => CancelLinkTraversal();

        // ── Controle de movimento ─────────────────────────────────────────

        public void EnableMovement()
        {
            if (_movementCoroutine != null) return;
            _movementCoroutine = StartCoroutine(MovementLoop());
        }

        public void DisableMovement()
        {
            if (_movementCoroutine == null) return;
            StopCoroutine(_movementCoroutine);
            _movementCoroutine = null;
            CancelLinkTraversal();
            _navMesh?.Stop(_adapter);
            animationController.SetIsMoving(false);
        }

        // ── Loop principal ────────────────────────────────────────────────

        private IEnumerator MovementLoop()
        {
            while (true)
            {
                if (_adapter.IsReady())
                {
                    if (!_isTraversingLink && _navMesh.IsOnNavMeshLink(_adapter))
                        yield return StartCoroutine(TraverseLinkRoutine());
                    else if (!_isTraversingLink)
                        Tick();
                }

                yield return null;
            }
        }

        private void Tick()
        {
            Vector2 input = _inputReader.MoveInput;

            if (input.sqrMagnitude < 0.01f)
            {
                _navMesh.Stop(_adapter);
                animationController.SetIsMoving(false);
                return;
            }

            Vector3 direction = new Vector3(input.x, 0f, input.y).normalized;
            animationController.SetIsMoving(true);
            _navMesh.SetDestination(_adapter, transform.position + direction * _projectionDistance);
            

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                _rotationSpeed * Time.deltaTime);
        }

        // ── NavMesh Link ──────────────────────────────────────────────────

        private IEnumerator TraverseLinkRoutine()
        {
            _isTraversingLink = true;
            _linkCts = new CancellationTokenSource();

            var operation = _navMesh.TraverseNavMeshLinkAsync(
                _adapter, _speed, _rotationSpeed, _linkCts.Token);

            yield return new WaitUntil(() => operation.IsCompleted);

            _isTraversingLink = false;
            _linkCts?.Dispose();
            _linkCts = null;
        }

        private void CancelLinkTraversal()
        {
            if (_linkCts == null) return;
            _linkCts.Cancel();
            _linkCts.Dispose();
            _linkCts = null;
            _isTraversingLink = false;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_navMesh == null || _adapter == null || !Application.isPlaying) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_navMesh.GetCurrentDestination(_adapter), 0.15f);

            Vector3[] corners = _navMesh.GetPathCorners(_adapter);
            if (corners == null || corners.Length < 2) return;

            Gizmos.color = Color.yellow;
            for (int i = 0; i < corners.Length - 1; i++)
                Gizmos.DrawLine(corners[i], corners[i + 1]);

            if (_isTraversingLink)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.3f);
            }
        }
#endif
    }
}
