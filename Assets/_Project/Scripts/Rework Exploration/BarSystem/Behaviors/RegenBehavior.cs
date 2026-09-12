using System;
using BarSystem.Core;

namespace BarSystem.Behaviors
{
    /// <summary>
    /// Applies a value per second (positive = regenerates, negative = continuously
    /// drains). Reused for health (regeneration) and stamina (regeneration while idle).
    /// </summary>
    public class RegenBehavior : IBarBehavior
    {
        private readonly float _ratePerSecond;
        private readonly Func<bool> _isActive;

        /// <param name="ratePerSecond">How much to apply per second.</param>
        /// <param name="isActive">
        /// Optional condition to enable/disable regeneration
        /// (e.g., "not in combat", "stamina has not been used recently").
        /// If null, it is always active.
        /// </param>
        public RegenBehavior(float ratePerSecond, Func<bool> isActive = null)
        {
            _ratePerSecond = ratePerSecond;
            _isActive = isActive;
        }

        public void Tick(float deltaTime, BarModel model)
        {
            if (_isActive != null && !_isActive())
                return;

            model.ApplyDelta(_ratePerSecond * deltaTime);
        }
    }
}