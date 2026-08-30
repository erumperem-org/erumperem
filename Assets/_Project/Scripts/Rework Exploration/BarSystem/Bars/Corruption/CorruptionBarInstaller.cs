using UnityEngine;
using BarSystem.Core;
using BarSystem.Config;
using BarSystem.Persistence;
using BarSystem.Behaviors;
using BarSystem.View;

namespace BarSystem.Bars.Corruption
{
    /// <summary>
    /// Corruption bar: grows automatically (GrowthOverTimeBehavior) and/or through
    /// external events (AddCorruption), and notifies when it crosses thresholds
    /// (e.g., to trigger negative effects, mutations, NPC dialogue, etc.).
    /// </summary>
    public class CorruptionBarInstaller : MonoBehaviour
    {
        [SerializeField] private BarConfigSO _config;
        [SerializeField] private UISliderBarView _sliderView;

        [Header("Automatic Growth (Optional)")]
        [SerializeField] private bool _growOverTime = true;
        [SerializeField] private float _growthPerSecond = 0.5f;

        [Header("Smoothing (Optional)")]
        [SerializeField] private bool _useSmoothing = true;
        [SerializeField] private float _smoothingSpeed = 4f;

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

            if (_growOverTime)
                _controller.AddBehavior(new GrowthOverTimeBehavior(_growthPerSecond));

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

        public void AddCorruption(float amount) => Model.ApplyDelta(amount);
        public void ReduceCorruption(float amount) => Model.ApplyDelta(-amount);

        private void OnDisable()
        {
            _repository.Save(new BarSaveData(Model.Id, Model.Current, Model.Max));
            _controller.Dispose();
        }
    }
}