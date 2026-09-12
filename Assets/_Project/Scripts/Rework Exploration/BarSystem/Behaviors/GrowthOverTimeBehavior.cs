using BarSystem.Core;

namespace BarSystem.Behaviors
{
    /// <summary>
    /// Grows automatically over time (e.g., corruption, pollution, accumulated
    /// "rage"). Use a negative value if automatic decay is needed.
    /// </summary>
    public class GrowthOverTimeBehavior : IBarBehavior
    {
        private readonly float _ratePerSecond;

        public GrowthOverTimeBehavior(float ratePerSecond)
        {
            _ratePerSecond = ratePerSecond;
        }

        public void Tick(float deltaTime, BarModel model)
        {
            model.ApplyDelta(_ratePerSecond * deltaTime);
        }
    }
}