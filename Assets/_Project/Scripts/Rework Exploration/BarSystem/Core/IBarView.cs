namespace BarSystem.Core
{
    /// <summary>
    /// Abstraction for "something that renders a bar". Knows nothing about Slider,
    /// filled Image, UI Toolkit, or any concrete component —
    /// that is the sole responsibility of concrete implementations
    /// (e.g., UISliderBarView in the View layer). The Core defines the contract;
    /// the View only implements it — the dependency never runs in the opposite direction.
    /// </summary>
    public interface IBarView
    {
        /// <param name="normalizedValue">Value between 0 and 1.</param>
        void SetNormalizedValue(float normalizedValue);
    }
}