using System;
using InteractionSystem.Bridges;
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Detecta se o instigador atual (via IInstigatorProvider) está dentro
    /// de um raio ao redor deste GameObject, usando trigger de collider em
    /// vez de polling por distância - mesmo padrão do Hub/SafeArea do
    /// projeto, generalizado aqui para não depender de
    /// PlayableCharacterController.
    ///
    /// Não consulta CanInteract de ninguém - é só um sinal físico de
    /// "o instigador está perto", então não tem risco de dependência
    /// circular com um IInteractable que use esse sinal para decidir sua
    /// própria disponibilidade.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class InstigatorProximityZone : MonoBehaviour
    {
        [Header("Instigador")]
        [RequireInterface(typeof(IInstigatorProvider))]
        [SerializeField] private UnityEngine.Object instigatorProviderSource;

        [Header("Alcance")]
        [SerializeField] private float radius = 2f;

        private IInstigatorProvider _instigatorProvider;
        private SphereCollider _triggerCollider;
        private GameObject _currentInstigator;
        private bool _isInstigatorInside;

        /// <summary>Estado atual, para consulta direta sem precisar assinar os eventos.</summary>
        public bool IsInstigatorInRange => _isInstigatorInside;

        /// <summary>Disparado quando o instigador passa a estar dentro do raio.</summary>
        public event Action InstigatorEnteredRange;

        /// <summary>Disparado quando o instigador deixa de estar dentro do raio.</summary>
        public event Action InstigatorExitedRange;

        private void Awake()
        {
            _instigatorProvider = instigatorProviderSource as IInstigatorProvider;
            _triggerCollider = GetComponent<SphereCollider>();
            SyncCollider();


        }

        private void OnEnable()
        {
            if (_instigatorProvider == null)
            {
                _instigatorProvider = FindAnyObjectByType<PlayableCharacterInstigatorProvider>();
            }

            _instigatorProvider.InstigatorChanged += HandleInstigatorChanged;
            HandleInstigatorChanged(_instigatorProvider.Current);

            if (_instigatorProvider == null)
            {
                Debug.LogError($"{nameof(InstigatorProximityZone)}: instigatorProviderSource não implementa IInstigatorProvider.", this);
            }
        }

        private void OnDisable()
        {
            if (_instigatorProvider != null)
            {
                _instigatorProvider.InstigatorChanged -= HandleInstigatorChanged;
            }
        }

        private void HandleInstigatorChanged(GameObject newInstigator)
        {
            _currentInstigator = newInstigator;

            // O trigger físico só dispara em cruzamento de fronteira. Se o
            // instigador mudou enquanto o novo já estava fisicamente dentro
            // (ou o antigo continua dentro, mas não é mais o instigador),
            // nenhum evento de física ocorre - precisa reconciliar manualmente.
            ReevaluateContainment();
        }

        private void ReevaluateContainment()
        {
            if (_currentInstigator == null)
            {
                SetInsideState(false);
                return;
            }

            float sqrDist = (transform.position - _currentInstigator.transform.position).sqrMagnitude;
            SetInsideState(sqrDist <= radius * radius);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsInstigator(other)) SetInsideState(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsInstigator(other)) SetInsideState(false);
        }

        private bool IsInstigator(Collider other)
        {
            return _currentInstigator != null
                && (other.transform == _currentInstigator.transform || other.transform.IsChildOf(_currentInstigator.transform));
        }

        /// <summary>
        /// ÚNICO ponto que altera _isInstigatorInside e dispara os eventos -
        /// tanto OnTriggerEnter/Exit (física) quanto ReevaluateContainment
        /// (troca de instigador) passam por aqui. Idempotente.
        /// </summary>
        private void SetInsideState(bool isInside)
        {
            if (isInside == _isInstigatorInside) return;
            _isInstigatorInside = isInside;

            if (isInside) InstigatorEnteredRange?.Invoke();
            else InstigatorExitedRange?.Invoke();
        }

        private void OnValidate()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<SphereCollider>();
            }

            SyncCollider();
        }

        private void SyncCollider()
        {
            if (_triggerCollider == null) return;

            _triggerCollider.isTrigger = true;
            _triggerCollider.radius = radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}