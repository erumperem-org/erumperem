using Core.Exploration.Character.Movement;
using Core.Exploration.Character.NPC;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Presets
{
    public sealed class PursuingNpc : MonoBehaviour
    {
        [Header("Identificação")]
        [SerializeField] private string characterName = "PursuingNpc";

        [Header("Alvo")]
        [SerializeField] private Transform target;

        [Header("Configuração")]
        [Tooltip("Raio de percepção. O alvo é considerado perdido ao sair deste raio.")]
        [SerializeField] private float perceptionRadius = 15f;

        [Tooltip("Intervalo em segundos entre cada verificação de percepção.")]
        [SerializeField] private float perceptionCheckInterval = 0.25f;

        private NpcMovementController _controller;
        private CancellationTokenSource _perceptionCts;

        // Disparado quando o NPC alcança o alvo (a ser assinado externamente).
        public System.Action OnTargetReached;

        // Disparado quando o alvo sai do raio de percepção.
        public System.Action OnTargetLost;

        private void Awake()
        {
            _controller = GetComponent<NpcMovementController>();
        }

        private async void Start()
        {
            if (target == null)
            {
                Debug.LogWarning($"[PursuingNpc] '{characterName}' sem alvo. Iniciando em FreeBehavior.");

                await _controller.SetStrategy(
                    new FreeBehavior(),
                    new FreeBehaviorContext(_controller, _controller.NavMesh, _controller.Adapter, transform, characterName));

                return;
            }

            await BeginPursuit();
        }

        private void OnDestroy()
        {
            StopPerceptionLoop();
        }

        /// <summary>Define um novo alvo e reinicia a perseguição.</summary>
        public async void SetTarget(Transform newTarget)
        {
            target = newTarget;
            await BeginPursuit();
        }

        private async Task BeginPursuit()
        {
            // Cancela o monitoramento anterior antes de iniciar um novo.
            StopPerceptionLoop();

            var ctx = new PursuingBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                target,
                characterName,
                perceptionRadius);

            await _controller.SetStrategy(new PursuingBehavior(), ctx);

            // Só inicia o loop se ainda estamos em perseguição ativa.
            if (target != null)
                StartPerceptionLoop();
        }

        private async Task StopPursuit()
        {
            // Cancela o monitoramento anterior antes de iniciar um novo.
            StopPerceptionLoop();

            var ctx = new FreeBehaviorContext(
                _controller,
                _controller.NavMesh,
                _controller.Adapter,
                transform,
                characterName);

            await _controller.SetStrategy(new FreeBehavior(), ctx);
        }
        
        // ── Monitoramento de percepção ─────────────────────────────────────

        private void StartPerceptionLoop()
        {
            _perceptionCts = new CancellationTokenSource();
            _ = PerceptionLoopAsync(_perceptionCts.Token);
        }

        private void StopPerceptionLoop()
        {
            if (_perceptionCts == null) return;
            _perceptionCts.Cancel();
            _perceptionCts.Dispose();
            _perceptionCts = null;
        }

        /// <summary>
        /// Verifica periodicamente se o alvo saiu do raio de percepção.
        /// Cancela automaticamente ao trocar de comportamento via <see cref="StopPerceptionLoop"/>.
        /// </summary>
        private async Task PerceptionLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        Mathf.RoundToInt(perceptionCheckInterval * 1000),
                        token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (target == null || token.IsCancellationRequested)
                    return;

                float distance = Vector3.Distance(transform.position, target.position);

                if (distance > perceptionRadius)
                {
                    Debug.Log($"[PursuingNpc] '{characterName}' perdeu o alvo (dist: {distance:F1} > raio: {perceptionRadius}).");
                    OnTargetLost?.Invoke();
                    await StopPursuit();
                    return;
                }
            }
        }
    }
}