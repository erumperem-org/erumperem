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

        public bool TryInteract(GameObject instigator, out IInteractionContext context)
        {
            context = null;

            if (!CanInteract)
                return false;

            if (!CanInteractWith(instigator))
                return false;

            context = CreateContext(instigator);
            SetAvailable(false);
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

            if (effective) onBecameAvailable?.Invoke();
            else onBecameUnavailable?.Invoke();
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
    }
}