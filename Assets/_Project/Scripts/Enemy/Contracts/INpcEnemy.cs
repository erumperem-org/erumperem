// ============================================================
// INpcEnemy.cs
// Namespace : Systems.NPC.Enemy.Contracts
// ============================================================

using UnityEngine;

namespace Systems.NPC.Enemy.Contracts
{
    public enum NpcEnemyState
    {
        Idle,
        Wander,
        Chase,
        ReturningToPool
    }

    public interface INpcEnemy
    {
        NpcEnemyState CurrentState { get; }

        /// <summary>
        /// Disparado quando o NPC entra em contato físico com o Player.
        /// Quem decide o que fazer (ex: carregar cena) é o ouvinte externo,
        /// não o NPC — respeitando SRP.
        /// </summary>
        event System.Action<INpcEnemy> OnPlayerContact;

        void Initialize(NpcEnemyConfig config);
        void Activate();
        void ReturnToPool();
    }
}
