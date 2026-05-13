using System;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Enemy
{
    /// <summary>
    /// Contém todos os dados de um inimigo de exploração.
    /// Serializado como campo público no <see cref="ExplorationEnemyController"/>
    /// para ser inspecionável no editor.
    /// </summary>
    [Serializable]
    public class ExplorationEnemyData
    {
        // ── Diagnóstico ──────────────────────────────────────────────────────────

        [Header("Diagnóstico (somente leitura em runtime)")]
        [Tooltip("Nome da estratégia ativa, atualizado automaticamente pelo controller.")]
        public string EnemyStartegyExposed;

        // ── Referências de comportamento ─────────────────────────────────────────

        /// <summary>Estratégia de comportamento atualmente ativa.</summary>
        public IEnemyStartegy ActiveStrategy;

        /// <summary>Contexto associado à estratégia ativa.</summary>
        public IEnemyStartegyContext CurrentContext;

        // ── Navegação ────────────────────────────────────────────────────────────

        [Header("Navegação")]
        [Tooltip("NavMeshAgent controlado pelas estratégias de movimento.")]
        public NavMeshAgent Agent;

        // ── Identificação ────────────────────────────────────────────────────────

        [Header("Identificação")]
        [Tooltip("ID único gerado pelo builder no momento da criação.")]
        public string EnemyId;

        // ── Configuração de comportamento ────────────────────────────────────────

        [Header("Configuração de Comportamento")]
        [Tooltip("Nível de força/dificuldade do inimigo.")]
        public ExplorationEnemyLevels EnemyLevel;

        [Tooltip("Raio de percepção: distância na qual o inimigo detecta o alvo.")]
        public float PerceptionRadius;

        [Tooltip("Raio de patrulha: alcance máximo dos pontos de destino aleatórios durante a patrulha.")]
        public float PatrolRadius;
    }
}
