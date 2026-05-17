using UnityEngine;
using UnityEngine.AI;

namespace Services.Navigation
{
    /// <summary>
    /// MonoBehaviour mínimo que vive no GameObject do agente.
    /// Responsabilidade única: expor o <see cref="NavMeshAgent"/> para que o
    /// <see cref="NavMeshService"/> possa operá-lo, e hospedar as Coroutines
    /// que requerem um contexto de MonoBehaviour.
    ///
    /// Não contém lógica de navegação — toda ela fica no serviço.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class NavMeshAgentAdapter : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        // Componentes
        // ─────────────────────────────────────────────────────────────

        /// <summary>Agente gerenciado por este adapter.</summary>
        public NavMeshAgent Agent { get; private set; }

        // ─────────────────────────────────────────────────────────────
        // Snapshot de configuração (para ResetAgent)
        // ─────────────────────────────────────────────────────────────

        internal float DefaultSpeed { get; private set; }
        internal float DefaultAngularSpeed { get; private set; }
        internal float DefaultAcceleration { get; private set; }
        internal float DefaultStoppingDistance { get; private set; }
        internal bool DefaultAutoBraking { get; private set; }

        // ─────────────────────────────────────────────────────────────
        // Ciclo de vida Unity
        // ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();

            if (Agent == null)
            {
                Debug.LogError($"[NavMeshAgentAdapter] Nenhum NavMeshAgent encontrado em '{gameObject.name}'.");
                return;
            }

            TakeSnapshot();
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers internos
        // ─────────────────────────────────────────────────────────────

        private void TakeSnapshot()
        {
            DefaultSpeed = Agent.speed;
            DefaultAngularSpeed = Agent.angularSpeed;
            DefaultAcceleration = Agent.acceleration;
            DefaultStoppingDistance = Agent.stoppingDistance;
            DefaultAutoBraking = Agent.autoBraking;
        }

        /// <summary>
        /// Verifica se o agente está pronto para receber comandos de navegação.
        /// </summary>
        public bool IsReady()
            => Agent != null && Agent.enabled && Agent.isOnNavMesh;

        /// <summary>
        /// Habilita ou desabilita a rotação automática do NavMeshAgent.
        /// Use <c>false</c> quando a rotação for controlada manualmente.
        /// </summary>
        public void SetUpdateRotation(bool value)
        {
            if (Agent != null)
                Agent.updateRotation = value;
        }

        // ── NavMesh Link ───────────────────────────────────────────────────────────

        /// <summary>True quando o agente está sobre um OffMeshLink / NavMesh Link.</summary>
        public bool IsOnNavMeshLink => Agent.isOnOffMeshLink;

        /// <summary>Dados do link atual (startPos, endPos, linkType…).</summary>
        public OffMeshLinkData CurrentLinkData => Agent.currentOffMeshLinkData;
    }


}
