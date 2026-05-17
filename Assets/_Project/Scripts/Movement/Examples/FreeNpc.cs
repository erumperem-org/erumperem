using Core.Exploration.Character.Movement;
using Core.Exploration.Character.NPC;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Presets
{
    /// <summary>
    /// NPC em estado neutro: parado, sem executar nenhuma rotina de movimento.
    /// Útil como estado inicial, NPC decorativo ou ponto de chegada de outra rotina.
    ///
    /// Setup: adicione este componente ao GameObject junto com NavMeshAgent e NavMeshAgentAdapter.
    /// </summary>
    public sealed class FreeNpc : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string characterName = "FreeNpc";

        private NpcMovementController _controller;

        private void Awake()
        {
            _controller = GetComponent<NpcMovementController>();
        }

        private async void Start()
        {
            var ctx = new FreeBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                characterName);

            await _controller.SetStrategy(new FreeBehavior(), ctx);
        }
    }
}
