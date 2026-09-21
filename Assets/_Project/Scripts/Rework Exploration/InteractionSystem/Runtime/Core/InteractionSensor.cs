using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InteractionSystem
{
    /// <summary>
    /// Sensor de detecção: mantém um SphereCollider (trigger) que segue a
    /// posição do instigador atual (via IInstigatorProvider) a cada frame.
    /// A lista de candidatos é construída incrementalmente via
    /// OnTriggerEnter/OnTriggerExit (evento de física, não polling), e o
    /// alvo atual é o candidato disponível mais próximo dentro dessa lista.
    ///
    /// Troca de técnica em relação à versão anterior (OverlapSphere
    /// periódico): evita dois problemas dela - (1) buffer fixo que podia
    /// encher com colliders irrelevantes antes de incluir o interactable de
    /// verdade, fazendo o alvo real simplesmente não aparecer; (2) custo de
    /// varrer TODOS os colliders da esfera a cada intervalo, mesmo quando
    /// nada mudou. Aqui só existe trabalho quando algo entra/sai do raio.
    ///
    /// Requer um Rigidbody neste GameObject (kinematic, sem gravidade) -
    /// eventos de trigger só disparam se pelo menos um dos lados da
    /// colisão tiver Rigidbody.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class InteractionSensor : MonoBehaviour
    {
        [Header("Instigador")]
        [RequireInterface(typeof(IInstigatorProvider))]
        [SerializeField] private UnityEngine.Object instigatorProviderSource;

        [Header("Detecção (Trigger)")]
        [SerializeField] private float radius = 2f;
        [Tooltip("Filtro adicional por layer, além da collision matrix do projeto (Project Settings > Physics).")]
        [SerializeField] private LayerMask interactableMask = ~0;
        [Tooltip("Intervalo entre reavaliações de 'qual candidato é o mais próximo'. A detecção em si (entrar/sair do raio) é sempre imediata, isso só limita a frequência do recálculo de distância.")]
        [SerializeField] private float reevaluateInterval = 0.05f;

        [Header("Input")]
        [Tooltip("Action cujo 'performed' dispara TryInteract() com o alvo atual.")]
        [SerializeField] private InputActionReference interactActionReference;

        [Header("Gizmos")]
        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0.2f, 0.8f, 1f, 0.35f);
        [SerializeField] private Color gizmoTargetColor = new Color(0.2f, 1f, 0.3f, 0.6f);

        private IInstigatorProvider _instigatorProvider;
        private SphereCollider _triggerCollider;
        private Rigidbody _rigidbody;
        private readonly HashSet<IInteractable> _candidates = new HashSet<IInteractable>();
        private float _nextReevaluateTime;

        public IInteractable CurrentTarget { get; private set; }

        /// <summary>Disparado quando o alvo detectado muda (útil para UI de prompt "Pressione E").</summary>
        public event Action<IInteractable> TargetChanged;

        /// <summary>Disparado quando uma interação é efetivamente executada com sucesso.</summary>
        public event Action<IInteractionContext> InteractionPerformed;

        private void Awake()
        {
            _instigatorProvider = instigatorProviderSource as IInstigatorProvider;

            _triggerCollider = GetComponent<SphereCollider>();
            _triggerCollider.isTrigger = true;
            _triggerCollider.radius = radius;

            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;

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
            }
            else
            {
                interactActionReference.action.Enable();
                interactActionReference.action.performed += HandleInteractPerformed;
            }

            _candidates.Clear();
            SetTarget(null);
        }

        private void OnDisable()
        {
            if (interactActionReference != null && interactActionReference.action != null)
            {
                interactActionReference.action.performed -= HandleInteractPerformed;
                interactActionReference.action.Disable();
            }

            _candidates.Clear();
            SetTarget(null);
        }

        private void HandleInteractPerformed(InputAction.CallbackContext context) => TryInteract();

        private void Update()
        {
            if (_instigatorProvider?.Current == null)
            {
                _candidates.Clear();
                SetTarget(null);
                return;
            }

            // O trigger segue o instigador - é isso que substitui o
            // OverlapSphere periódico: a física do próprio Unity cuida de
            // gerar OnTriggerEnter/Exit conforme o sensor se move.
            transform.position = _instigatorProvider.Current.transform.position;

            if (Time.time < _nextReevaluateTime) return;
            _nextReevaluateTime = Time.time + reevaluateInterval;

            PickNearestCandidate();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOnMask(other)) return;
            if (IsInstigatorCollider(other)) return;

            var interactable = other.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            _candidates.Add(interactable);
        }

        private void OnTriggerExit(Collider other)
        {
            var interactable = other.GetComponentInParent<IInteractable>();
            if (interactable == null) return;

            _candidates.Remove(interactable);

            if (ReferenceEquals(interactable, CurrentTarget))
            {
                PickNearestCandidate();
            }
        }

        private bool IsOnMask(Collider other) => (interactableMask.value & (1 << other.gameObject.layer)) != 0;

        private bool IsInstigatorCollider(Collider other)
        {
            var instigatorTransform = _instigatorProvider?.Current?.transform;
            return instigatorTransform != null
                && (other.transform == instigatorTransform || other.transform.IsChildOf(instigatorTransform));
        }

        private void PickNearestCandidate()
        {
            Vector3 origin = transform.position;
            Transform instigatorTransform = _instigatorProvider?.Current?.transform;

            IInteractable best = null;
            float bestSqrDist = float.MaxValue;

            foreach (var interactable in _candidates)
            {
                if (!IsAvailable(interactable)) continue;

                var interactableTransform = (interactable as Component)?.transform;
                if (interactableTransform == null) continue;

                // Um candidato que virou o próprio instigador (ex: foi promovido a
                // InGame) não pode ser seu próprio alvo. Isso pode acontecer mesmo
                // sem OnTriggerExit, já que a troca de papel não move fisicamente
                // ninguém, então ele permanece em _candidates.
                if (instigatorTransform != null &&
                    (interactableTransform == instigatorTransform || interactableTransform.IsChildOf(instigatorTransform)))
                {
                    continue;
                }

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
        /// Decide se um candidato na lista é elegível como CurrentTarget.
        /// Por padrão aceita qualquer IInteractable dentro do raio, mesmo
        /// com CanInteract == false no momento - quem impede a execução de
        /// uma interação indisponível é o próprio TryInteract(). Evita
        /// dependência circular quando o CanInteract de um interactable
        /// depende de ele mesmo ser o CurrentTarget (ex:
        /// PlayableNpcInteractable). Sobrescreva numa subclasse se seu
        /// jogo quiser esconder alvos indisponíveis do prompt de UI.
        /// </summary>
        protected virtual bool IsAvailable(IInteractable interactable) => true;

        private void SetTarget(IInteractable interactable)
        {
            if (ReferenceEquals(CurrentTarget, interactable)) return;

            CurrentTarget = interactable;
            TargetChanged?.Invoke(CurrentTarget);
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

        private void OnValidate()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<SphereCollider>();
            }

            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
                _triggerCollider.radius = radius;
            }
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