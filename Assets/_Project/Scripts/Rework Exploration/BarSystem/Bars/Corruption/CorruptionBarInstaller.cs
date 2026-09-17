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
    /// Corruption bar: grows automatically (GrowthOverTimeBehavior) and/or through
    /// external events (AddCorruption), and notifies when it crosses thresholds
    /// (e.g., to trigger negative effects, mutations, NPC dialogue, etc.).
    ///
    /// O crescimento automático só atua enquanto o alvo estiver FORA do Hub
    /// (SafeArea) configurado - dentro da área segura, o Tick de crescimento
    /// é simplesmente pulado. A view (slider + texto) mostra apenas até
    /// metade do valor real (Max real = 100 → view "cheia" com Current = 50),
    /// enquanto o BarModel interno continua crescendo normalmente até o Max real.
    /// </summary>
    public class CorruptionBarInstaller : MonoBehaviour
    {
        [SerializeField] private BarConfigSO _config;
        [SerializeField] private UISliderBarView _sliderView;
        [SerializeField] private UITextBarView _textView;

        [Header("Área segura (crescimento pausa enquanto o alvo está dentro)")]
        [SerializeField] private Hub _hub;

        [Header("Automatic Growth (Optional)")]
        [SerializeField] private bool _growOverTime = true;
        [SerializeField] private float _growthPerSecond = 0.5f;

        [Header("Smoothing (Optional)")]
        [SerializeField] private bool _useSmoothing = true;
        [SerializeField] private float _smoothingSpeed = 4f;

        [Header("View scale (view enche com uma fração do valor real)")]
        [Tooltip("2 = a view fica cheia quando o valor real atinge metade do Max.")]
        [SerializeField] private float _viewScale = 2f;

        private BarController _controller;
        private SmoothedBarView _smoothedView;
        private IBarStateRepository _repository;
        private bool _isPlayerInsideSafeArea;
        private int _currentTier;

        public BarModel Model { get; private set; }

        /// <summary>Disparado sempre que o valor de corrupção cruza para um novo tier (via CorruptionTierCalculator).</summary>
        public event Action<int> OnTierChanged;

        /// <summary>Tier atual, calculado a partir do valor corrente do modelo.</summary>
        public int CurrentTier => _currentTier;

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

            IBarView view = _textView != null
                ? new CompositeBarView(_sliderView, _textView)
                : (IBarView)_sliderView;

            if (_useSmoothing)
            {
                _smoothedView = new SmoothedBarView(view, _smoothingSpeed);
                view = _smoothedView;
            }

            // Fica por fora de tudo: reescala o normalizedValue (real) antes
            // de chegar no smoothing/slider/texto, fazendo a view encher
            // com apenas uma fração (por padrão, metade) do valor real.
            view = new HalfScaleBarView(view, _viewScale);

            _controller.AddView(view);

            _currentTier = CorruptionTierCalculator.GetTier(Model.Current);
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

            // Sincroniza com o estado atual do Hub, caso o alvo já esteja
            // dentro/fora antes deste componente assinar os eventos.
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

            _repository.Save(new BarSaveData(Model.Id, Model.Current, Model.Max));
            _controller.Dispose();
        }

        private void Update()
        {
            // Dentro da área segura, o crescimento (GrowthOverTimeBehavior)
            // não deve atuar - o Tick é simplesmente pulado. AddCorruption/
            // ReduceCorruption continuam funcionando, pois não dependem deste Tick.
            if (!_isPlayerInsideSafeArea)
            {
                _controller.Tick(Time.deltaTime);
            }

            // A view (incluindo o smoothing) continua sendo atualizada
            // sempre, mesmo com o crescimento pausado.
            _smoothedView?.Tick(Time.deltaTime);
        }

        public void AddCorruption(float amount) => Model.ApplyDelta(amount);
        public void ReduceCorruption(float amount) => Model.ApplyDelta(-amount);

        private void HandlePlayerEnteredSafeArea() => _isPlayerInsideSafeArea = true;
        private void HandlePlayerExitedSafeArea() => _isPlayerInsideSafeArea = false;

        private void HandleValueChangedForTier(float _)
        {
            int newTier = CorruptionTierCalculator.GetTier(Model.Current);
            if (newTier == _currentTier)
            {
                return;
            }

            _currentTier = newTier;
            OnTierChanged?.Invoke(_currentTier);
        }
    }
}