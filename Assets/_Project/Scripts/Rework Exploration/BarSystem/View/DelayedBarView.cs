using System;
using UnityEngine;
using BarSystem.Core;

namespace BarSystem.View
{
    /// <summary>
    /// Configuration for how a <see cref="DelayedBarView"/> reacts to decreases and
    /// increases of the tracked bar. The two directions are fully independent: you can
    /// enable just one of them, both, or neither (in which case the view simply mirrors
    /// the source bar immediately, like a plain pass-through).
    /// </summary>
    [Serializable]
    public class DelayedBarReactionSettings
    {
        [Header("On Decrease (e.g. damage)")]
        [Tooltip("If true, decreases use Delay + Speed below. If false, decreases are applied immediately (no special feedback).")]
        public bool ReactToDecrease = true;

        [Tooltip("Seconds the bar holds its previous value before it starts following a decrease.")]
        [Min(0f)] public float DecreaseDelay = 0.4f;

        [Tooltip("Catch-up speed (normalized units/second) once the delay has elapsed. 0 or less = instant snap after the delay.")]
        public float DecreaseSpeed = 1.5f;

        [Header("On Increase (e.g. heal)")]
        [Tooltip("If true, increases use Delay + Speed below. If false, increases are applied immediately (no special feedback).")]
        public bool ReactToIncrease = false;

        [Tooltip("Seconds the bar holds its previous value before it starts following an increase.")]
        [Min(0f)] public float IncreaseDelay = 0f;

        [Tooltip("Catch-up speed (normalized units/second) once the delay has elapsed. 0 or less = instant snap after the delay.")]
        public float IncreaseSpeed = 6f;
    }

    /// <summary>
    /// Decorator view that follows another bar's normalized value independently, applying
    /// an optional delay + catch-up speed for decreases and/or increases.
    ///
    /// Typical use case: a "trail"/"ghost" bar behind the real one. The real bar (e.g. a
    /// UISliderBarView) updates instantly; this one waits a bit before draining down on
    /// damage (so the player can register how much was lost), and — if configured — reacts
    /// faster than normal when the value goes up (e.g. healing), by giving Increase a
    /// smaller/zero delay and a higher speed than Decrease.
    ///
    /// It implements <see cref="IBarView"/> so it plugs into a BarController exactly like
    /// any other view:
    ///
    ///     IBarView realView = _sliderView;
    ///     var delayedView = new DelayedBarView(_trailSliderView, _reactionSettings);
    ///     _controller.AddView(realView);
    ///     _controller.AddView(delayedView);
    ///
    /// Both views receive the same normalized value stream from the model, but this one
    /// decides internally when/how fast to actually forward it to the visual it wraps
    /// (<paramref name="innerView"/> in the constructor — the concrete slider/image that is
    /// really being animated).
    ///
    /// Since the View layer has no MonoBehaviour lifecycle of its own, this must be pumped
    /// every frame from the outside, the same way SmoothedBarView is:
    ///
    ///     private void Update()
    ///     {
    ///         _controller.Tick(Time.deltaTime);
    ///         _delayedView.Tick(Time.deltaTime);
    ///     }
    ///
    /// Extension point: subclass and override <see cref="ShouldReact"/>, <see cref="GetDelay"/>,
    /// <see cref="GetSpeed"/> and/or <see cref="OnTargetChanged"/> to customize the behavior
    /// per bar type (e.g. delay proportional to the size of the hit, easing curves, triggering
    /// SFX/VFX on change) without touching this base implementation.
    /// </summary>
    public class DelayedBarView : IBarView
    {
        private readonly IBarView _innerView;
        private readonly DelayedBarReactionSettings _settings;

        private float _target;
        private float _displayed;
        private float _delayTimer;
        private bool _hasValue;

        public DelayedBarView(IBarView innerView, DelayedBarReactionSettings settings)
        {
            _innerView = innerView ?? throw new ArgumentNullException(nameof(innerView));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Called whenever the tracked bar changes (same call the "real" view receives).
        /// This only records the new target; the actual animation happens in <see cref="Tick"/>.
        /// </summary>
        public void SetNormalizedValue(float normalizedValue)
        {
            normalizedValue = Mathf.Clamp01(normalizedValue);

            if (!_hasValue)
            {
                // Nothing to delay against yet (first value received): snap immediately.
                _hasValue = true;
                _target = normalizedValue;
                _displayed = normalizedValue;
                _delayTimer = 0f;
                _innerView.SetNormalizedValue(_displayed);
                return;
            }

            if (Mathf.Approximately(normalizedValue, _target))
                return;

            _target = normalizedValue;

            // A fresh change (new hit, new heal...) restarts the wait window, even if the
            // bar was already mid-animation from a previous change in the same direction.
            _delayTimer = 0f;

            OnTargetChanged(_target);
        }

        /// <summary>
        /// Advances the delay/catch-up animation by <paramref name="deltaTime"/>.
        /// Call this every frame from the owner's Update, after (or independently of)
        /// the controller's own Tick.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_hasValue || Mathf.Approximately(_displayed, _target))
                return;

            bool isDecrease = _target < _displayed;

            if (!ShouldReact(isDecrease))
            {
                // This direction has no special feedback configured: just mirror the source.
                _displayed = _target;
                _innerView.SetNormalizedValue(_displayed);
                return;
            }

            float delay = GetDelay(isDecrease);
            if (_delayTimer < delay)
            {
                _delayTimer += deltaTime;
                return;
            }

            float speed = GetSpeed(isDecrease);
            _displayed = speed <= 0f
                ? _target // instant snap once the delay has elapsed
                : (isDecrease
                    ? Mathf.Max(_target, _displayed - speed * deltaTime)
                    : Mathf.Min(_target, _displayed + speed * deltaTime));

            _innerView.SetNormalizedValue(_displayed);
        }

        /// <summary>
        /// Whether <paramref name="isDecrease"/> should use delay + speed feedback at all.
        /// Override to decide dynamically instead of relying purely on the static settings.
        /// </summary>
        protected virtual bool ShouldReact(bool isDecrease) =>
            isDecrease ? _settings.ReactToDecrease : _settings.ReactToIncrease;

        /// <summary>
        /// Delay (in seconds) applied before following the given direction.
        /// Override for variations, e.g. a delay proportional to the size of the change.
        /// </summary>
        protected virtual float GetDelay(bool isDecrease) =>
            isDecrease ? _settings.DecreaseDelay : _settings.IncreaseDelay;

        /// <summary>
        /// Catch-up speed (normalized units/second) for the given direction.
        /// Override for variations, e.g. an easing curve instead of a constant speed.
        /// </summary>
        protected virtual float GetSpeed(bool isDecrease) =>
            isDecrease ? _settings.DecreaseSpeed : _settings.IncreaseSpeed;

        /// <summary>
        /// Hook called whenever a new target value arrives, before the animation starts.
        /// Override to trigger SFX/VFX/haptics/etc. on change.
        /// </summary>
        protected virtual void OnTargetChanged(float newTarget) { }
    }
}