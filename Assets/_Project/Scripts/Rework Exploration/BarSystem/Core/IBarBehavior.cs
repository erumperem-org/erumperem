namespace BarSystem.Core
{
    /// <summary>
    /// A behavior rule applied to a bar on each tick (e.g., regeneration,
    /// draining, automatic growth). Bar types are composed of multiple
    /// behaviors plugged into the BarController instead of BarModel subclasses.
    /// </summary>
    public interface IBarBehavior
    {
        void Tick(float deltaTime, BarModel model);
    }
}