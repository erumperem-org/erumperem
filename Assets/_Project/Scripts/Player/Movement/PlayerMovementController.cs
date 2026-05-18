using UnityEngine;
using Services.Navigation;
using System.Collections;
using System.Threading;

namespace Player
{
    public enum MovementMode { None, Player, Follow, WalkToPoint }

    [RequireComponent(typeof(NavMeshAgentAdapter))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _projectionDistance = 3f;
        [SerializeField] private float _stoppingDistance = 0.1f;
        [SerializeField] private float _acceleration = 5f;
        [SerializeField] private PlayableAnimationController animationController;
        private Vector3 _lastMoveDirection = Vector3.zero;

        [Header("Rotação")]
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Follow (Companion)")]
        [Tooltip("Distância mínima do Main para o Companion começar a se mover.")]
        [SerializeField] private float _followMinDistance = 2f;
        [Tooltip("Distância em que o Companion para de se aproximar.")]
        [SerializeField] private float _followStopDistance = 1.5f;
        [SerializeField] private float _directionChangeDotThreshold = 0.7f;

        [Header("Resting")]
        [Tooltip("Margem extra além de stoppingDistance para considerar que chegou ao ponto.")]
        [SerializeField] private float _walkToPointTolerance = 0.3f;

        // ── Dependências injetadas ─────────────────────────────────────────

        [HideInInspector] public PlayerInputReader _inputReader;

        private INavMeshService _navMesh;
        private NavMeshAgentAdapter _adapter;

        // ── Estado interno ─────────────────────────────────────────────────

        private MovementMode _mode = MovementMode.None;
        private Transform _followTarget;
        private Vector3 _restingDestination;
        private bool _destinationSet;

        private Coroutine _movementCoroutine;
        private CancellationTokenSource _linkCts;
        private bool _isTraversingLink;

        // ── Propriedades ───────────────────────────────────────────────────

        public void SetService(INavMeshService service) => _navMesh = service;
        public bool IsMoving => _navMesh != null && _adapter != null && _navMesh.IsMoving(_adapter);

        // ── Unity lifecycle ────────────────────────────────────────────────

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
            _navMesh.SetAcceleration(_adapter, _acceleration);
            _navMesh.EnableAutoBraking(_adapter, true);
            _navMesh.SetSpeed(_adapter, _speed);
            _navMesh.SetStoppingDistance(_adapter, _stoppingDistance);
            _adapter.SetUpdateRotation(false);
        }

        private void OnDisable() => CancelLinkTraversal();

        // ── API pública — usada pelo PlayableCharacterStatesBuilder ────────

        /// <summary>Main: controlado pelo input do jogador.</summary>
        public void EnableMovement()
        {
            SetMode(MovementMode.Player);
        }

        /// <summary>Companion: segue o Main via NavMesh.</summary>
        public void EnableFollow(Transform target)
        {
            bool targetChanged = target != _followTarget;
            _followTarget = target;

            if (targetChanged)
                _navMesh?.ClearPath(_adapter); // limpa rota antiga ao trocar de target

            SetMode(MovementMode.Follow);
        }

        /// <summary>Resting: caminha até uma posição fixa e para.</summary>
        public void EnableWalkToPoint(Vector3 destination)
        {
            _restingDestination = destination;
            SetMode(MovementMode.WalkToPoint);
        }

        /// <summary>Para qualquer movimento (usado internamente ou para estados sem movimento).</summary>
        public void DisableMovement()
        {
            StopMovementCoroutine();
            _mode = MovementMode.None;
            _navMesh?.Stop(_adapter);
            animationController?.SetIsMoving(false);
        }

        // ── Troca de modo ──────────────────────────────────────────────────

        private void SetMode(MovementMode mode)
        {
            StopMovementCoroutine();
            _destinationSet = false; // reseta flag ao trocar de modo
            _mode = mode;
            _movementCoroutine = StartCoroutine(MovementLoop());
        }

        // ── Loop principal ─────────────────────────────────────────────────

        private IEnumerator MovementLoop()
        {
            while (true)
            {
                if (_adapter.IsReady())
                {
                    if (!_isTraversingLink && _navMesh.IsOnNavMeshLink(_adapter))
                        yield return StartCoroutine(TraverseLinkRoutine());
                    else if (!_isTraversingLink)
                        TickMode();
                }

                yield return null;
            }
        }

        private void TickMode()
        {
            switch (_mode)
            {
                case MovementMode.Player: TickPlayer(); break;
                case MovementMode.Follow: TickFollow(); break;
                case MovementMode.WalkToPoint: TickWalkToPoint(); break;
            }
        }

