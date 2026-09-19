using BarSystem.Core;

namespace BarSystem.Behaviors
{
    /// <summary>
    /// Does not drain anything automatically on each tick: exposes a method
    /// (Consume) to drain the bar when an external action occurs
    /// (e.g., running, attacking, using an ability).
    /// </summary>
    public class DrainOnUseBehavior : IBarBehavior
    {
        public void Tick(float deltaTime, BarModel model)
        {
            // No automatic per-tick logic — consumption is always on demand.
        }

        public void Consume(BarModel model, float amount) => model.ApplyDelta(-amount);
    }
}