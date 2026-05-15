using System.Threading.Tasks;
using UnityEngine;

namespace Core.Exploration.Enemy
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  Interfaces – Contrato do padrão Strategy de comportamento de inimigos
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Contrato base para qualquer comportamento de inimigo.
    /// Toda estratégia deve saber se iniciar ao receber um contexto.
    /// </summary>
    public interface IEnemyStartegy
    {
        /// <summary>Inicia a execução do comportamento com o contexto fornecido.</summary>
        Task ExecuteBehavior(IEnemyStartegyContext context);
    }

    /// <summary>
    /// Extensão do contrato base para estratégias que possuem limpeza ao serem trocadas.
    /// Permite cancelar ou reverter o comportamento anterior antes de iniciar um novo.
    /// </summary>
    public interface IReverseableEnemyStartegy : IEnemyStartegy
    {
        /// <summary>Executa a limpeza assíncrona do comportamento (ex: cancelar tokens, resetar path).</summary>
        Task UnexecuteBehavior(IEnemyStartegyContext context);

        /// <summary>Cancela imediatamente sem aguardar tarefas assíncronas (usado no OnDestroy).</summary>
        void CancelImmediate();
    }

    /// <summary>
    /// Marcador de contexto passado para cada estratégia.
    /// Cada comportamento define sua própria classe de contexto concreta.
    /// </summary>
    public interface IEnemyStartegyContext { }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Contextos – dados necessários para cada comportamento específico
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dados necessários para o comportamento de patrulha.
    /// O inimigo vaga pela cena e transiciona para perseguição ao detectar o alvo.
    /// </summary>
    public class PatrolBehaviorContext : IEnemyStartegyContext
    {
        public ExplorationEnemyController Enemy;
        public Transform Target;
        public float PerceptionRadius;

        public PatrolBehaviorContext(
            ExplorationEnemyController enemy,
            Transform target,
            float perceptionRadius)
        {
            Enemy           = enemy;
            Target          = target;
            PerceptionRadius = perceptionRadius;
        }
    }

    /// <summary>
    /// Dados necessários para o comportamento de perseguição.
    /// O inimigo segue ativamente o alvo enquanto ele estiver dentro do raio de percepção.
    /// </summary>
    public class PursuingBehaviorContext : IEnemyStartegyContext
    {
        public ExplorationEnemyController Enemy;
        public Transform Target;
        public float PerceptionRadius;

        public PursuingBehaviorContext(
            ExplorationEnemyController enemy,
            Transform target,
            float perceptionRadius)
        {
            Enemy           = enemy;
            Target          = target;
            PerceptionRadius = perceptionRadius;
        }
    }

    /// <summary>
    /// Dados necessários para o comportamento de stalking.
    /// O inimigo mantém uma distância específica do alvo sem se aproximar demais.
    /// </summary>
    public class StalkingBehaviorContext : IEnemyStartegyContext
    {
        public ExplorationEnemyController Enemy;
        public Transform Target;
        public float StalkingDistance;

        public StalkingBehaviorContext(
            ExplorationEnemyController enemy,
            Transform target,
            float stalkingDistance)
        {
            Enemy           = enemy;
            Target          = target;
            StalkingDistance = stalkingDistance;
        }
    }

    /// <summary>
    /// Dados necessários para o comportamento de pool (inativo).
    /// Reposiciona o inimigo e o move para o parent de objetos poolados.
    /// </summary>
    public class OnPoolBehaviorContext : IEnemyStartegyContext
    {
        public ExplorationEnemyController Enemy;
        public Vector3 NewPosition;
        public Transform Parent;

        public OnPoolBehaviorContext(
            ExplorationEnemyController enemy,
            Vector3 newPosition,
            Transform parent)
        {
            Enemy       = enemy;
            NewPosition = newPosition;
            Parent      = parent;
        }
    }
}
