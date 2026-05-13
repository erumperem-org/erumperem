using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Services.Navigation
{
    /// <summary>
    /// Implementação de <see cref="INavMeshService"/> baseada em MonoBehaviour.
    /// Usa Coroutines para loops de polling e Task/CancellationToken para operações assíncronas.
    ///
    /// Adicione este componente ao mesmo GameObject que possui o NavMeshAgent,
    /// ou injete-o via construtor passando um MonoBehaviour host para Coroutines.
    /// </summary>
    public sealed class NavMeshService : MonoBehaviour, INavMeshService
    {
        // ─────────────────────────────────────────────────────────────
        // Estado interno
        // ─────────────────────────────────────────────────────────────

        private NavMeshAgent _agent;

        // Snapshot das configurações originais para ResetAgent()
        private float _defaultSpeed;
        private float _defaultAngularSpeed;
        private float _defaultAcceleration;
        private float _defaultStoppingDistance;
        private bool  _defaultAutoBraking;

        // Controle de Coroutines ativas
        private Coroutine _followCoroutine;

        // CancellationTokenSource interno para operações async (MoveToAsync, FollowTargetAsync…)
        private CancellationTokenSource _movementCts;

        // Flag de pausa para PauseNavigation / ResumeNavigation
        private bool _isPaused;
        private Vector3 _pausedDestination;

        // ─────────────────────────────────────────────────────────────
        // Inicialização
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();

            if (_agent == null)
            {
                Debug.LogError($"[NavMeshService] Nenhum NavMeshAgent encontrado em '{gameObject.name}'.");
                return;
            }

            TakeDefaultSnapshot();
        }

        private void TakeDefaultSnapshot()
        {
            _defaultSpeed           = _agent.speed;
            _defaultAngularSpeed    = _agent.angularSpeed;
            _defaultAcceleration    = _agent.acceleration;
            _defaultStoppingDistance = _agent.stoppingDistance;
            _defaultAutoBraking     = _agent.autoBraking;
        }

        // ─────────────────────────────────────────────────────────────
        // Movimentação básica
        // ─────────────────────────────────────────────────────────────

        public bool MoveTo(Vector3 destination)
        {
            if (!IsAgentReady()) return false;

            CancelMovement();
            _agent.isStopped = false;
            return _agent.SetDestination(destination);
        }

        public async Task<bool> MoveToAsync(
            Vector3 destination,
            float timeout = 30f,
            CancellationToken cancellationToken = default)
        {
            if (!IsAgentReady()) return false;

            CancelMovement();
            _movementCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (!MoveTo(destination)) return false;

            return await WaitUntilReachedAsync(timeout, _movementCts.Token);
        }

        public void FollowTarget(Transform target, float updateInterval = 0.15f)
        {
            if (!IsAgentReady() || target == null) return;

            StopFollowCoroutine();
            CancelMovement();
            _followCoroutine = StartCoroutine(FollowTargetCoroutine(target, updateInterval));
        }

        public async Task FollowTargetAsync(
            Transform target,
            float stopDistance = 1.5f,
            float updateInterval = 0.15f,
            CancellationToken cancellationToken = default)
        {
            if (!IsAgentReady() || target == null) return;

            CancelMovement();
            _movementCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var tcs = new TaskCompletionSource<bool>();

            _followCoroutine = StartCoroutine(
                FollowTargetUntilStopCoroutine(target, stopDistance, updateInterval, _movementCts.Token, tcs));

            await tcs.Task;
        }

        public void Stop()
        {
            if (!IsAgentReady()) return;

            StopFollowCoroutine();
            CancelMovement();

            _agent.isStopped = true;
            _isPaused = false;
        }

        public void Resume()
        {
            if (!IsAgentReady()) return;

            _agent.isStopped = false;
            _isPaused = false;
        }

        public void PauseNavigation()
        {
            if (!IsAgentReady() || _isPaused) return;

            _pausedDestination = _agent.destination;
            _agent.isStopped   = true;
            _isPaused          = true;
        }

        public void ResumeNavigation()
        {
            if (!IsAgentReady() || !_isPaused) return;

            _agent.isStopped = false;
            _agent.SetDestination(_pausedDestination);
            _isPaused = false;
        }

        public void CancelMovement()
        {
            StopFollowCoroutine();

            if (_movementCts != null)
            {
                _movementCts.Cancel();
                _movementCts.Dispose();
                _movementCts = null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Teleporte e posicionamento
        // ─────────────────────────────────────────────────────────────

        public bool Warp(Vector3 position)
        {
            if (!IsAgentReady()) return false;

            CancelMovement();
            return _agent.Warp(position);
        }

        public bool TeleportToNearestNavMeshPoint(Vector3 position, float maxDistance = 5f)
        {
            if (!SamplePosition(position, out var nearest, maxDistance)) return false;

            return Warp(nearest);
        }

        // ─────────────────────────────────────────────────────────────
        // Controle de destino e velocidade
        // ─────────────────────────────────────────────────────────────

        public bool SetDestination(Vector3 destination)
        {
            if (!IsAgentReady()) return false;

            _agent.isStopped = false;
            return _agent.SetDestination(destination);
        }

        public void SetVelocity(Vector3 velocity)
        {
            if (!IsAgentReady()) return;

            _agent.velocity = velocity;
        }

        public void SetSpeed(float speed)
        {
            if (!IsAgentReady()) return;

            _agent.speed = Mathf.Max(0f, speed);
        }

        public void SetAngularSpeed(float angularSpeed)
        {
            if (!IsAgentReady()) return;

            _agent.angularSpeed = Mathf.Max(0f, angularSpeed);
        }

        public void SetAcceleration(float acceleration)
        {
            if (!IsAgentReady()) return;

            _agent.acceleration = Mathf.Max(0f, acceleration);
        }

        public void SetStoppingDistance(float distance)
        {
            if (!IsAgentReady()) return;

            _agent.stoppingDistance = Mathf.Max(0f, distance);
        }

        // ─────────────────────────────────────────────────────────────
        // Caminhos
        // ─────────────────────────────────────────────────────────────

        public void ClearPath()
        {
            if (!IsAgentReady()) return;

            _agent.ResetPath();
        }

        public bool RecalculatePath()
        {
            if (!IsAgentReady() || !_agent.hasPath) return false;

            var destination = _agent.destination;
            _agent.ResetPath();
            return _agent.SetDestination(destination);
        }

        public NavMeshPath CalculatePath(Vector3 destination)
        {
            if (!IsAgentReady()) return null;

            var path = new NavMeshPath();
            _agent.CalculatePath(destination, path);

            return path.status == NavMeshPathStatus.PathInvalid ? null : path;
        }

        public float GetPathLength(NavMeshPath path)
        {
            if (path == null || path.corners.Length < 2) return 0f;

            var corners = path.corners;
            float total = 0f;

            for (int i = 1; i < corners.Length; i++)
                total += Vector3.Distance(corners[i - 1], corners[i]);

            return total;
        }

        public Vector3[] GetPathCorners()
        {
            if (!IsAgentReady() || !_agent.hasPath) return Array.Empty<Vector3>();

            return _agent.path.corners;
        }

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do caminho
        // ─────────────────────────────────────────────────────────────

        public bool HasPath()
            => IsAgentReady() && _agent.hasPath;

        public bool IsPathComplete()
            => IsAgentReady() && _agent.hasPath
            && _agent.path.status == NavMeshPathStatus.PathComplete;

        public bool IsPathPartial()
            => IsAgentReady() && _agent.hasPath
            && _agent.path.status == NavMeshPathStatus.PathPartial;

        public bool IsPathInvalid()
            => IsAgentReady() && _agent.hasPath
            && _agent.path.status == NavMeshPathStatus.PathInvalid;

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do agente
        // ─────────────────────────────────────────────────────────────

        public bool IsMoving()
            => IsAgentReady() && !_agent.isStopped
            && _agent.velocity.sqrMagnitude > 0.01f;

        public bool IsStopped()
            => !IsAgentReady() || _agent.isStopped
            || _agent.velocity.sqrMagnitude <= 0.01f;

        public bool IsPending()
            => IsAgentReady() && _agent.pathPending;

        public bool HasReachedDestination()
        {
            if (!IsAgentReady()) return false;
            if (_agent.pathPending)  return false;
            if (_agent.remainingDistance > _agent.stoppingDistance) return false;

            return !_agent.hasPath || _agent.velocity.sqrMagnitude <= 0.01f;
        }

        public bool IsOnNavMesh()
            => IsAgentReady() && _agent.isOnNavMesh;

        public float GetRemainingDistance()
            => IsAgentReady() ? _agent.remainingDistance : 0f;

        public Vector3 GetCurrentDestination()
            => IsAgentReady() ? _agent.destination : Vector3.zero;

        public Vector3 GetCurrentVelocity()
            => IsAgentReady() ? _agent.velocity : Vector3.zero;

        public Vector3 GetNormalizedDirection()
        {
            if (!IsAgentReady()) return Vector3.zero;

            var vel = _agent.velocity;
            return vel.sqrMagnitude > 0.001f ? vel.normalized : Vector3.zero;
        }

        // ─────────────────────────────────────────────────────────────
        // NavMesh queries
        // ─────────────────────────────────────────────────────────────

        public bool SamplePosition(Vector3 sourcePosition, out Vector3 result, float maxDistance, int areaMask = NavMesh.AllAreas)
        {
            result = sourcePosition;

            if (!NavMesh.SamplePosition(sourcePosition, out var hit, maxDistance, areaMask))
                return false;

            result = hit.position;
            return true;
        }

        public bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit)
        {
            // NavMesh.Raycast retorna true quando há obstrução; aqui invertemos para
            // "true = caminho limpo" para melhor legibilidade no consumidor.
            bool blocked = NavMesh.Raycast(sourcePosition, targetPosition, out hit, NavMesh.AllAreas);
            return !blocked;
        }

        public bool ValidateDestination(Vector3 destination, float sampleRadius = 1f)
            => SamplePosition(destination, out _, sampleRadius);

        public bool CanReach(Vector3 destination)
        {
            var path = CalculatePath(destination);
            return path != null && path.status == NavMeshPathStatus.PathComplete;
        }

        // ─────────────────────────────────────────────────────────────
        // Espera assíncrona
        // ─────────────────────────────────────────────────────────────

        public async Task<bool> WaitUntilReachedAsync(
            float timeout = 30f,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();

            StartCoroutine(WaitUntilReachedCoroutine(timeout, cancellationToken, tcs));

            return await tcs.Task;
        }

        public async Task<bool> WaitUntilStoppedAsync(
            float timeout = 10f,
            CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<bool>();

            StartCoroutine(WaitUntilStoppedCoroutine(timeout, cancellationToken, tcs));

            return await tcs.Task;
        }

        // ─────────────────────────────────────────────────────────────
        // Rotação
        // ─────────────────────────────────────────────────────────────

        public async Task FaceTargetAsync(
            Transform target,
            float rotationSpeed = 360f,
            CancellationToken cancellationToken = default)
        {
            if (target == null) return;

            var direction = (target.position - transform.position);
            direction.y = 0f;

            await FaceDirectionAsync(direction, rotationSpeed, cancellationToken);
        }

        public async Task FaceDirectionAsync(
            Vector3 direction,
            float rotationSpeed = 360f,
            CancellationToken cancellationToken = default)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            var tcs = new TaskCompletionSource<bool>();

            StartCoroutine(FaceDirectionCoroutine(direction.normalized, rotationSpeed, cancellationToken, tcs));

            await tcs.Task;
        }

        // ─────────────────────────────────────────────────────────────
        // Configurações de comportamento
        // ─────────────────────────────────────────────────────────────

        public void EnableAutoBraking(bool enabled)
        {
            if (IsAgentReady()) _agent.autoBraking = enabled;
        }

        public void EnableRotation(bool enabled)
        {
            if (IsAgentReady()) _agent.updateRotation = enabled;
        }

        public void EnablePositionUpdate(bool enabled)
        {
            if (IsAgentReady()) _agent.updatePosition = enabled;
        }

        public void EnableObstacleAvoidance(bool enabled)
        {
            if (!IsAgentReady()) return;

            _agent.obstacleAvoidanceType = enabled
                ? ObstacleAvoidanceType.LowQualityObstacleAvoidance
                : ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        public void SetAreaMask(int areaMask)
        {
            if (IsAgentReady()) _agent.areaMask = areaMask;
        }

        public void SetAvoidancePriority(int priority)
        {
            if (IsAgentReady()) _agent.avoidancePriority = Mathf.Clamp(priority, 0, 99);
        }

        // ─────────────────────────────────────────────────────────────
        // Ciclo de vida do agente
        // ─────────────────────────────────────────────────────────────

        public void EnableAgent()
        {
            if (_agent != null) _agent.enabled = true;
        }

        public void DisableAgent()
        {
            if (_agent == null) return;

            CancelMovement();
            _agent.enabled = false;
        }

        public void ResetAgent()
        {
            if (!IsAgentReady()) return;

            CancelMovement();
            _agent.ResetPath();
            _agent.isStopped       = false;
            _agent.velocity        = Vector3.zero;
            _isPaused              = false;

            _agent.speed           = _defaultSpeed;
            _agent.angularSpeed    = _defaultAngularSpeed;
            _agent.acceleration    = _defaultAcceleration;
            _agent.stoppingDistance = _defaultStoppingDistance;
            _agent.autoBraking     = _defaultAutoBraking;
        }

        // ─────────────────────────────────────────────────────────────
        // Coroutines internas
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Coroutine de follow simples: atualiza o destino do agente periodicamente
        /// enquanto o alvo estiver vivo. Interrompida por StopFollowCoroutine().
        /// </summary>
        private IEnumerator FollowTargetCoroutine(Transform target, float updateInterval)
        {
            var wait = new WaitForSeconds(updateInterval);

            while (target != null)
            {
                if (IsAgentReady())
                    _agent.SetDestination(target.position);

                yield return wait;
            }
        }

        /// <summary>
        /// Coroutine de follow assíncrono: atualiza o destino e resolve a TaskCompletionSource
        /// quando o agente chegar dentro de <paramref name="stopDistance"/> ou o token for cancelado.
        /// </summary>
        private IEnumerator FollowTargetUntilStopCoroutine(
            Transform target,
            float stopDistance,
            float updateInterval,
            CancellationToken cancellationToken,
            TaskCompletionSource<bool> tcs)
        {
            var wait = new WaitForSeconds(updateInterval);

            while (target != null && !cancellationToken.IsCancellationRequested)
            {
                if (IsAgentReady())
                {
                    float dist = Vector3.Distance(transform.position, target.position);

                    if (dist <= stopDistance)
                    {
                        Stop();
                        tcs.TrySetResult(true);
                        yield break;
                    }

                    _agent.SetDestination(target.position);
                }

                yield return wait;
            }

            // Target destruído ou cancelado
            if (IsAgentReady()) _agent.isStopped = true;
            tcs.TrySetResult(false);
        }

        /// <summary>
        /// Coroutine que aguarda o agente alcançar o destino com suporte a timeout e cancellation.
        /// </summary>
        private IEnumerator WaitUntilReachedCoroutine(
            float timeout,
            CancellationToken cancellationToken,
            TaskCompletionSource<bool> tcs)
        {
            float elapsed = 0f;

            while (elapsed < timeout && !cancellationToken.IsCancellationRequested)
            {
                if (HasReachedDestination())
                {
                    tcs.TrySetResult(true);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            tcs.TrySetResult(false);
        }

        /// <summary>
        /// Coroutine que aguarda o agente parar completamente.
        /// </summary>
        private IEnumerator WaitUntilStoppedCoroutine(
            float timeout,
            CancellationToken cancellationToken,
            TaskCompletionSource<bool> tcs)
        {
            float elapsed = 0f;

            while (elapsed < timeout && !cancellationToken.IsCancellationRequested)
            {
                if (IsStopped())
                {
                    tcs.TrySetResult(true);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            tcs.TrySetResult(false);
        }

        /// <summary>
        /// Coroutine de rotação suave em direção a um vetor normalizado no plano XZ.
        /// </summary>
        private IEnumerator FaceDirectionCoroutine(
            Vector3 targetDirection,
            float rotationSpeed,
            CancellationToken cancellationToken,
            TaskCompletionSource<bool> tcs)
        {
            if (targetDirection.sqrMagnitude < 0.001f)
            {
                tcs.TrySetResult(false);
                yield break;
            }

            var targetRotation = Quaternion.LookRotation(targetDirection);
            bool wasRotationEnabled = _agent.updateRotation;

            // Desabilita a rotação do agente para assumir o controle
            if (IsAgentReady()) _agent.updateRotation = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                float step = rotationSpeed * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, step);

                float angle = Quaternion.Angle(transform.rotation, targetRotation);

                if (angle < 0.5f)
                {
                    transform.rotation = targetRotation;
                    break;
                }

                yield return null;
            }

            if (IsAgentReady()) _agent.updateRotation = wasRotationEnabled;

            tcs.TrySetResult(!cancellationToken.IsCancellationRequested);
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers privados
        // ─────────────────────────────────────────────────────────────

        private bool IsAgentReady()
            => _agent != null && _agent.enabled && _agent.isOnNavMesh;

        private void StopFollowCoroutine()
        {
            if (_followCoroutine == null) return;

            StopCoroutine(_followCoroutine);
            _followCoroutine = null;
        }

        private void OnDestroy()
        {
            CancelMovement();
        }
    }
}
