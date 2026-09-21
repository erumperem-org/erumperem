using System;
using UnityEngine;
using UnityEngine.Events;

namespace InteractionSystem
{
    [DisallowMultipleComponent]
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("Interaction State")]
        [Tooltip("Se a interação já nasce disponível.")]
        [SerializeField] private bool startAvailable = true;

        [Header("Integração Externa")]
        [SerializeField] private UnityEvent onInteractionStarted;
        [SerializeField] private UnityEvent onBecameAvailable;
        [SerializeField] private UnityEvent onBecameUnavailable;
        [SerializeField] private UnityEvent onInstigatorInRange;
        [SerializeField] private UnityEvent onInstigatorOutOfRange;

        [Header("Alcance (opcional)")]
        [Tooltip("Se atribuída, OnInstigatorInRange/OnInstigatorOutOfRange são chamados automaticamente ao entrar/sair do raio. Não faz nada sozinha - uma subclasse decide o que fazer sobrescrevendo os métodos.")]
        [SerializeField] private InstigatorProximityZone proximityZone;

        /// <summary>Lock interno, controlado apenas por SetAvailable.</summary>
        private bool _canInteract;

        /// <summary>
        /// Se a interação pode ser realizada agora. Por padrão só reflete o
        /// lock interno (controlado via SetAvailable), mas é virtual para
        /// que uma subclasse combine outra condição (ex: item necessário,
        /// linha de visão) sem duplicar TryInteract() nem os sensores, que
        /// já consultam esta propriedade.
        /// </summary>
        public virtual bool CanInteract => _canInteract;

        public event Action<bool> AvailabilityChanged;

        protected virtual void Awake()
        {
            _canInteract = startAvailable;
        }

        /// <summary>
        /// Assinatura dos eventos de InstigatorProximityZone. Subclasses que
        /// sobrescrevem OnEnable/OnDisable DEVEM chamar base.OnEnable()/
        /// base.OnDisable(), senão este wiring nunca roda.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (proximityZone != null)
            {
                proximityZone.InstigatorEnteredRange += HandleInstigatorEnteredRange;
                proximityZone.InstigatorExitedRange += HandleInstigatorExitedRange;

                if (proximityZone.IsInstigatorInRange) OnInstigatorInRange();
                else OnInstigatorOutOfRange();
            }
        }

        protected virtual void OnDisable()
        {
            if (proximityZone != null)
            {
                proximityZone.InstigatorEnteredRange -= HandleInstigatorEnteredRange;
                proximityZone.InstigatorExitedRange -= HandleInstigatorExitedRange;
            }
        }

        private void HandleInstigatorEnteredRange() => OnInstigatorInRange();
        private void HandleInstigatorExitedRange() => OnInstigatorOutOfRange();

        public bool TryInteract(GameObject instigator, out IInteractionContext context)
        {
            context = null;

            if (!CanInteract)
                return false;

            if (!CanInteractWith(instigator))
                return false;

            context = CreateContext(instigator);
            OnInteractionStarted(context);

            return true;
        }

        public void SetAvailable(bool available)
        {
            if (_canInteract == available) return;
            _canInteract = available;

            // O valor reportado é sempre CanInteract (virtual) - se uma
            // subclasse combina condição extra, o evento e os UnityEvents
            // refletem o estado efetivo, não só o lock interno.
            bool effective = CanInteract;

            AvailabilityChanged?.Invoke(effective);

            if (effective)
            {
                onBecameAvailable?.Invoke();
                OnAvailable();
            }
            else
            {
                onBecameUnavailable?.Invoke();
                OnUnavailable();
            }
        }

        protected virtual bool CanInteractWith(GameObject instigator) => true;

        protected virtual IInteractionContext CreateContext(GameObject instigator)
        {
            return new InteractionContext(this, instigator);
        }

        protected virtual void OnInteractionStarted(IInteractionContext context)
        {
            onInteractionStarted?.Invoke();
        }

        /// <summary>Chamado quando CanInteract passa a true. Também dispara o UnityEvent onBecameAvailable.</summary>
        protected virtual void OnAvailable() { }

        /// <summary>Chamado quando CanInteract passa a false. Também dispara o UnityEvent onBecameUnavailable.</summary>
        protected virtual void OnUnavailable() { }

        /// <summary>Chamado quando o instigador entra no raio de proximityZone (se atribuída). Também dispara o UnityEvent onInstigatorInRange.</summary>
        protected virtual void OnInstigatorInRange()
        {
            onInstigatorInRange?.Invoke();
        }

        /// <summary>Chamado quando o instigador sai do raio de proximityZone (se atribuída). Também dispara o UnityEvent onInstigatorOutOfRange.</summary>
        protected virtual void OnInstigatorOutOfRange()
        {
            onInstigatorOutOfRange?.Invoke();
        }
    }
}