using UnityEngine;
using BarSystem.Core;
using BarSystem.Config;
using BarSystem.Persistence;
using BarSystem.Behaviors;
using BarSystem.View;

namespace BarSystem.Bars.Health
{
    /// <summary>
    /// Builds a health bar from generic components: BarModel +
    /// RegenBehavior (optional) + ThresholdNotifierBehavior (low health) +
    /// UISliderBarView (with optional smoothing). No drawing or state logic
    /// is reimplemented here — this class only handles composition.
    /// </summary>
    public class HealthBarInstaller : MonoBehaviour
    {
        [SerializeField] private BarConfigSO _config;
        [SerializeField] private UISliderBarView _sliderView;

        [Header("Regeneration (Optional)")]
        [SerializeField] private bool _useRegen = false;
        [SerializeField] private float _regenPerSecond = 2f;

        [Header("Smoothing (Optional)")]
        [SerializeField] private bool _useSmoothing = true;
        [SerializeField] private float _smoothingSpeed = 6f;

        private BarController _controller;
        private SmoothedBarView _smoothedView;
        private IBarStateRepository _repository;

        public BarModel Model { get; private set; }

        private void Awake()
        {
            _repository = new JsonFileBarStateRepository();

            BarSaveData saved = _repository.Load(_config.Id);
            float max = saved?.Max ?? _config.MaxDefault;
            float current = saved?.Current ?? _config.CurrentDefault;

            Model = new BarModel(_config.Id, _config.MinDefault, max, current);
            _controller = new BarController(Model);

            if (_useRegen)
                _controller.AddBehavior(new RegenBehavior(_regenPerSecond));

            IBarView view = _sliderView;
            if (_useSmoothing)
            {
                _smoothedView = new SmoothedBarView(_sliderView, _smoothingSpeed);
                view = _smoothedView;
            }

            _controller.AddView(view);
        }

        private void Update()
        {
            _controller.Tick(Time.deltaTime);
            _smoothedView?.Tick(Time.deltaTime);
        }

        public void ApplyDamage(float amount) => Model.ApplyDelta(-amount);
        public void ApplyHeal(float amount) => Model.ApplyDelta(amount);

        private void OnDisable()
        {
            _repository.Save(new BarSaveData(Model.Id, Model.Current, Model.Max));
            _controller.Dispose();
        }
    }
}