using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem
{
    /// <summary>
    /// Sensor de detecção: varre periodicamente uma OverlapSphere ao redor
    /// do instigador atual (via IInstigatorProvider) em busca de
    /// IInteractable, escolhe o candidato disponível mais próximo como alvo
    /// atual e dispara a interação quando a Input Action configurada for
    /// performed — igual ao InputActionButtonTrigger, mas o "onTriggered" já
    /// é o próprio TryInteract() deste sensor.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionSensor : MonoBehaviour
    {
        [Header("Instigador")]
        [RequireInterface(typeof(IInstigatorProvider))]
        [SerializeField] private UnityEngine.Object instigatorProviderSource;

        [Header("Detecção (OverlapSphere)")]
        [SerializeField] private float radius = 2f;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [Tooltip("Intervalo entre varreduras, em segundos.")]
        [SerializeField] private float scanInterval = 0.1f;
        [SerializeField] private int maxColliders = 16;

        [Header("Input")]
        [Tooltip("Action cujo 'performed' dispara TryInteract() com o alvo atual.")]
        [SerializeField] private InputActionReference interactActionReference;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.35f);
        [SerializeField] private Color gizmoTargetColor = new Color(0.2f, 1f, 0.3f, 0.6f);

        private IInstigatorProvider _instigatorProvider;
        private Collider[] _overlapBuffer;
        private float _nextScanTime;

        public IInteractable CurrentTarget { get; private set; }

        public event Action<IInteractable> TargetChanged;
        public event Action<IInteractionContext> InteractionPerformed;

        private void Awake()
        {
            _instigatorProvider = instigatorProviderSource as IInstigatorProvider;
            _overlapBuffer = new Collider[Mathf.Max(1, maxColliders)];

            if (_instigatorProvider == null)
            {
                Debug.LogError($"{nameof(InteractionSensor)}: instigatorProviderSource não implementa IInstigatorProvider.", this);
            }
        }

        private void OnEnable()
        {
            if (interactActionReference == null || interactActionReference.action == null)
            {
                Debug.LogError($"{nameof(InteractionSensor)}: interactActionReference não atribuído.", this);
                return;
            }

            interactActionReference.action.Enable();
            interactActionReference.action.performed += HandleInteractPerformed;
        }

        private void OnDisable()
        {
            if (interactActionReference != null && interactActionReference.action != null)
            {
                interactActionReference.action.performed -= HandleInteractPerformed;
                interactActionReference.action.Disable();
            }

            SetTarget(null);
        }

        private void HandleInteractPerformed(InputAction.CallbackContext context) => TryInteract();

        private void Update()
        {
            if (_instigatorProvider?.Current == null)
            {
                SetTarget(null);
                return;
            }

            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + scanInterval;

            Scan();
        }

        private void Scan()
        {
            Vector3 origin = _instigatorProvider.Current.transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, _overlapBuffer, interactableMask, triggerInteraction);

            IInteractable best = null;
            float bestSqrDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col == null) continue;

                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null || !IsAvailable(interactable)) continue;
                if (ReferenceEquals(interactable, CurrentTarget)) continue; // <- exclui o alvo atual da escolha

                var interactableTransform = (interactable as Component)?.transform;
                if (interactableTransform == null) continue;

                float sqrDist = (interactableTransform.position - origin).sqrMagnitude;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    best = interactable;
                }
            }

            SetTarget(best);
        }

        /// <summary>
        /// Decide se um candidato detectado é elegível como CurrentTarget.
        /// Por padrão aceita qualquer IInteractable detectado fisicamente,
        /// mesmo com CanInteract == false no momento - quem impede a
        /// execução de uma interação indisponível é o próprio
        /// TryInteract(). Isso evita dependência circular quando o
        /// CanInteract de um interactable depende de ele mesmo ser o
        /// CurrentTarget (ex: PlayableNpcInteractable). Sobrescreva numa
        /// subclasse se seu jogo quiser esconder alvos indisponíveis do
        /// prompt de UI.
        /// </summary>
        protected virtual bool IsAvailable(IInteractable interactable) => true;

        private void SetTarget(IInteractable interactable)
        {
            if (ReferenceEquals(CurrentTarget, interactable)) return;

            CurrentTarget = interactable;
            TargetChanged?.Invoke(CurrentTarget);
        }

        private void HandleTargetAvailabilityChanged(bool available)
        {
            if (!available)
            {
                SetTarget(null);
            }
        }

        /// <summary>
        /// Dispara a interação com o alvo atual, se houver. Chamado pela
        /// Input Action, mas também exposto publicamente para testes
        /// (ex: botão do editor) sem precisar simular input real.
        /// </summary>
        public bool TryInteract()
        {
            if (CurrentTarget == null || _instigatorProvider?.Current == null) return false;

            bool success = CurrentTarget.TryInteract(_instigatorProvider.Current, out var context);
            if (success)
                InteractionPerformed?.Invoke(context);

            return success;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Vector3 origin = Application.isPlaying && _instigatorProvider?.Current != null
                ? _instigatorProvider.Current.transform.position
                : transform.position;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(origin, radius);

            if (Application.isPlaying && CurrentTarget is Component targetComponent)
            {
                Gizmos.color = gizmoTargetColor;
                Gizmos.DrawWireSphere(targetComponent.transform.position, 0.3f);
                Gizmos.DrawLine(origin, targetComponent.transform.position);
            }
        }
    }
}