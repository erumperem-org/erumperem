using UnityEngine;

namespace InteractionSystem.Concrete
{
    /// <summary>
    /// Fica disponível apenas quando TODAS as condições abaixo são
    /// verdadeiras: não é o próprio instigador atual, o instigador está
    /// dentro da safe area do Hub, e este NPC é o alvo atual do
    /// InteractionSensor da cena.
    ///
    /// Requer que InteractionSensor.IsAvailable() não filtre por
    /// CanInteract - senão, assim que este NPC ficar indisponível uma vez,
    /// nunca mais volta a ser elegível como CurrentTarget.
    /// </summary>
    public class PlayableNpcInteractable : InteractableBase
    {
        [Header("Instigador")]
        [RequireInterface(typeof(IInstigatorProvider))]
        [SerializeField] private UnityEngine.Object instigatorProviderSource;

        [Header("Safe Area")]
        [Tooltip("Hub cuja safe area precisa conter o instigador atual para este NPC ficar disponível.")]
        [SerializeField] private Hub hub;

        [Header("Sensor")]
        [Tooltip("Se vazio, tenta achar um InteractionSensor na cena automaticamente.")]
        [SerializeField] private InteractionSensor sensor;

        private IInstigatorProvider _instigatorProvider;
        private bool _isCurrentInstigator;
        private bool _isInsideSafeArea;

        protected override void Awake()
        {
            base.Awake();

            _instigatorProvider = instigatorProviderSource as IInstigatorProvider;

            if (_instigatorProvider == null)
                Debug.LogError($"{nameof(PlayableNpcInteractable)}: instigatorProviderSource não implementa IInstigatorProvider.", this);
            if (hub == null)
                Debug.LogError($"{nameof(PlayableNpcInteractable)}: hub não atribuído.", this);

            if (sensor == null)
                sensor = FindAnyObjectByType<InteractionSensor>();

            if (sensor == null)
                Debug.LogError($"{nameof(PlayableNpcInteractable)}: nenhum InteractionSensor encontrado.", this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            if (_instigatorProvider != null)
                _instigatorProvider.InstigatorChanged += HandleInstigatorChanged;

            if (hub != null)
            {
                hub.OnPlayerEnteredSafeArea += HandleEnteredSafeArea;
                hub.OnPlayerExitedSafeArea += HandleExitedSafeArea;
                _isInsideSafeArea = hub.IsTargetInside;
            }
            _isCurrentInstigator = _instigatorProvider?.Current == gameObject;

            Reevaluate();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_instigatorProvider != null)
                _instigatorProvider.InstigatorChanged -= HandleInstigatorChanged;

            if (hub != null)
            {
                hub.OnPlayerEnteredSafeArea -= HandleEnteredSafeArea;
                hub.OnPlayerExitedSafeArea -= HandleExitedSafeArea;
            }
        }

        private void HandleInstigatorChanged(GameObject currentInstigator)
        {
            _isCurrentInstigator = currentInstigator == gameObject;
            Reevaluate();
        }

        private void HandleEnteredSafeArea()
        {
            _isInsideSafeArea = true;
            Reevaluate();
        }

        private void HandleExitedSafeArea()
        {
            _isInsideSafeArea = false;
            Reevaluate();
        }

        /// <summary>
        /// Único ponto que decide a disponibilidade final, combinando as
        /// três condições - evita que um handler sobrescreva o resultado
        /// dos outros ao chamar SetAvailable direto de cada um.
        /// </summary>
        private void Reevaluate()
        {
            SetAvailable(!_isCurrentInstigator && _isInsideSafeArea);
        }
    }
}