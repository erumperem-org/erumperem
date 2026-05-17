using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.Movement
{
    /// <summary>
    /// Contexto base com os dados comuns a todos os comportamentos de movimentação.
    /// O <see cref="NavMeshAgentAdapter"/> vive aqui — os behaviors não precisam
    /// resolvê-lo individualmente.
    /// </summary>
    public abstract class CharacterMovementContextBase : ICharacterMovementStrategyContext
    {
        // ── Referências de infraestrutura ────────────────────────────────
        public readonly NpcMovementController    Controller;
        public readonly INavMeshService          NavMesh;
        public readonly NavMeshAgentAdapter      Adapter;

        // ── Referências de cena ──────────────────────────────────────────
        public readonly Transform Self;
        public readonly Transform Target;

        // ── Dados de configuração ────────────────────────────────────────
        public readonly string CharacterName;
        public readonly float  PerceptionRadius;

        protected CharacterMovementContextBase(
            NpcMovementController controller,
            INavMeshService       navMesh,
            NavMeshAgentAdapter   adapter,
            Transform             self,
            Transform             target,
            string                characterName,
            float                 perceptionRadius)
        {
            Controller       = controller;
            NavMesh          = navMesh;
            Adapter          = adapter;
            Self             = self;
            Target           = target;
            CharacterName    = characterName;
            PerceptionRadius = perceptionRadius;
        }
    }
}
