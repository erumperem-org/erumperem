using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Services.Navigation
{
    /// <summary>
    /// Camada de serviço stateless para operações de navegação via NavMeshAgent.
    ///
    /// Diferente da versão anterior (MonoBehaviour acoplado 1:1 ao agente),
    /// esta interface opera sobre um <see cref="NavMeshAgentAdapter"/> passado
    /// como parâmetro — permitindo uma única instância de serviço para N agentes,
    /// eliminando O(n) componentes e o estado compartilhado que causava corrida.
    ///
    /// Operações assíncronas retornam um <see cref="NavMeshOperation"/>, que é o
    /// handle exclusivo do caller: cancelar ou descartar um handle não interfere
    /// nas operações de outros callers sobre o mesmo agente.
    /// </summary>
    public interface INavMeshService
    {
        // ─────────────────────────────────────────────────────────────
        // Movimentação básica
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Define um destino para o agente navegar imediatamente.
        /// Retorna false se o destino for inválido ou o agente estiver desabilitado.
        /// </summary>
        bool MoveTo(NavMeshAgentAdapter adapter, Vector3 destination);

        /// <summary>
        /// Move o agente até o destino de forma assíncrona.
        /// Retorna um <see cref="NavMeshOperation"/> cujo <c>Task</c> resolve com
        /// <c>true</c> ao chegar, <c>false</c> se cancelado ou timeout.
        /// </summary>
        NavMeshOperation MoveToAsync(NavMeshAgentAdapter adapter, Vector3 destination,
            float timeout = 30f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Faz o agente seguir continuamente um alvo.
        /// O destino é atualizado a cada <paramref name="updateInterval"/> segundos.
        /// Interrompa chamando <see cref="Stop"/> ou descartando o handle retornado.
        /// </summary>
        NavMeshOperation FollowTargetAsync(NavMeshAgentAdapter adapter, Transform target,
            float stopDistance = 1.5f, float updateInterval = 0.15f,
            CancellationToken cancellationToken = default);

        /// <summary>Interrompe imediatamente toda movimentação do agente.</summary>
        void Stop(NavMeshAgentAdapter adapter);

        /// <summary>Retoma a navegação após uma interrupção via <see cref="Stop"/>.</summary>
        void Resume(NavMeshAgentAdapter adapter);

        /// <summary>
        /// Suspende temporariamente a navegação, preservando destino e caminho
        /// para retomada exata via <see cref="ResumeNavigation"/>.
        /// </summary>
        void PauseNavigation(NavMeshAgentAdapter adapter);

        /// <summary>Continua uma navegação suspensa via <see cref="PauseNavigation"/>.</summary>
        void ResumeNavigation(NavMeshAgentAdapter adapter);

        // ─────────────────────────────────────────────────────────────
        // Teleporte e posicionamento
        // ─────────────────────────────────────────────────────────────

        /// <summary>Teletransporta o agente para uma posição sem cálculo de caminho.</summary>
        bool Warp(NavMeshAgentAdapter adapter, Vector3 position);

        /// <summary>
        /// Move o agente instantaneamente para o ponto válido mais próximo da NavMesh.
        /// </summary>
        bool TeleportToNearestNavMeshPoint(NavMeshAgentAdapter adapter, Vector3 position,
            float maxDistance = 5f);

        // ─────────────────────────────────────────────────────────────
        // Controle de destino e velocidade
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Atualiza diretamente o destino atual do agente sem alterar outros estados.
        /// </summary>
        bool SetDestination(NavMeshAgentAdapter adapter, Vector3 destination);

        /// <summary>Define manualmente o vetor de velocidade do agente (unidades/s).</summary>
        void SetVelocity(NavMeshAgentAdapter adapter, Vector3 velocity);

        /// <summary>Define a velocidade máxima de movimentação do agente.</summary>
        void SetSpeed(NavMeshAgentAdapter adapter, float speed);

        /// <summary>Define a velocidade máxima de rotação do agente (graus/s).</summary>
        void SetAngularSpeed(NavMeshAgentAdapter adapter, float angularSpeed);

        /// <summary>Define a aceleração do agente (unidades/s²).</summary>
        void SetAcceleration(NavMeshAgentAdapter adapter, float acceleration);

        /// <summary>
        /// Define a distância mínima do destino para considerar que ele foi alcançado.
        /// </summary>
        void SetStoppingDistance(NavMeshAgentAdapter adapter, float distance);

        // ─────────────────────────────────────────────────────────────
        // Caminhos
        // ─────────────────────────────────────────────────────────────

        /// <summary>Remove o caminho atualmente calculado.</summary>
        void ClearPath(NavMeshAgentAdapter adapter);

        /// <summary>
        /// Solicita um novo cálculo de rota até o destino atual sem alterar o destino.
        /// </summary>
        bool RecalculatePath(NavMeshAgentAdapter adapter);

        /// <summary>
        /// Calcula um caminho até um ponto SEM mover o agente.
        /// Retorna null se o destino for inalcançável.
        /// </summary>
        NavMeshPath CalculatePath(NavMeshAgentAdapter adapter, Vector3 destination);

        /// <summary>
        /// Calcula o comprimento total de um <see cref="NavMeshPath"/> percorrendo seus corners.
        /// </summary>
        float GetPathLength(NavMeshPath path);

        /// <summary>
        /// Retorna os pontos de curva (corners) do caminho atualmente em uso pelo agente.
        /// </summary>
        Vector3[] GetPathCorners(NavMeshAgentAdapter adapter);

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do caminho
        // ─────────────────────────────────────────────────────────────

        /// <summary>Verifica se existe um caminho válido calculado.</summary>
        bool HasPath(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se o caminho pode ser concluído integralmente até o destino.</summary>
        bool IsPathComplete(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se apenas parte do caminho é alcançável.</summary>
        bool IsPathPartial(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se o caminho atual é inválido ou não pôde ser calculado.</summary>
        bool IsPathInvalid(NavMeshAgentAdapter adapter);

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do agente
        // ─────────────────────────────────────────────────────────────

        /// <summary>Verifica se o agente está se deslocando ativamente.</summary>
        bool IsMoving(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se o agente está completamente parado.</summary>
        bool IsStopped(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se o cálculo do caminho ainda está em processamento assíncrono.</summary>
        bool IsPending(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se o agente alcançou o destino configurado.</summary>
        bool HasReachedDestination(NavMeshAgentAdapter adapter);

        /// <summary>Verifica se o agente está posicionado sobre uma NavMesh válida.</summary>
        bool IsOnNavMesh(NavMeshAgentAdapter adapter);

        /// <summary>Obtém a distância restante até o destino (em unidades de mundo).</summary>
        float GetRemainingDistance(NavMeshAgentAdapter adapter);

        /// <summary>Retorna o destino atualmente configurado no agente.</summary>
        Vector3 GetCurrentDestination(NavMeshAgentAdapter adapter);

        /// <summary>Retorna o vetor de velocidade atual do agente.</summary>
        Vector3 GetCurrentVelocity(NavMeshAgentAdapter adapter);

        /// <summary>Retorna a direção normalizada do deslocamento atual.</summary>
        Vector3 GetNormalizedDirection(NavMeshAgentAdapter adapter);

        // ─────────────────────────────────────────────────────────────
        // NavMesh queries (sem agente específico)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Busca um ponto válido sobre a NavMesh próximo de <paramref name="sourcePosition"/>.
        /// </summary>
        bool SamplePosition(Vector3 sourcePosition, out Vector3 result, float maxDistance,
            int areaMask = NavMesh.AllAreas);

        /// <summary>
        /// Verifica se há obstruções navegáveis entre dois pontos da NavMesh.
        /// Retorna true se o trajeto estiver limpo (sem obstáculos).
        /// </summary>
        bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit);

        /// <summary>Verifica se um destino é navegável antes de iniciar a movimentação.</summary>
        bool ValidateDestination(Vector3 destination, float sampleRadius = 1f);

        /// <summary>
        /// Verifica se um ponto pode ser alcançado pelo agente a partir da sua posição atual.
        /// </summary>
        bool CanReach(NavMeshAgentAdapter adapter, Vector3 destination);

        // ─────────────────────────────────────────────────────────────
        // Espera assíncrona
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Aguarda até que o destino seja alcançado.
        /// Retorna um <see cref="NavMeshOperation"/> cujo <c>Task</c> resolve com
        /// <c>true</c> ao chegar, <c>false</c> se cancelado ou timeout.
        /// </summary>
        NavMeshOperation WaitUntilReachedAsync(NavMeshAgentAdapter adapter,
            float timeout = 30f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Aguarda até que o agente pare completamente (velocity ≈ zero).
        /// Retorna um <see cref="NavMeshOperation"/> cujo <c>Task</c> resolve com
        /// <c>true</c> ao parar, <c>false</c> se cancelado ou timeout.
        /// </summary>
        NavMeshOperation WaitUntilStoppedAsync(NavMeshAgentAdapter adapter,
            float timeout = 10f, CancellationToken cancellationToken = default);

        // ─────────────────────────────────────────────────────────────
        // Rotação
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Rotaciona o agente suavemente para olhar em direção a um Transform alvo.
        /// </summary>
        NavMeshOperation FaceTargetAsync(NavMeshAgentAdapter adapter, Transform target,
            float rotationSpeed = 360f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rotaciona o agente suavemente para uma direção específica no plano XZ.
        /// </summary>
        NavMeshOperation FaceDirectionAsync(NavMeshAgentAdapter adapter, Vector3 direction,
            float rotationSpeed = 360f, CancellationToken cancellationToken = default);

        // ─────────────────────────────────────────────────────────────
        // Configurações de comportamento
        // ─────────────────────────────────────────────────────────────

        /// <summary>Ativa ou desativa a desaceleração automática próximo ao destino.</summary>
        void EnableAutoBraking(NavMeshAgentAdapter adapter, bool enabled);

        /// <summary>Ativa ou desativa a rotação automática do agente durante a navegação.</summary>
        void EnableRotation(NavMeshAgentAdapter adapter, bool enabled);

        /// <summary>Ativa ou desativa a atualização automática de posição pelo NavMeshAgent.</summary>
        void EnablePositionUpdate(NavMeshAgentAdapter adapter, bool enabled);

        /// <summary>Ativa ou desativa o desvio automático de obstáculos e outros agentes.</summary>
        void EnableObstacleAvoidance(NavMeshAgentAdapter adapter, bool enabled);

        /// <summary>Define quais áreas da NavMesh o agente pode utilizar via bitmask.</summary>
        void SetAreaMask(NavMeshAgentAdapter adapter, int areaMask);

        /// <summary>
        /// Define a prioridade de desvio entre agentes (0 = máxima prioridade, 99 = mínima).
        /// </summary>
        void SetAvoidancePriority(NavMeshAgentAdapter adapter, int priority);

        // ─────────────────────────────────────────────────────────────
        // Ciclo de vida do agente
        // ─────────────────────────────────────────────────────────────

        /// <summary>Ativa o componente NavMeshAgent.</summary>
        void EnableAgent(NavMeshAgentAdapter adapter);

        /// <summary>Desativa o componente NavMeshAgent e cancela operações ativas.</summary>
        void DisableAgent(NavMeshAgentAdapter adapter);

        /// <summary>
        /// Restaura o agente para o estado padrão: para o movimento, limpa o caminho
        /// e redefine as configurações para os valores do snapshot inicial.
        /// </summary>
        void ResetAgent(NavMeshAgentAdapter adapter);

        // ── NavMesh Links ──────────────────────────────────────────────────────────

        /// <summary>Verifica se o agente está sobre um NavMesh Link (OffMeshLink).</summary>
        bool IsOnNavMeshLink(NavMeshAgentAdapter adapter);

        /// <summary>
        /// Traversa o NavMesh Link atual do agente de forma suave (Lerp).
        /// O agente tem updatePosition/updateRotation desabilitados durante a travessia.
        /// Resolve com true ao concluir, false se cancelado.
        /// </summary>
        NavMeshOperation TraverseNavMeshLinkAsync(
            NavMeshAgentAdapter adapter,
            float speed,
            float rotationSpeed,
            CancellationToken cancellationToken = default);
    }
}
