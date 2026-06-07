using System.Collections;
using System.Threading;
using Services.Navigation;
using UnityEngine;

namespace Player
{
    public enum MovementMode { None, Player, Follow, WalkToPoint }

    [RequireComponent(typeof(NavMeshAgentAdapter))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float _speed = 5f;
        [SerializeField] private float _projectionDistance = 3f;
        [SerializeField] private float _stoppingDistance = 0.1f;
        [SerializeField] private float _acceleration = 5f;
        [SerializeField] private PlayableAnimationController animationController;

        [Header("Rotação")]
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Follow (Companion)")]
        [SerializeField] private float _followMinDistance = 2f;
        [SerializeField] private float _followStopDistance = 1.5f;
        [SerializeField] private float _directionChangeDotThreshold = 0.7f;

        [Header("Resting")]
        [SerializeField] private float _walkToPointTolerance = 0.3f;

        // ── Dependências ──────────────────────────────────────────────────

        [SerializeField] private PlayerInputReader _inputReader;
        private INavMeshService _navMesh;
        private NavMeshAgentAdapter _adapter;
        private Rigidbody _rb;

        // ── Estado interno ────────────────────────────────────────────────

        private MovementMode _mode = MovementMode.None;
        private Transform _followTarget;
        private Vector3 _restingDestination;
        private bool _destinationSet;
        private Vector3 _lastMoveDirection = Vector3.zero;

        private Coroutine _movementCoroutine;
        private CancellationTokenSource _linkCts;
        private bool _isTraversingLink;

        // ── Propriedades ──────────────────────────────────────────────────

        public bool IsMoving => _mode == MovementMode.Player
            ? _rb.linearVelocity.sqrMagnitude > 0.01f
            : _navMesh != null && _adapter != null && _navMesh.IsMoving(_adapter);

        public void SetService(INavMeshService service) => _navMesh = service;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            _adapter = GetComponent<NavMeshAgentAdapter>();
            _rb = GetComponent<Rigidbody>();
            _navMesh = new NavMeshService();

            // Rigidbody não deve rotacionar por física — só por código.
            _rb.freezeRotation = true;
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

        // ── API pública ───────────────────────────────────────────────────

        public void SetInputReader(PlayerInputReader inputReader) =>
            _inputReader = inputReader;

        public void EnableMovement()
        {
            SetMode(MovementMode.Player);
        }

        public void EnableFollow(Transform target)
        {
            bool targetChanged = target != _followTarget;
            _followTarget = target;
            if (targetChanged) _navMesh?.ClearPath(_adapter);
            SetMode(MovementMode.Follow);
        }

        public void EnableWalkToPoint(Vector3 destination)
        {
            _restingDestination = destination;
            SetMode(MovementMode.WalkToPoint);
        }

        public void DisableMovement()
        {
            StopMovementCoroutine();
            _mode = MovementMode.None;
            SetPhysicsMode(false);
            _navMesh?.Stop(_adapter);
            animationController?.SetIsMoving(false);
        }

        // ── Troca de modo ─────────────────────────────────────────────────

        private void SetMode(MovementMode mode)
        {
            StopMovementCoroutine();
            _destinationSet = false;
            _mode = mode;

            // Modo Player → física. Follow/WalkToPoint → NavMesh.
            bool isPhysics = mode == MovementMode.Player;
            SetPhysicsMode(isPhysics);

            _movementCoroutine = StartCoroutine(MovementLoop());
        }

        /// <summary>
        /// Liga/desliga Rigidbody e NavMeshAgent de forma mutuamente exclusiva.
        /// Ambos ativos ao mesmo tempo causam conflito de posição.
        /// </summary>
        private void SetPhysicsMode(bool physics)
        {
            _rb.isKinematic = !physics;
            //_adapter.SetEnabled(!physics);  // habilita NavMesh apenas fora do modo Player
        }

        // ── Loop principal ────────────────────────────────────────────────

        private IEnumerator MovementLoop()
        {
            while (true)
            {
                if (_mode == MovementMode.Player)
                {
                    // Física roda no FixedUpdate — fazemos tick ali.
                    yield return new WaitForFixedUpdate();
                    TickPlayer();
                }
                else if (_adapter.IsReady())
                {
                    if (!_isTraversingLink && _navMesh.IsOnNavMeshLink(_adapter))
                        yield return StartCoroutine(TraverseLinkRoutine());
                    else if (!_isTraversingLink)
                        TickMode();

                    yield return null;
                }
                else
                {
                    yield return null;
                }
            }
        }

        private void TickMode()
        {
            switch (_mode)
            {
                case MovementMode.Follow: TickFollow(); break;
                case MovementMode.WalkToPoint: TickWalkToPoint(); break;
            }
        }

        // ── Tick: Player (física) ─────────────────────────────────────────

        private void TickPlayer()
        {
            if (_inputReader == null) return;

            var input = _inputReader.MoveInput;

            if (input.sqrMagnitude < 0.01f)
            {
                _lastMoveDirection = Vector3.zero;
                _rb.linearVelocity = Vector3.zero;
                animationController?.SetIsMoving(false);
                return;
            }

            var direction = new Vector3(input.x, 0f, input.y).normalized;
            _lastMoveDirection = direction;

            // Move por física — idêntico ao MovimentoXZ mas sem Rigidbody.MovePosition
            // para não bypassar a física de colisão.
            _rb.linearVelocity = direction * _speed;

            animationController?.SetIsMoving(true);
            _rb.MoveRotation(Quaternion.LookRotation(direction, Vector3.up));
        }

        // ── Tick: Follow (NavMesh) ────────────────────────────────────────

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

        // ── Tick: WalkToPoint (NavMesh) ───────────────────────────────────

        private void TickWalkToPoint()
        {
            float threshold = _stoppingDistance + _walkToPointTolerance;
            float dist = Vector3.Distance(transform.position, _restingDestination);

            if (dist > threshold)
            {
                if (!_destinationSet)
                {
                    bool ok = _navMesh.MoveTo(_adapter, _restingDestination);
                    if (!ok && _navMesh.SamplePosition(_restingDestination, out var sampled, 2f))
                        _navMesh.MoveTo(_adapter, sampled);

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
                SetPhysicsMode(false);
            }
        }

        // ── NavMesh Link ──────────────────────────────────────────────────

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

        // ── Helpers ───────────────────────────────────────────────────────

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
            if (!Application.isPlaying) return;

            if (_mode == MovementMode.Player && _rb != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position, _rb.linearVelocity);
                return;
            }

            if (_navMesh == null || _adapter == null) return;

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