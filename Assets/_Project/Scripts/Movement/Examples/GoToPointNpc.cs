using Core.Exploration.Character.Movement;
using Core.Exploration.Character.NPC;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Presets
{
    /// <summary>
    /// NPC que navega até um ponto fixo e para ao chegar (transiciona para FreeBehavior).
    /// Útil para NPCs que precisam se posicionar em um local específico na cena.
    ///
    /// Setup: adicione este componente ao GameObject junto com NavMeshAgent e NavMeshAgentAdapter.
    /// Atribua um Transform de destino no Inspector ou chame <see cref="SetDestination"/> em runtime.
    /// </summary>
    public sealed class GoToPointNpc : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string characterName = "GoToPointNpc";

        [Header("Configuração")]
        [Tooltip("Destino para onde o NPC vai se mover ao iniciar.")]
        [SerializeField] private Transform destination;

        private NpcMovementController _controller;

        private void Awake()
        {
            _controller = GetComponent<NpcMovementController>();
        }

        private async void Start()
        {
            if (destination == null)
            {
                Debug.LogWarning($"[GoToPointNpc] '{characterName}' sem destino configurado. Iniciando em FreeBehavior.");

                await _controller.SetStrategy(
                    new FreeBehavior(),
                    new FreeBehaviorContext(_controller, _controller.NavMesh, _controller.Adapter, transform, characterName));

                return;
            }

            await SendTo(destination.position);
        }

        /// <summary>
        /// Envia o NPC para um novo destino em runtime.
        /// </summary>
        public async void SetDestination(Vector3 point)
        {
            await SendTo(point);
        }

        private async System.Threading.Tasks.Task SendTo(Vector3 point)
        {
            var ctx = new GoToPointBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                characterName,
                perceptionRadius: 0f,
                target:           null,
                point,
                onArrived: () => Debug.Log($"[GoToPointNpc] '{characterName}' chegou ao destino."));

            await _controller.SetStrategy(new GoToPointBehavior(), ctx);
        }
    }
}
