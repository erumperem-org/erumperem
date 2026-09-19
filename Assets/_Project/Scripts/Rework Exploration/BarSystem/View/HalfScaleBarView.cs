using UnityEngine;
using BarSystem.Core;

namespace BarSystem.View
{
    /// <summary>
    /// Decorator de IBarView que reescala o valor normalizado recebido do
    /// BarController (baseado no Max real do BarModel) para que a
    /// visualização "encha" com apenas uma fração do valor real.
    /// Ex.: Max real = 100, viewScale = 2 → a view fica "cheia" (1.0)
    /// quando Current = 50; o BarModel interno segue crescendo normalmente até 100.
    /// Fica por fora de toda a cadeia (inclusive do smoothing), pois reescala
    /// exatamente o que o BarController manda para SetNormalizedValue.
    /// </summary>
    public class HalfScaleBarView : IBarView
    {
        private readonly IBarView _inner;
        private readonly float _viewScale;

        /// <param name="inner">View (ou cadeia de views) que efetivamente renderiza.</param>
        /// <param name="viewScale">
        /// Fator aplicado ao normalizedValue antes de repassar adiante.
        /// 2f = view atinge 1 (cheia) quando o valor real atinge metade do Max.
        /// </param>
        public HalfScaleBarView(IBarView inner, float viewScale = 2f)
        {
            _inner = inner;
            _viewScale = viewScale;
        }

        public void SetNormalizedValue(float normalizedValue)
        {
            float scaled = Mathf.Clamp01(normalizedValue * _viewScale);
            _inner.SetNormalizedValue(scaled);
        }
    }
}