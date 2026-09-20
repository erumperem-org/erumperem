using System.Collections;
using System.Threading;
using Services.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Player
{
    public enum MovementMode { None, Player, Follow, WalkToPoint, ClickToMove }

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

        [Header("Movimento por clique")]
        [SerializeField, Min(1f)] private float _clickAcceleration = 60f;
        [SerializeField, Min(1f)] private float _clickRotationSharpness = 22f;

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

        public bool IsClickMoving => _mode == MovementMode.ClickToMove;
        public Vector3 ClickDestination => _adapter.Agent.destination;

        public bool TryMoveByClick(Vector3 destination, float sampleRadius = 1f)
        {
            if (!isActiveAndEnabled || (_mode != MovementMode.Player && !IsClickMoving)) return false;
            var agent = _adapter.Agent;
            if (agent == null) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            for (int i = 0; i < 32; i++) filter.SetAreaCost(i, agent.GetAreaCost(i));
            if (!NavMesh.SamplePosition(transform.position, out var start, 1f, filter)
                || !NavMesh.SamplePosition(destination, out var end, sampleRadius, filter)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete) return false;
            bool retargeting = IsClickMoving;
            float previousSpeed = retargeting ? agent.velocity.magnitude : 0f;
            if (!retargeting) SetMode(MovementMode.ClickToMove, start.position);
            if (!agent.isOnNavMesh || !agent.SetPath(path))
            {
                CancelClickMovement();
                return false;
            }
            agent.stoppingDistance = _stoppingDistance;
            agent.speed = _speed;
            agent.acceleration = _clickAcceleration;
            agent.autoBraking = true;
            agent.updateRotation = false;
            agent.isStopped = false;
            if (retargeting && previousSpeed > 0.1f && !agent.isOnOffMeshLink)
            {
                var corners = path.corners;
                for (int i = 1; i < corners.Length; i++)
                {
                    Vector3 direction = corners[i] - agent.nextPosition;
                    direction.y = 0f;
                    if (direction.sqrMagnitude < 0.0025f) continue;
                    agent.velocity = direction.normalized * Mathf.Min(previousSpeed, _speed);
                    break;
                }
            }
            return true;
        }

        public void CancelClickMovement()
        {
            if (!IsClickMoving) return;
            EnableMovement();
            animationController?.SetIsMoving(false);
        }

        private void TickClickMovement()
        {
            var agent = _adapter.Agent;
            if (_inputReader == null || !_inputReader.CanAcceptWorldInput || _inputReader.MoveInput.sqrMagnitude > 0.01f
                || (!agent.pathPending && (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete
                    || agent.remainingDistance <= _stoppingDistance + 0.05f)))
            {
                CancelClickMovement();
                return;
            }
            var direction = agent.desiredVelocity;
            direction.y = 0f;
            animationController?.SetIsMoving(agent.velocity.sqrMagnitude > 0.01f);
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-_clickRotationSharpness * Time.deltaTime));
        }

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            _adapter = GetComponent<NavMeshAgentAdapter>();
            _rb = GetComponent<Rigidbody>();
            _navMesh = new NavMeshService();

            if (animationController == null)
            {
                animationController = GetComponentInChildren<PlayableAnimationController>();
            }

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



        public void DisableMovement()
        {
            StopMovementCoroutine();
            _mode = MovementMode.None;
            SetMovementBackend(MovementMode.None);
            _navMesh?.Stop(_adapter);
            animationController?.SetIsMoving(false);
        }

        // ── Troca de modo ─────────────────────────────────────────────────
        public void EnableFollow(Transform target, Vector3? startPosition = null)
        {
            bool targetChanged = target != _followTarget;
            _followTarget = target;
            if (targetChanged) _navMesh?.ClearPath(_adapter);
            SetMode(MovementMode.Follow, startPosition);
        }

        public void EnableWalkToPoint(Vector3 destination, Vector3? startPosition = null)
        {
            _restingDestination = destination;
            SetMode(MovementMode.WalkToPoint, startPosition);
        }

        private void SetMode(MovementMode mode, Vector3? startPosition = null)
        {
            StopMovementCoroutine();
            _destinationSet = false;
            _mode = mode;
            SetMovementBackend(mode, startPosition);
            _movementCoroutine = StartCoroutine(MovementLoop());
        }

        private void SetMovementBackend(MovementMode mode, Vector3? startPosition = null)
        {
            var useRigidbodyPhysics = mode == MovementMode.Player;
            var useNavMeshAgent = mode is MovementMode.Follow or MovementMode.WalkToPoint or MovementMode.ClickToMove;

            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
            }
            _rb.isKinematic = !useRigidbodyPhysics;

            var navMeshAgent = _adapter.Agent;
            if (navMeshAgent == null) return;

            navMeshAgent.enabled = useNavMeshAgent;
            if (!useNavMeshAgent) return;
            navMeshAgent.acceleration = mode == MovementMode.ClickToMove ? _clickAcceleration : _acceleration;

            var sampleOrigin = startPosition ?? transform.position;
            if (NavMesh.SamplePosition(sampleOrigin, out var navMeshHit, 2f, NavMesh.AllAreas))
            {
                navMeshAgent.Warp(navMeshHit.position);
            }

            _navMesh?.ClearPath(_adapter);
        }
        // ── Loop principal ────────────────────────────────────────────────

        private IEnumerator MovementLoop()
        {
            yield return null;
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
                case MovementMode.ClickToMove: TickClickMovement(); break;
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

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            var smoothedRotation = Quaternion.Slerp(
                _rb.rotation,
                targetRotation,
                _rotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(smoothedRotation);
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
                SetMovementBackend(MovementMode.None);
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
