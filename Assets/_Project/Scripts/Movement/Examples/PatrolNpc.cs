using Core.Exploration.Character.Movement;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Presets
{
    /// <summary>
    /// NPC que patrulha pontos aleatórios em torno de um centro fixo.
    /// O centro é a posição do próprio GameObject no Start, ou um Transform
    /// externo configurado via <see cref="patrolCenter"/>.
    ///
    /// Não possui percepção nem reage a alvos — patrulha pura.
    ///
    /// Setup: adicione este componente ao GameObject junto com NavMeshAgent e NavMeshAgentAdapter.
    /// </summary>
    public sealed class PatrolNpc : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string characterName = "PatrolNpc";

        [Header("Configuração")]
        [Tooltip("Centro da patrulha. Se vazio, usa a posição inicial do NPC.")]
        [SerializeField] private Transform patrolCenter;

        [Tooltip("Raio máximo em torno do centro da patrulha.")]
        [SerializeField] private float patrolRadius = 12f;

        private NpcMovementController _controller;

        private void Awake()
        {
            _controller = GetComponent<NpcMovementController>();
        }

        private async void Start()
        {
            Vector3 center = patrolCenter != null ? patrolCenter.position : transform.position;

            var ctx = new PatrolBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                target:          null,
                characterName,
                perceptionRadius: 0f,
                center,
                patrolRadius);

            await _controller.SetStrategy(new PatrolBehavior(), ctx);
        }
    }
}
