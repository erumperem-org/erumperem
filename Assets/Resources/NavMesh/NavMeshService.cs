using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AI;

namespace Services.Navigation
{
    /// <summary>
    /// Implementação stateless de <see cref="INavMeshService"/>.
    ///
    /// Uma única instância deste serviço opera sobre N agentes via
    /// <see cref="NavMeshAgentAdapter"/>. O estado por-agente que precisa
    /// persistir entre frames (pausa, coroutines de follow) é mantido em
    /// <see cref="AgentState"/>, indexado pelo adapter — sem vazar para o
    /// caller nem para outros agentes.
    ///
    /// Cada operação assíncrona retorna um <see cref="NavMeshOperation"/>:
    /// o caller possui o handle e decide quando cancelar. Dois callers podem
    /// acionar o mesmo agente em paralelo sem que um destrua silenciosamente
    /// a operação do outro.
    ///
    /// Coroutines são hospedadas no MonoBehaviour <see cref="NavMeshCoroutineHost"/>,
    /// criado automaticamente e mantido vivo via DontDestroyOnLoad.
    /// </summary>
    public sealed class NavMeshService : INavMeshService
    {
        // ─────────────────────────────────────────────────────────────
        // Estado por agente (isolado, nunca exposto ao caller)
        // ─────────────────────────────────────────────────────────────

        private sealed class AgentState
        {
            public bool IsPaused;
            public Vector3 PausedDestination;
            public Coroutine FollowCoroutine;
        }

        private readonly Dictionary<NavMeshAgentAdapter, AgentState> _states
            = new Dictionary<NavMeshAgentAdapter, AgentState>();

        // ─────────────────────────────────────────────────────────────
        // Host de Coroutines
        // ─────────────────────────────────────────────────────────────

        private readonly NavMeshCoroutineHost _host;

        // ─────────────────────────────────────────────────────────────
        // Construção
        // ─────────────────────────────────────────────────────────────

        public NavMeshService()
        {
            _host = NavMeshCoroutineHost.GetOrCreate();
        }

        // ─────────────────────────────────────────────────────────────
        // Movimentação básica
        // ─────────────────────────────────────────────────────────────

        public bool MoveTo(NavMeshAgentAdapter adapter, Vector3 destination)
        {
            if (!IsReady(adapter)) return false;

            StopFollowCoroutine(adapter);
            adapter.Agent.isStopped = false;
            return adapter.Agent.SetDestination(destination);
        }

        public NavMeshOperation MoveToAsync(NavMeshAgentAdapter adapter, Vector3 destination,
            float timeout = 30f, CancellationToken cancellationToken = default)
        {
            var op = new NavMeshOperation(cancellationToken);

            if (!IsReady(adapter) || !MoveTo(adapter, destination))
            {
                op.Complete(false);
                return op;
            }

            _host.Run(WaitUntilReachedCoroutine(adapter, timeout, op));
            return op;
        }

        public NavMeshOperation FollowTargetAsync(NavMeshAgentAdapter adapter, Transform target,
            float stopDistance = 1.5f, float updateInterval = 0.15f,
            CancellationToken cancellationToken = default)
        {
            var op = new NavMeshOperation(cancellationToken);

            if (!IsReady(adapter) || target == null)
            {
                op.Complete(false);
                return op;
            }

            StopFollowCoroutine(adapter);
            var state = GetOrCreateState(adapter);
            state.FollowCoroutine = _host.Run(
                FollowTargetUntilStopCoroutine(adapter, target, stopDistance, updateInterval, op));

            return op;
        }

        public void Stop(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return;

            StopFollowCoroutine(adapter);
            adapter.Agent.isStopped = true;
            GetOrCreateState(adapter).IsPaused = false;
        }

        public void Resume(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return;

            adapter.Agent.isStopped = false;
            GetOrCreateState(adapter).IsPaused = false;
        }

        public void PauseNavigation(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return;

            var state = GetOrCreateState(adapter);
            if (state.IsPaused) return;

            state.PausedDestination = adapter.Agent.destination;
            adapter.Agent.isStopped = true;
            state.IsPaused = true;
        }

