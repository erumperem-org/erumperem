using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Gerencia a pool de inimigos usando <see cref="ObjectPool{T}"/> do Unity.
    /// Controla o ciclo de vida completo: criação, ativação, desativação e destruição.
    ///
    /// Fluxo básico:
    ///   1. <see cref="GetEnemy"/> → obtém ou cria um inimigo → OnGet → PatrolBehavior
    ///   2. <see cref="ReleaseEnemy"/> → OnRelease → OnPoolBehavior → desativado
    ///   3. Ao atingir <see cref="PoolMaxSize"/>, novos gets retornam null (com aviso).
    /// </summary>
    public class ExplorationEnemyPooling : MonoBehaviour
    {
        // ── Referências ────────────────────────────────────────────────────────────

        [Header("Builder")]
        [SerializeField] private ExplorationEnemyBuilder _builder;

        [Header("Hierarquia de objetos")]
        [Tooltip("Parent dos inimigos inativos (na pool).")]
        public Transform PooledObjectsParent;

        [Tooltip("Parent dos inimigos ativos na cena.")]
        public Transform ActiveObjectsParent;

        [Header("Alvo")]
        [Tooltip("Transform do jogador; passado para o PatrolBehavior ao ativar inimigos.")]
        public Transform Player;

        // ── Configuração da pool ───────────────────────────────────────────────────

        [Header("Configuração da Pool")]
        [Tooltip("Capacidade inicial pré-alocada pela pool (não é o limite máximo).")]
        [SerializeField] private int _poolDefaultCapacity = 10;

        [Tooltip("Número máximo de instâncias que podem existir simultaneamente. " +
                 "Acima disso, GetEnemy retorna null.")]
        [SerializeField] private int _poolMaxSize = 20;

        [Tooltip("Posição para onde inimigos inativos são movidos ao retornar para a pool.")]
        [SerializeField] private Vector3 _poolPosition;

        // ── Spawn ──────────────────────────────────────────────────────────────────

        [Header("Configuração de Spawn")]
        [Tooltip("Nível a ser atribuído ao próximo inimigo obtido da pool.")]
        [SerializeField] private ExplorationEnemyLevels _nextEnemyLevelToCreate;

        [Tooltip("Centro de referência para o ponto aleatório de spawn no NavMesh.")]
        [SerializeField] private Vector3 _spawnCenter = Vector3.zero;

        [Tooltip("Raio de busca de pontos aleatórios no NavMesh para posicionamento de spawn.")]
        [SerializeField] private float _spawnNavMeshRadius = 50f;

        // ── Estado interno ─────────────────────────────────────────────────────────

        private ObjectPool<ExplorationEnemyController> _enemyPool;

        // ── Unity lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            // Inicializa a pool com os callbacks de ciclo de vida
            _enemyPool = new ObjectPool<ExplorationEnemyController>(
                createFunc:       CreateEnemy,
                actionOnGet:      OnGet,
                actionOnRelease:  OnRelease,
                actionOnDestroy:  OnDestroyItem,
                collectionCheck:  true,
                defaultCapacity:  _poolDefaultCapacity,
                maxSize:          _poolMaxSize
            );

            // Reinicia o contador de IDs ao inicializar a pool
            ExplorationEnemyBuilder.EnemyNumber = 0;
        }

        // ── API pública ────────────────────────────────────────────────────────────

        /// <summary>
        /// Obtém um inimigo da pool (ou cria um novo se a capacidade permitir)
        /// e o configura com o nível especificado e o comportamento de patrulha.
        /// Retorna null se a pool estiver no limite máximo e não houver inativos disponíveis.
        /// </summary>
        /// <param name="level">Nível do inimigo a ser ativado.</param>
        public ExplorationEnemyController GetEnemy(ExplorationEnemyLevels level)
        {
            if (!HasAvailableSlot())
            {
                Debug.LogWarning($"[EnemyPool] Pool atingiu a capacidade máxima ({_poolMaxSize}). " +
                                 "Aumente PoolMaxSize ou libere inimigos antes de solicitar novos.");
                return null;
            }

            _nextEnemyLevelToCreate = level;

            ExplorationEnemyController enemy = _enemyPool.Get();
            enemy.transform.SetParent(ActiveObjectsParent);

            return enemy;
        }

        /// <summary>
        /// Devolve um inimigo para a pool, ativando o <see cref="OnPoolBehavior"/>
        /// (reposiciona e desativa o GameObject).
        /// </summary>
        public void ReleaseEnemy(ExplorationEnemyController controller)
        {
            _enemyPool.Release(controller);
        }

        // ── Callbacks da ObjectPool ────────────────────────────────────────────────

        /// <summary>Chamado pela pool quando precisa criar uma nova instância.</summary>
        private ExplorationEnemyController CreateEnemy()
        {
            return _builder.CreateEnemy(
                GetRandomNavMeshPoint(_spawnCenter, _spawnNavMeshRadius),
                ActiveObjectsParent,
                _nextEnemyLevelToCreate
            );
        }

        /// <summary>
        /// Chamado quando um inimigo é retirado da pool (<see cref="GetEnemy"/>).
        /// Reposiciona, reativa e inicia o comportamento de patrulha.
        /// </summary>
        private void OnGet(ExplorationEnemyController controller)
        {
            controller.transform.position = GetRandomNavMeshPoint(_spawnCenter, _spawnNavMeshRadius);
            controller.gameObject.SetActive(true);

            // Atualiza o nível caso o inimigo vá com um nível diferente do que tinha
            if (controller.Data.EnemyLevel != _nextEnemyLevelToCreate)
                _ = ExplorationEnemyController.SetEnemyLevel(controller, _nextEnemyLevelToCreate);

            // Inicia o comportamento de patrulha com o jogador como alvo
            _ = ExplorationEnemyController.SetEnemyStartegy(
                controller,
                new PatrolBehavior(),
                new PatrolBehaviorContext(controller, Player, controller.Data.PerceptionRadius)
            );
        }

        /// <summary>
        /// Chamado quando um inimigo é devolvido para a pool (<see cref="ReleaseEnemy"/>).
        /// Executa o OnPoolBehavior de forma assíncrona antes de desativar o GameObject.
        /// </summary>
        private void OnRelease(ExplorationEnemyController controller)
        {
            _ = SetPoolState(controller);
        }

        /// <summary>
        /// Aplica o estado de pool ao inimigo: move para a posição de pool, reparenta
        /// e desativa o GameObject após a estratégia ser configurada.
        /// </summary>
        private async Task SetPoolState(ExplorationEnemyController controller)
        {
            await ExplorationEnemyController.SetEnemyStartegy(
                controller,
                new OnPoolBehavior(),
                new OnPoolBehaviorContext(controller, _poolPosition, PooledObjectsParent)
            );

            controller.gameObject.SetActive(false);
        }

        /// <summary>
        /// Chamado quando a pool descarta permanentemente uma instância
        /// (ex: ao destruir o gerenciador ou reduzir o tamanho da pool).
        /// </summary>
        private void OnDestroyItem(ExplorationEnemyController controller)
        {
            _builder.DestroyEnemy(controller);
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Verifica se ainda há espaço para ativar um novo inimigo:
        /// ou existe algum inativo disponível, ou o total ainda está abaixo do máximo.
        /// </summary>
        private bool HasAvailableSlot()
        {
            return _enemyPool.CountAll < _poolMaxSize
                   || _enemyPool.CountInactive > 0;
        }

        /// <summary>
        /// Coroutine auxiliar para liberar um inimigo após um delay em segundos.
        /// Útil para testes ou lógicas de tempo de vida.
        /// </summary>
        private IEnumerator ReturnAfter(ExplorationEnemyController controller, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ReleaseEnemy(controller);
        }

        /// <summary>
        /// Retorna um ponto aleatório válido no NavMesh dentro de <paramref name="range"/>
        /// a partir de <paramref name="center"/>. Retorna Vector3.zero se nenhum for encontrado.
        /// </summary>
        private Vector3 GetRandomNavMeshPoint(Vector3 center, float range)
        {
            Vector3 randomPoint  = center + Random.insideUnitSphere * range;
            bool    foundPosition = NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, range, NavMesh.AllAreas);
            return foundPosition ? hit.position : Vector3.zero;
        }
    }
}
