using System;
using UnityEngine;
using BarSystem.Core;
using BarSystem.Config;
using BarSystem.Persistence;
using BarSystem.Behaviors;
using BarSystem.View;
using Core.Rewards;

namespace BarSystem.Bars.Corruption
{
    /// <summary>
    /// Corruption bar: grows automatically while outside the Hub and decreases
    /// automatically while inside the Hub.
    /// 
    /// External changes through AddCorruption/ReduceCorruption continue to work
    /// regardless of the player's location.
    /// 
    /// The view displays only a fraction of the real value.
    /// By default, the view is full when the real value reaches half of Max.
    /// </summary>
    public class CorruptionBarInstaller : MonoBehaviour
    {
        [SerializeField] private BarConfigSO _config;
        [SerializeField] private UISliderBarView _sliderView;
        [SerializeField] private UITextBarView _textView;

        [Header("Safe Area")]
        [SerializeField] private Hub _hub;

        [Header("Automatic Growth")]
        [SerializeField] private bool _growOverTime = true;
        [SerializeField] private float _growthPerSecond = 0.5f;

        [Header("Automatic Reduction Inside Safe Area")]
        [SerializeField] private bool _reduceInsideSafeArea = true;
        [SerializeField] private float _reductionPerSecond = 0.5f;

        [Header("Smoothing")]
        [SerializeField] private bool _useSmoothing = true;
        [SerializeField] private float _smoothingSpeed = 4f;

        [Header("View Scale")]
        [Tooltip("2 = the view becomes full when the real value reaches half of Max.")]
        [SerializeField] private float _viewScale = 2f;

        private BarController _controller;
        private SmoothedBarView _smoothedView;
        private IBarStateRepository _repository;
        private bool _isPlayerInsideSafeArea;
        private int _currentTier;

        public BarModel Model { get; private set; }

        /// <summary>
        /// Invoked whenever the corruption value crosses into a new tier.
        /// </summary>
        public event Action<int> OnTierChanged;

        /// <summary>
        /// Current corruption tier.
        /// </summary>
        public int CurrentTier => _currentTier;

        private void Awake()
        {
            _repository = new JsonFileBarStateRepository();

            BarSaveData saved = _repository.Load(_config.Id);

            float max = saved?.Max ?? _config.MaxDefault;
            float current = saved?.Current ?? _config.CurrentDefault;

            Model = new BarModel(
                _config.Id,
                _config.MinDefault,
                max,
                current
            );

            _controller = new BarController(Model);

            if (_growOverTime)
            {
                _controller.AddBehavior(
                    new GrowthOverTimeBehavior(_growthPerSecond)
                );
            }

            IBarView view = _textView != null
                ? new CompositeBarView(_sliderView, _textView)
                : (IBarView)_sliderView;

            if (_useSmoothing)
            {
                _smoothedView = new SmoothedBarView(
                    view,
                    _smoothingSpeed
                );

                view = _smoothedView;
            }

            view = new HalfScaleBarView(
                view,
                _viewScale
            );

            _controller.AddView(view);

            _currentTier = CorruptionTierCalculator.GetTier(
                Model.Current
            );
        }

        private void OnEnable()
        {
            Model.OnValueChanged += HandleValueChangedForTier;

            if (_hub == null)
            {
                return;
            }

            _hub.OnPlayerEnteredSafeArea += HandlePlayerEnteredSafeArea;
            _hub.OnPlayerExitedSafeArea += HandlePlayerExitedSafeArea;

            // Synchronize with the current Hub state.
            _isPlayerInsideSafeArea = _hub.IsTargetInside;
        }

        private void OnDisable()
        {
            Model.OnValueChanged -= HandleValueChangedForTier;

            if (_hub != null)
            {
                _hub.OnPlayerEnteredSafeArea -= HandlePlayerEnteredSafeArea;
                _hub.OnPlayerExitedSafeArea -= HandlePlayerExitedSafeArea;
            }

            _repository.Save(
                new BarSaveData(
                    Model.Id,
                    Model.Current,
                    Model.Max
                )
            );

            _controller.Dispose();
        }

        private void Update()
        {
            if (_isPlayerInsideSafeArea)
            {
                // Inside the Hub, corruption decreases automatically.
                if (_reduceInsideSafeArea)
                {
                    Model.ApplyDelta(
                        -_reductionPerSecond * Time.deltaTime
                    );
                }
            }
            else
            {
                // Outside the Hub, the normal corruption growth continues.
                _controller.Tick(Time.deltaTime);
            }

            // The view continues updating regardless of the player's location.
            _smoothedView?.Tick(Time.deltaTime);
        }

        public void AddCorruption(float amount)
        {
            Model.ApplyDelta(amount);
        }

        public void ReduceCorruption(float amount)
        {
            Model.ApplyDelta(-amount);
        }

        private void HandlePlayerEnteredSafeArea()
        {
            _isPlayerInsideSafeArea = true;
        }

        private void HandlePlayerExitedSafeArea()
        {
            _isPlayerInsideSafeArea = false;
        }

        private void HandleValueChangedForTier(float _)
        {
            int newTier = CorruptionTierCalculator.GetTier(
                Model.Current
            );

            if (newTier == _currentTier)
            {
                return;
            }

            _currentTier = newTier;
            OnTierChanged?.Invoke(_currentTier);
        }
    }
}