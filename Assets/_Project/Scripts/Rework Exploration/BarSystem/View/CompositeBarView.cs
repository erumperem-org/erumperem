using BarSystem.Core;

namespace BarSystem.View
{
    /// <summary>
    /// Repassa o mesmo normalizedValue para múltiplas views ao mesmo tempo
    /// (ex.: slider + texto), permitindo tratá-las como uma única IBarView
    /// perante decorators como HalfScaleBarView ou SmoothedBarView.
    /// </summary>
    public class CompositeBarView : IBarView
    {
        private readonly IBarView[] _views;

        public CompositeBarView(params IBarView[] views)
        {
            _views = views;
        }

        public void SetNormalizedValue(float normalizedValue)
        {
            for (int i = 0; i < _views.Length; i++)
            {
                _views[i].SetNormalizedValue(normalizedValue);
            }
        }
    }
}