        public void ResumeNavigation(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return;

            var state = GetOrCreateState(adapter);
            if (!state.IsPaused) return;

            adapter.Agent.isStopped = false;
            adapter.Agent.SetDestination(state.PausedDestination);
            state.IsPaused = false;
        }

        // ─────────────────────────────────────────────────────────────
        // Teleporte e posicionamento
        // ─────────────────────────────────────────────────────────────

        public bool Warp(NavMeshAgentAdapter adapter, Vector3 position)
        {
            if (!IsReady(adapter)) return false;

            StopFollowCoroutine(adapter);
            return adapter.Agent.Warp(position);
        }

        public bool TeleportToNearestNavMeshPoint(NavMeshAgentAdapter adapter, Vector3 position,
            float maxDistance = 5f)
        {
            if (!SamplePosition(position, out var nearest, maxDistance)) return false;

            return Warp(adapter, nearest);
        }

        // ─────────────────────────────────────────────────────────────
        // Controle de destino e velocidade
        // ─────────────────────────────────────────────────────────────

        public bool SetDestination(NavMeshAgentAdapter adapter, Vector3 destination)
        {
            if (!IsReady(adapter)) return false;

            adapter.Agent.isStopped = false;
            return adapter.Agent.SetDestination(destination);
        }

        public void SetVelocity(NavMeshAgentAdapter adapter, Vector3 velocity)
        {
            if (IsReady(adapter)) adapter.Agent.velocity = velocity;
        }

        public void SetSpeed(NavMeshAgentAdapter adapter, float speed)
        {
            if (IsReady(adapter)) adapter.Agent.speed = Mathf.Max(0f, speed);
        }

        public void SetAngularSpeed(NavMeshAgentAdapter adapter, float angularSpeed)
        {
            if (IsReady(adapter)) adapter.Agent.angularSpeed = Mathf.Max(0f, angularSpeed);
        }

        public void SetAcceleration(NavMeshAgentAdapter adapter, float acceleration)
        {
            if (IsReady(adapter)) adapter.Agent.acceleration = Mathf.Max(0f, acceleration);
        }

        public void SetStoppingDistance(NavMeshAgentAdapter adapter, float distance)
        {
            if (IsReady(adapter)) adapter.Agent.stoppingDistance = Mathf.Max(0f, distance);
        }

        // ─────────────────────────────────────────────────────────────
        // Caminhos
        // ─────────────────────────────────────────────────────────────

        public void ClearPath(NavMeshAgentAdapter adapter)
        {
            if (IsReady(adapter)) adapter.Agent.ResetPath();
        }

        public bool RecalculatePath(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter) || !adapter.Agent.hasPath) return false;

