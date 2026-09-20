using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Sensor genérico e reutilizável: fica "armado" assim que o
    /// interactable alvo se torna indisponível e, ao detectar que o
    /// instigador saiu do raio configurado, chama SetAvailable(true) nele.
    ///
    /// Usado por interactables que não sabem sozinhos quando "terminar" -
    /// ex: um baú cujo painel de inventário fica aberto até o jogador se
    /// afastar fisicamente.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProximityReactivationSensor : MonoBehaviour
    {
        [Header("Alvo")]
        [RequireInterface(typeof(IInteractable))]
        [SerializeField] private UnityEngine.Object interactableSource;

        [Header("Instigador")]
        [RequireInterface(typeof(IInstigatorProvider))]
        [SerializeField] private UnityEngine.Object instigatorProviderSource;

        [Header("Detecção (OverlapSphere)")]
        [Tooltip("Raio dentro do qual o instigador ainda é considerado 'próximo'.")]
        [SerializeField] private float exitRadius = 2.5f;
        [SerializeField] private LayerMask instigatorMask = ~0;
        [SerializeField] private float pollInterval = 0.2f;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.6f, 0f, 0.4f);

        private IInteractable _interactable;
        private IInstigatorProvider _instigatorProvider;
        private readonly Collider[] _overlapBuffer = new Collider[8];
        private bool _armed;
        private float _nextPollTime;

        private void Awake()
        {
            _interactable = interactableSource as IInteractable;
            _instigatorProvider = instigatorProviderSource as IInstigatorProvider;

            if (_interactable == null)
                Debug.LogError($"{nameof(ProximityReactivationSensor)}: interactableSource não implementa IInteractable.", this);
            if (_instigatorProvider == null)
                Debug.LogError($"{nameof(ProximityReactivationSensor)}: instigatorProviderSource não implementa IInstigatorProvider.", this);
        }

        private void OnEnable()
        {
            if (_interactable != null)
                _interactable.AvailabilityChanged += HandleAvailabilityChanged;
        }

        private void OnDisable()
        {
            if (_interactable != null)
                _interactable.AvailabilityChanged -= HandleAvailabilityChanged;
        }

        private void HandleAvailabilityChanged(bool available)
        {
            _armed = !available;
        }

        private void Update()
        {
            if (!_armed || _instigatorProvider?.Current == null) return;
            if (Time.time < _nextPollTime) return;
            _nextPollTime = Time.time + pollInterval;

            if (!InstigatorStillInRange())
            {
                _armed = false;
                _interactable.SetAvailable(true);
            }
        }

        private bool InstigatorStillInRange()
        {
            var instigatorTransform = _instigatorProvider.Current.transform;
            int count = Physics.OverlapSphereNonAlloc(transform.position, exitRadius, _overlapBuffer, instigatorMask);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (col != null && col.transform.IsChildOf(instigatorTransform))
                    return true;
            }

            return false;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, exitRadius);
        }
    }
}
