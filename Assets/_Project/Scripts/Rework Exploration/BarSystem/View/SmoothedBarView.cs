using System;
using BarSystem.Core;

namespace BarSystem.View
{
    /// <summary>
    /// Decorator that smooths value changes using exponential interpolation
    /// before passing them to the real view, so the "dumb" view does not need
    /// to know about it. This is not a MonoBehaviour: it needs to be ticked by
    /// something with an update loop (normally the specific bar Installer).
    /// </summary>
    public class SmoothedBarView : IBarView
    {
        private readonly IBarView _inner;
        private readonly float _speed;

        private float _target;
        private float _current;
        private bool _initialized;

        /// <param name="inner">The real view that actually renders the bar.</param>
        /// <param name="speed">The higher the value, the faster the bar reaches the target value.</param>
        public SmoothedBarView(IBarView inner, float speed = 5f)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _speed = speed;
        }

        public void SetNormalizedValue(float normalizedValue)
        {
            _target = normalizedValue;

            if (!_initialized)
            {
                _current = normalizedValue;
                _initialized = true;
                _inner.SetNormalizedValue(_current);
            }
        }

        /// <summary>
        /// Call every frame (e.g., Update) to animate the transition toward the target value.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Math.Abs(_current - _target) < 0.0001f)
                return;

            float t = 1f - (float)Math.Exp(-_speed * deltaTime);
            _current += (_target - _current) * t;
            _inner.SetNormalizedValue(_current);
        }
    }
}