using Core.Exploration.Character.Movement;
using Core.Exploration.Character.NPC;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Presets
{
    /// <summary>
    /// NPC que mantém o alvo dentro de uma banda de distância [MinDistance, MaxDistance].
    /// Avança se o alvo se afastar demais, recua se o alvo se aproximar demais.
    ///
    /// Ao perder o alvo dispara <see cref="OnTargetLost"/>.
    /// Enquanto observa (dentro da banda) dispara <see cref="OnObserving"/> a cada tick.
    ///
    /// O que fazer após esses eventos é responsabilidade do sistema externo.
    ///
    /// Setup: adicione este componente ao GameObject junto com NavMeshAgent e NavMeshAgentAdapter.
    /// </summary>
    public sealed class StalkingNpc : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string characterName = "StalkingNpc";

        [Header("Alvo")]
        [SerializeField] private Transform target;

        [Header("Configuração")]
        [Tooltip("Raio de percepção. O alvo é considerado perdido ao sair deste raio.")]
        [SerializeField] private float perceptionRadius = 20f;

        [Tooltip("Distância mínima do alvo — abaixo disso o NPC recua.")]
        [SerializeField] private float minDistance = 4f;

        [Tooltip("Distância máxima do alvo — acima disso o NPC avança.")]
        [SerializeField] private float maxDistance = 8f;

        // ── Eventos públicos para sistemas externos ───────────────────────

        /// <summary>Disparado quando o alvo sai do raio de percepção.</summary>
        public event System.Action OnTargetLost;

        /// <summary>Disparado a cada tick enquanto o NPC está dentro da banda (observando).</summary>
        public event System.Action OnObserving;

        private NpcMovementController _controller;

        private void Awake()
        {
            _controller = GetComponent<NpcMovementController>();
        }

        private async void Start()
        {
            if (target == null)
            {
                Debug.LogWarning($"[StalkingNpc] '{characterName}' sem alvo. Iniciando em FreeBehavior.");

                await _controller.SetStrategy(
                    new FreeBehavior(),
                    new FreeBehaviorContext(_controller, _controller.NavMesh, _controller.Adapter, transform, characterName));

                return;
            }

            await BeginStalking();
        }

        /// <summary>Define um novo alvo e reinicia o stalking.</summary>
        public async void SetTarget(Transform newTarget)
        {
            target = newTarget;
            await BeginStalking();
        }

        private async System.Threading.Tasks.Task BeginStalking()
        {
            var ctx = new StalkingBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                target,
                characterName,
                perceptionRadius,
                minDistance,
                maxDistance,
                onTargetLost: () => OnTargetLost?.Invoke(),
                onObserving:  () => OnObserving?.Invoke());

            await _controller.SetStrategy(new StalkingBehavior(), ctx);
        }
    }
}
