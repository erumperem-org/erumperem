using UnityEngine;
using BarSystem.Core;
using BarSystem.Config;
using BarSystem.Behaviors;
using BarSystem.View;

namespace BarSystem.Bars.Stamina
{
    /// <summary>
    /// Stamina bar: on-demand consumption (DrainOnUseBehavior) +
    /// automatic regeneration only when not being used (conditional RegenBehavior).
    ///
    /// Stamina is intentionally NOT persisted: it always resets to its
    /// configured maximum when the scene loads and doesn't affect any other
    /// system across sessions - so, unlike HealthBarInstaller/
    /// CorruptionBarInstaller, no IBarStateRepository is used here.
    /// </summary>
    public class PlayableCharacterStaminaBarInstaller : MonoBehaviour
    {
        [SerializeField] private BarConfigSO _config;
        [SerializeField] private UISliderBarView _sliderView;

        [Header("Regeneration While Idle")]
        [SerializeField] private float _regenPerSecond = 10f;
        [SerializeField] private float _secondsBeforeRegen = 1f;

        [Header("Smoothing (Optional)")]
        [SerializeField] private bool _useSmoothing = true;
        [SerializeField] private float _smoothingSpeed = 8f;

        private BarController _controller;
        private DrainOnUseBehavior _drain;
        private SmoothedBarView _smoothedView;

        private float _timeSinceLastUse;

        public BarModel Model { get; private set; }

        private void Awake()
        {
            // Sem persistência: sempre começa no máximo configurado.
            Model = new BarModel(_config.Id, _config.MinDefault, _config.MaxDefault, _config.MaxDefault);
            _controller = new BarController(Model);

            _drain = new DrainOnUseBehavior();
            _controller.AddBehavior(_drain);

            _controller.AddBehavior(new RegenBehavior(
                _regenPerSecond,
                isActive: () => _timeSinceLastUse >= _secondsBeforeRegen));

            IBarView view = _sliderView;
            if (_useSmoothing)
            {
                _smoothedView = new SmoothedBarView(_sliderView, _smoothingSpeed);
                view = _smoothedView;
            }

            _controller.AddView(view);

            _timeSinceLastUse = _secondsBeforeRegen;
        }

        private void Update()
        {
            _timeSinceLastUse += Time.deltaTime;
            _controller.Tick(Time.deltaTime);
            _smoothedView?.Tick(Time.deltaTime);
        }

        /// <summary>
        /// Consumes stamina for an action (running, attacking, dodging...).
        /// </summary>
        public void Consume(float amount)
        {
            _drain.Consume(Model, amount);
            _timeSinceLastUse = 0f;
        }

        private void OnDisable()
        {
            _controller.Dispose();
        }
    }
}