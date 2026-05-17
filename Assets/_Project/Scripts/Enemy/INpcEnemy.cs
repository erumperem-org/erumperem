// ============================================================
// INpcEnemy.cs
// Namespace : Systems.NPC.Enemy.Contracts
// ============================================================
// Contrato principal do NPC inimigo.
// Permite que Pool, Builder e Spawner operem sobre o NPC
// sem depender da implementação concreta.
// ============================================================

using UnityEngine;

namespace Systems.NPC.Enemy.Contracts
{
    /// <summary>
    /// Estado interno do NPC inimigo.
    /// Cada estado corresponde a uma Coroutine ativa distinta.
    /// </summary>
    public enum NpcEnemyState
    {
        Idle,
        Wander,
        Chase,
        ReturningToPool
    }

    /// <summary>
    /// Contrato público do NPC inimigo.
    /// Pool, Builder e Spawner só conhecem esta interface.
    /// </summary>
    public interface INpcEnemy
    {
        /// <summary>Estado atual do NPC.</summary>
        NpcEnemyState CurrentState { get; }

        /// <summary>
        /// Inicializa o NPC com a configuração fornecida pelo Builder.
        /// Chamado imediatamente após retirar da pool.
        /// </summary>
        void Initialize(NpcEnemyConfig config);

        /// <summary>
        /// Ativa o NPC: posiciona no spawn e inicia o comportamento de wander.
        /// </summary>
        void Activate();

        /// <summary>
        /// Encerra todas as Coroutines, limpa estado e devolve à pool.
        /// Pode ser chamado por qualquer sistema externo.
        /// </summary>
        void ReturnToPool();
    }
}
