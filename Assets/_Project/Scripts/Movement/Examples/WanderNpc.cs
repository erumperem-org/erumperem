using Core.Exploration.Character.Movement;
using Core.Exploration.Character.NPC;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Presets
{
    /// <summary>
    /// NPC que caminha aleatoriamente a partir da sua posição atual.
    /// Não possui percepção nem reage a alvos — movimentação orgânica pura.
    ///
    /// Setup: adicione este componente ao GameObject junto com NavMeshAgent e NavMeshAgentAdapter.
    /// </summary>
    public sealed class WanderNpc : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string characterName = "WanderNpc";

        [Header("Configuração")]
        [Tooltip("Raio máximo de cada passo do wander a partir da posição atual.")]
        [SerializeField] private float wanderRadius = 10f;

        private NpcMovementController _controller;

        private void Awake()
        {
            _controller = GetComponent<NpcMovementController>();
        }

        private async void Start()
        {
            var ctx = new WanderBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                target:          null,
                characterName,
                perceptionRadius: 0f,
                wanderRadius,
                false,
                transform.position);

            await _controller.SetStrategy(new WanderBehavior(), ctx);
        }
    }
}