        // ── Tick: Player (Main) ────────────────────────────────────────────

        private void TickPlayer()
        {
            if (_inputReader == null) return;

            var input = _inputReader.MoveInput;

            if (input.sqrMagnitude < 0.01f)
            {
                _lastMoveDirection = Vector3.zero;
                _navMesh.Stop(_adapter);
                _navMesh.ClearPath(_adapter);
                animationController?.SetIsMoving(false);
                return;
            }

            var direction = new Vector3(input.x, 0f, input.y).normalized;

            // Zera velocidade se a direção mudou bruscamente (produto escalar < threshold)
            if (_lastMoveDirection.sqrMagnitude > 0.001f &&
                Vector3.Dot(direction, _lastMoveDirection) < _directionChangeDotThreshold)
            {
                _navMesh.SetVelocity(_adapter, Vector3.zero);
            }

            _lastMoveDirection = direction;

            _navMesh.SetDestination(_adapter, transform.position + direction * _projectionDistance);
            animationController?.SetIsMoving(true);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                _rotationSpeed * Time.deltaTime);
        }
        // ── Tick: Follow (Companion) ───────────────────────────────────────

        private void TickFollow()
        {
            if (_followTarget == null)
            {
                _navMesh.Stop(_adapter);
                animationController?.SetIsMoving(false);
                return;
            }

            float dist = Vector3.Distance(transform.position, _followTarget.position);

            if (dist > _followMinDistance)
            {
                _navMesh.SetStoppingDistance(_adapter, _followStopDistance);
                _navMesh.SetDestination(_adapter, _followTarget.position);
                animationController?.SetIsMoving(true);

                Vector3 dir = (_followTarget.position - transform.position).normalized;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir, Vector3.up),
                        _rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                _navMesh.Stop(_adapter);
                animationController?.SetIsMoving(false);
            }
        }

        // ── Tick: WalkToPoint (Resting) ────────────────────────────────────

        private void TickWalkToPoint()
        {
            float threshold = _stoppingDistance + _walkToPointTolerance;
            float dist = Vector3.Distance(transform.position, _restingDestination);

            if (dist > threshold)
            {
                if (!_destinationSet)
                {
                    // MoveTo em vez de SetDestination: garante isStopped=false + limpa follow coroutine
                    bool ok = _navMesh.MoveTo(_adapter, _restingDestination);

                    if (!ok)
                    {
                        // Ponto fora da NavMesh — valida e busca o mais próximo
                        if (_navMesh.SamplePosition(_restingDestination, out var sampled, 2f))
                            _navMesh.MoveTo(_adapter, sampled);
                    }

                    _navMesh.SetStoppingDistance(_adapter, _stoppingDistance);
                    _destinationSet = true;
                }

                animationController?.SetIsMoving(true);

                Vector3 dir = (_restingDestination - transform.position).normalized;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir, Vector3.up),
                        _rotationSpeed * Time.deltaTime);
                }
            }
            else
            {
                _navMesh.Stop(_adapter);
                animationController?.SetIsMoving(false);
                StopMovementCoroutine();
                _mode = MovementMode.None;
            }
        }

        // ── NavMesh Link ───────────────────────────────────────────────────

        private IEnumerator TraverseLinkRoutine()
        {
            _isTraversingLink = true;
            _linkCts = new CancellationTokenSource();

            var op = _navMesh.TraverseNavMeshLinkAsync(_adapter, _speed, _rotationSpeed, _linkCts.Token);
            yield return new WaitUntil(() => op.IsCompleted);

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

        // ── Helpers ────────────────────────────────────────────────────────

        private void StopMovementCoroutine()
        {
            if (_movementCoroutine == null) return;
            StopCoroutine(_movementCoroutine);
            _movementCoroutine = null;
            CancelLinkTraversal();
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_navMesh == null || _adapter == null || !Application.isPlaying) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_navMesh.GetCurrentDestination(_adapter), 0.15f);

            var corners = _navMesh.GetPathCorners(_adapter);
            if (corners != null && corners.Length >= 2)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < corners.Length - 1; i++)
                    Gizmos.DrawLine(corners[i], corners[i + 1]);
            }

            if (_mode == MovementMode.WalkToPoint)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(_restingDestination, 0.2f);
            }

            if (_mode == MovementMode.Follow && _followTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, _followTarget.position);
            }
        }
#endif
    }
}