            var destination = adapter.Agent.destination;
            adapter.Agent.ResetPath();
            return adapter.Agent.SetDestination(destination);
        }

        public NavMeshPath CalculatePath(NavMeshAgentAdapter adapter, Vector3 destination)
        {
            if (!IsReady(adapter)) return null;

            var path = new NavMeshPath();
            adapter.Agent.CalculatePath(destination, path);

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

        public Vector3[] GetPathCorners(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter) || !adapter.Agent.hasPath) return Array.Empty<Vector3>();

            return adapter.Agent.path.corners;
        }

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do caminho
        // ─────────────────────────────────────────────────────────────

        public bool HasPath(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.hasPath;

        public bool IsPathComplete(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.hasPath
            && adapter.Agent.path.status == NavMeshPathStatus.PathComplete;

        public bool IsPathPartial(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.hasPath
            && adapter.Agent.path.status == NavMeshPathStatus.PathPartial;

        public bool IsPathInvalid(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.hasPath
            && adapter.Agent.path.status == NavMeshPathStatus.PathInvalid;

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do agente
        // ─────────────────────────────────────────────────────────────

        public bool IsMoving(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && !adapter.Agent.isStopped
            && adapter.Agent.velocity.sqrMagnitude > 0.01f;

        public bool IsStopped(NavMeshAgentAdapter adapter)
            => !IsReady(adapter) || adapter.Agent.isStopped
            || adapter.Agent.velocity.sqrMagnitude <= 0.01f;

        public bool IsPending(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.pathPending;

        public bool HasReachedDestination(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return false;
            if (adapter.Agent.pathPending) return false;
            if (adapter.Agent.remainingDistance > adapter.Agent.stoppingDistance) return false;

            return !adapter.Agent.hasPath || adapter.Agent.velocity.sqrMagnitude <= 0.01f;
        }

        public bool IsOnNavMesh(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.isOnNavMesh;

        public float GetRemainingDistance(NavMeshAgentAdapter adapter)
            => IsReady(adapter) ? adapter.Agent.remainingDistance : 0f;

        public Vector3 GetCurrentDestination(NavMeshAgentAdapter adapter)
            => IsReady(adapter) ? adapter.Agent.destination : Vector3.zero;

        public Vector3 GetCurrentVelocity(NavMeshAgentAdapter adapter)
            => IsReady(adapter) ? adapter.Agent.velocity : Vector3.zero;

        public Vector3 GetNormalizedDirection(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return Vector3.zero;

            var vel = adapter.Agent.velocity;
            return vel.sqrMagnitude > 0.001f ? vel.normalized : Vector3.zero;
        }

        // ─────────────────────────────────────────────────────────────
        // NavMesh queries (sem agente específico)
        // ─────────────────────────────────────────────────────────────

        public bool SamplePosition(Vector3 sourcePosition, out Vector3 result,
            float maxDistance, int areaMask = NavMesh.AllAreas)
        {
            result = sourcePosition;

            if (!NavMesh.SamplePosition(sourcePosition, out var hit, maxDistance, areaMask))
                return false;

            result = hit.position;
            return true;
        }

        public bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit)
        {
            // NavMesh.Raycast retorna true quando há obstrução — invertido aqui para
            // "true = caminho limpo", mais legível no consumidor.
            bool blocked = NavMesh.Raycast(sourcePosition, targetPosition, out hit, NavMesh.AllAreas);
            return !blocked;
        }

        public bool ValidateDestination(Vector3 destination, float sampleRadius = 1f)
            => SamplePosition(destination, out _, sampleRadius);

        public bool CanReach(NavMeshAgentAdapter adapter, Vector3 destination)
        {
            var path = CalculatePath(adapter, destination);
            return path != null && path.status == NavMeshPathStatus.PathComplete;
        }

        // ─────────────────────────────────────────────────────────────
        // Espera assíncrona
        // ─────────────────────────────────────────────────────────────

        public NavMeshOperation WaitUntilReachedAsync(NavMeshAgentAdapter adapter,
            float timeout = 30f, CancellationToken cancellationToken = default)
        {
            var op = new NavMeshOperation(cancellationToken);
            _host.Run(WaitUntilReachedCoroutine(adapter, timeout, op));
            return op;
        }

        public NavMeshOperation WaitUntilStoppedAsync(NavMeshAgentAdapter adapter,
            float timeout = 10f, CancellationToken cancellationToken = default)
        {
            var op = new NavMeshOperation(cancellationToken);
            _host.Run(WaitUntilStoppedCoroutine(adapter, timeout, op));
            return op;
        }

        // ─────────────────────────────────────────────────────────────
        // Rotação
        // ─────────────────────────────────────────────────────────────

        public NavMeshOperation FaceTargetAsync(NavMeshAgentAdapter adapter, Transform target,
            float rotationSpeed = 360f, CancellationToken cancellationToken = default)
        {
            if (target == null)
            {
                var noop = new NavMeshOperation(cancellationToken);
                noop.Complete(false);
                return noop;
            }

            var direction = target.position - adapter.transform.position;
            direction.y = 0f;

            return FaceDirectionAsync(adapter, direction, rotationSpeed, cancellationToken);
        }

        public NavMeshOperation FaceDirectionAsync(NavMeshAgentAdapter adapter, Vector3 direction,
            float rotationSpeed = 360f, CancellationToken cancellationToken = default)
        {
            var op = new NavMeshOperation(cancellationToken);

            if (direction.sqrMagnitude < 0.001f)
            {
                op.Complete(false);
                return op;
            }

            _host.Run(FaceDirectionCoroutine(adapter, direction.normalized, rotationSpeed, op));
            return op;
        }

        // ─────────────────────────────────────────────────────────────
        // Configurações de comportamento
        // ─────────────────────────────────────────────────────────────

        public void EnableAutoBraking(NavMeshAgentAdapter adapter, bool enabled)
        {
            if (IsReady(adapter)) adapter.Agent.autoBraking = enabled;
        }

        public void EnableRotation(NavMeshAgentAdapter adapter, bool enabled)
        {
            if (IsReady(adapter)) adapter.Agent.updateRotation = enabled;
        }

        public void EnablePositionUpdate(NavMeshAgentAdapter adapter, bool enabled)
        {
            if (IsReady(adapter)) adapter.Agent.updatePosition = enabled;
        }

        public void EnableObstacleAvoidance(NavMeshAgentAdapter adapter, bool enabled)
        {
            if (!IsReady(adapter)) return;

            adapter.Agent.obstacleAvoidanceType = enabled
                ? ObstacleAvoidanceType.LowQualityObstacleAvoidance
                : ObstacleAvoidanceType.NoObstacleAvoidance;
        }

        public void SetAreaMask(NavMeshAgentAdapter adapter, int areaMask)
        {
            if (IsReady(adapter)) adapter.Agent.areaMask = areaMask;
        }

        public void SetAvoidancePriority(NavMeshAgentAdapter adapter, int priority)
        {
            if (IsReady(adapter)) adapter.Agent.avoidancePriority = Mathf.Clamp(priority, 0, 99);
        }

        // ─────────────────────────────────────────────────────────────
        // Ciclo de vida do agente
        // ─────────────────────────────────────────────────────────────

        public void EnableAgent(NavMeshAgentAdapter adapter)
        {
            if (adapter?.Agent != null) adapter.Agent.enabled = true;
        }

        public void DisableAgent(NavMeshAgentAdapter adapter)
        {
            if (adapter?.Agent == null) return;

            StopFollowCoroutine(adapter);
            adapter.Agent.enabled = false;
        }

        public void ResetAgent(NavMeshAgentAdapter adapter)
        {
            if (!IsReady(adapter)) return;

            StopFollowCoroutine(adapter);

            var agent = adapter.Agent;
            agent.ResetPath();
            agent.isStopped = false;
            agent.velocity = Vector3.zero;
            agent.speed = adapter.DefaultSpeed;
            agent.angularSpeed = adapter.DefaultAngularSpeed;
            agent.acceleration = adapter.DefaultAcceleration;
            agent.stoppingDistance = adapter.DefaultStoppingDistance;
            agent.autoBraking = adapter.DefaultAutoBraking;

            if (_states.TryGetValue(adapter, out var state))
                state.IsPaused = false;
        }

        // ─────────────────────────────────────────────────────────────
        // Coroutines internas
        // ─────────────────────────────────────────────────────────────

        private IEnumerator FollowTargetUntilStopCoroutine(
            NavMeshAgentAdapter adapter,
            Transform target,
            float stopDistance,
            float updateInterval,
            NavMeshOperation op)
        {
            var wait = new WaitForSeconds(updateInterval);

            while (target != null && !op.Token.IsCancellationRequested)
            {
                if (IsReady(adapter))
                {
                    float dist = Vector3.Distance(adapter.transform.position, target.position);

                    if (dist <= stopDistance)
                    {
                        adapter.Agent.isStopped = true;
                        op.Complete(true);
                        yield break;
                    }

                    adapter.Agent.SetDestination(target.position);
                }

                yield return wait;
            }

            if (IsReady(adapter)) adapter.Agent.isStopped = true;
            op.Complete(false);
        }

        private IEnumerator WaitUntilReachedCoroutine(
            NavMeshAgentAdapter adapter,
            float timeout,
            NavMeshOperation op)
        {
            float elapsed = 0f;

            while (elapsed < timeout && !op.Token.IsCancellationRequested)
            {
                if (HasReachedDestination(adapter))
                {
                    op.Complete(true);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            op.Complete(false);
        }

        private IEnumerator WaitUntilStoppedCoroutine(
            NavMeshAgentAdapter adapter,
            float timeout,
            NavMeshOperation op)
        {
            float elapsed = 0f;

            while (elapsed < timeout && !op.Token.IsCancellationRequested)
            {
                if (IsStopped(adapter))
                {
                    op.Complete(true);
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            op.Complete(false);
        }

        private IEnumerator FaceDirectionCoroutine(
            NavMeshAgentAdapter adapter,
            Vector3 targetDirection,
            float rotationSpeed,
            NavMeshOperation op)
        {
            var targetRotation = Quaternion.LookRotation(targetDirection);
            bool wasRotationEnabled = IsReady(adapter) && adapter.Agent.updateRotation;

            if (IsReady(adapter)) adapter.Agent.updateRotation = false;

            while (!op.Token.IsCancellationRequested)
            {
                float step = rotationSpeed * Time.deltaTime;
                adapter.transform.rotation = Quaternion.RotateTowards(
                    adapter.transform.rotation, targetRotation, step);

                if (Quaternion.Angle(adapter.transform.rotation, targetRotation) < 0.5f)
                {
                    adapter.transform.rotation = targetRotation;
                    break;
                }

                yield return null;
            }

            if (IsReady(adapter)) adapter.Agent.updateRotation = wasRotationEnabled;

            op.Complete(!op.Token.IsCancellationRequested);
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers privados
        // ─────────────────────────────────────────────────────────────

        private static bool IsReady(NavMeshAgentAdapter adapter)
            => adapter != null && adapter.IsReady();

        private AgentState GetOrCreateState(NavMeshAgentAdapter adapter)
        {
            if (!_states.TryGetValue(adapter, out var state))
            {
                state = new AgentState();
                _states[adapter] = state;
            }

            return state;
        }

        private void StopFollowCoroutine(NavMeshAgentAdapter adapter)
        {
            if (!_states.TryGetValue(adapter, out var state)) return;
            if (state.FollowCoroutine == null) return;

            _host.Stop(state.FollowCoroutine);
            state.FollowCoroutine = null;
        }

        // ── NavMesh Links ──────────────────────────────────────────────────────────

        public bool IsOnNavMeshLink(NavMeshAgentAdapter adapter)
            => IsReady(adapter) && adapter.Agent.isOnOffMeshLink;

        public NavMeshOperation TraverseNavMeshLinkAsync(
            NavMeshAgentAdapter adapter,
            float speed,
            float rotationSpeed,
            CancellationToken cancellationToken = default)
        {
            var op = new NavMeshOperation(cancellationToken);

            if (!IsReady(adapter) || !adapter.Agent.isOnOffMeshLink)
            {
                op.Complete(false);
                return op;
            }

            _host.Run(TraverseLinkCoroutine(adapter, speed, rotationSpeed, op));
            return op;
        }

        private IEnumerator TraverseLinkCoroutine(
            NavMeshAgentAdapter adapter,
            float speed,
            float rotationSpeed,
            NavMeshOperation op)
        {
            var agent = adapter.Agent;

            // Congela o agente: a posição será controlada manualmente via transform
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;

            var link = agent.currentOffMeshLinkData;
            var start = adapter.transform.position;
            var end = link.endPos;

            // Garante que end esteja sobre a NavMesh
            if (NavMesh.SamplePosition(end, out var hit, 1f, NavMesh.AllAreas))
                end = hit.position;

            float distance = Vector3.Distance(start, end);
            float duration = distance / Mathf.Max(speed, 0.01f);
            float elapsed = 0f;

            var direction = (end - start).normalized;
            var targetRotation = direction.sqrMagnitude > 0.001f
                                       ? Quaternion.LookRotation(direction, Vector3.up)
                                       : adapter.transform.rotation;

            while (elapsed < duration && !op.Token.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Posição: lerp linear entre start e end
                adapter.transform.position = Vector3.Lerp(start, end, t);

                // Rotação: slerp suave em direção ao destino do link
                adapter.transform.rotation = Quaternion.Slerp(
                    adapter.transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);

                yield return null;
            }

            // Snap final para garantir posição exata
            adapter.transform.position = end;
            adapter.transform.rotation = targetRotation;

            // Sinaliza ao NavMeshAgent que o link foi concluído
            agent.CompleteOffMeshLink();

            // Restaura controle normal
            agent.updatePosition = true;
            agent.updateRotation = false; // mantém false — PlayerMovementController controla rotação
            agent.isStopped = false;

            // Sincroniza o agent com a posição final do transform
            agent.nextPosition = end;

            op.Complete(!op.Token.IsCancellationRequested);
        }
    }
}
