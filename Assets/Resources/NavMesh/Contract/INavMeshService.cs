using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;

namespace Services.Navigation
{
    /// <summary>
    /// Camada de serviço para operações de navegação via NavMeshAgent.
    /// Fornece uma interface fluente e segura para agentes de IA consumirem.
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
        bool MoveTo(Vector3 destination);

        /// <summary>
        /// Move o agente até o destino aguardando a conclusão de forma assíncrona.
        /// Retorna true se o destino foi alcançado, false se cancelado, inválido ou timeout.
        /// </summary>
        Task<bool> MoveToAsync(Vector3 destination, float timeout = 30f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Faz o agente seguir continuamente um alvo via Coroutine interna.
        /// O agente atualiza o destino a cada <paramref name="updateInterval"/> segundos.
        /// </summary>
        void FollowTarget(Transform target, float updateInterval = 0.15f);

        /// <summary>
        /// Segue um alvo continuamente até cancelamento ou atingir <paramref name="stopDistance"/>.
        /// </summary>
        Task FollowTargetAsync(Transform target, float stopDistance = 1.5f, float updateInterval = 0.15f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Interrompe imediatamente toda movimentação do agente.
        /// </summary>
        void Stop();

        /// <summary>
        /// Retoma a navegação após uma interrupção via <see cref="Stop"/>.
        /// </summary>
        void Resume();

        /// <summary>
        /// Suspende temporariamente a navegação mantendo o caminho e destino atuais.
        /// Diferente de <see cref="Stop"/>, preserva o estado para retomada exata via <see cref="ResumeNavigation"/>.
        /// </summary>
        void PauseNavigation();

        /// <summary>
        /// Continua uma navegação suspensa via <see cref="PauseNavigation"/>.
        /// </summary>
        void ResumeNavigation();

        /// <summary>
        /// Cancela qualquer operação assíncrona de movimentação ativa e para o agente.
        /// </summary>
        void CancelMovement();

        // ─────────────────────────────────────────────────────────────
        // Teleporte e posicionamento
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Teletransporta o agente para uma posição sem cálculo de caminho.
        /// </summary>
        bool Warp(Vector3 position);

        /// <summary>
        /// Move o agente instantaneamente para o ponto válido mais próximo da NavMesh.
        /// </summary>
        bool TeleportToNearestNavMeshPoint(Vector3 position, float maxDistance = 5f);

        // ─────────────────────────────────────────────────────────────
        // Controle de destino e velocidade
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Atualiza diretamente o destino atual do agente sem alterar outros estados.
        /// </summary>
        bool SetDestination(Vector3 destination);

        /// <summary>
        /// Define manualmente o vetor de velocidade do agente (em unidades/s).
        /// Ignora a física de navegação enquanto ativo.
        /// </summary>
        void SetVelocity(Vector3 velocity);

        /// <summary>Define a velocidade máxima de movimentação do agente.</summary>
        void SetSpeed(float speed);

        /// <summary>Define a velocidade máxima de rotação do agente (graus/s).</summary>
        void SetAngularSpeed(float angularSpeed);

        /// <summary>Define a aceleração do agente (unidades/s²).</summary>
        void SetAcceleration(float acceleration);

        /// <summary>
        /// Define a distância mínima do destino para considerar que ele foi alcançado.
        /// </summary>
        void SetStoppingDistance(float distance);

        // ─────────────────────────────────────────────────────────────
        // Caminhos
        // ─────────────────────────────────────────────────────────────

        /// <summary>Remove o caminho atualmente calculado.</summary>
        void ClearPath();

        /// <summary>
        /// Solicita um novo cálculo de rota até o destino atual sem alterar o destino.
        /// </summary>
        bool RecalculatePath();

        /// <summary>
        /// Calcula um caminho até um ponto SEM mover o agente.
        /// Retorna null se o destino for inalcançável.
        /// </summary>
        NavMeshPath CalculatePath(Vector3 destination);

        /// <summary>
        /// Calcula o comprimento total de um <see cref="NavMeshPath"/> percorrendo seus corners.
        /// </summary>
        float GetPathLength(NavMeshPath path);

        /// <summary>
        /// Retorna os pontos de curva (corners) do caminho calculado atualmente em uso.
        /// </summary>
        Vector3[] GetPathCorners();

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do caminho
        // ─────────────────────────────────────────────────────────────

        /// <summary>Verifica se existe um caminho válido calculado.</summary>
        bool HasPath();

        /// <summary>Verifica se o caminho pode ser concluído integralmente até o destino.</summary>
        bool IsPathComplete();

        /// <summary>Verifica se apenas parte do caminho é alcançável (destino fora da NavMesh).</summary>
        bool IsPathPartial();

        /// <summary>Verifica se o caminho atual é inválido ou não pôde ser calculado.</summary>
        bool IsPathInvalid();

        // ─────────────────────────────────────────────────────────────
        // Verificações de estado do agente
        // ─────────────────────────────────────────────────────────────

        /// <summary>Verifica se o agente está se deslocando ativamente.</summary>
        bool IsMoving();

        /// <summary>Verifica se o agente está completamente parado.</summary>
        bool IsStopped();

        /// <summary>Verifica se o cálculo do caminho ainda está em processamento assíncrono.</summary>
        bool IsPending();

        /// <summary>Verifica se o agente alcançou o destino configurado.</summary>
        bool HasReachedDestination();

        /// <summary>Verifica se o agente está posicionado sobre uma NavMesh válida.</summary>
        bool IsOnNavMesh();

        /// <summary>Obtém a distância restante até o destino (em unidades de mundo).</summary>
        float GetRemainingDistance();

        /// <summary>Retorna o destino atualmente configurado no agente.</summary>
        Vector3 GetCurrentDestination();

        /// <summary>Retorna o vetor de velocidade atual do agente.</summary>
        Vector3 GetCurrentVelocity();

        /// <summary>Retorna a direção normalizada do deslocamento atual.</summary>
        Vector3 GetNormalizedDirection();

        // ─────────────────────────────────────────────────────────────
        // NavMesh queries
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Busca um ponto válido sobre a NavMesh próximo de <paramref name="sourcePosition"/>.
        /// </summary>
        bool SamplePosition(Vector3 sourcePosition, out Vector3 result, float maxDistance, int areaMask = NavMesh.AllAreas);

        /// <summary>
        /// Verifica se há obstruções navegáveis entre dois pontos da NavMesh.
        /// Retorna true se o trajeto estiver limpo (sem obstáculos de NavMesh).
        /// </summary>
        bool Raycast(Vector3 sourcePosition, Vector3 targetPosition, out NavMeshHit hit);

        /// <summary>
        /// Verifica se um destino é navegável antes de iniciar a movimentação.
        /// </summary>
        bool ValidateDestination(Vector3 destination, float sampleRadius = 1f);

        /// <summary>
        /// Verifica se um ponto pode ser alcançado pelo agente a partir da posição atual.
        /// </summary>
        bool CanReach(Vector3 destination);

        // ─────────────────────────────────────────────────────────────
        // Espera assíncrona
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Aguarda até que o destino seja alcançado.
        /// Retorna true se chegou, false se cancelado ou timeout.
        /// </summary>
        Task<bool> WaitUntilReachedAsync(float timeout = 30f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Aguarda até que o agente pare completamente (velocity ≈ zero).
        /// Retorna true se parou, false se cancelado ou timeout.
        /// </summary>
        Task<bool> WaitUntilStoppedAsync(float timeout = 10f, CancellationToken cancellationToken = default);

        // ─────────────────────────────────────────────────────────────
        // Rotação
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Rotaciona o agente suavemente para olhar em direção a um Transform alvo.
        /// </summary>
        Task FaceTargetAsync(Transform target, float rotationSpeed = 360f, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rotaciona o agente suavemente para uma direção específica no plano XZ.
        /// </summary>
        Task FaceDirectionAsync(Vector3 direction, float rotationSpeed = 360f, CancellationToken cancellationToken = default);

        // ─────────────────────────────────────────────────────────────
        // Configurações de comportamento
        // ─────────────────────────────────────────────────────────────

        /// <summary>Ativa ou desativa a desaceleração automática próximo ao destino.</summary>
        void EnableAutoBraking(bool enabled);

        /// <summary>Ativa ou desativa a rotação automática do agente durante a navegação.</summary>
        void EnableRotation(bool enabled);

        /// <summary>Ativa ou desativa a atualização automática de posição pelo NavMeshAgent.</summary>
        void EnablePositionUpdate(bool enabled);

        /// <summary>Ativa ou desativa o desvio automático de obstáculos e outros agentes.</summary>
        void EnableObstacleAvoidance(bool enabled);

        /// <summary>Define quais áreas da NavMesh o agente pode utilizar via bitmask.</summary>
        void SetAreaMask(int areaMask);

        /// <summary>
        /// Define a prioridade de desvio entre agentes (0 = máxima prioridade, 99 = mínima).
        /// </summary>
        void SetAvoidancePriority(int priority);

        // ─────────────────────────────────────────────────────────────
        // Ciclo de vida do agente
        // ─────────────────────────────────────────────────────────────

        /// <summary>Ativa o componente NavMeshAgent.</summary>
        void EnableAgent();

        /// <summary>Desativa o componente NavMeshAgent.</summary>
        void DisableAgent();

        /// <summary>
        /// Restaura o agente para o estado padrão: para o movimento, limpa o caminho
        /// e redefine as configurações para os valores do snapshot inicial.
        /// </summary>
        void ResetAgent();
    }
}
