using UnityEngine;
using UnityEngine.UI;
using BarSystem.Core;

namespace BarSystem.View
{
    /// <summary>
    /// The only point in the entire system that interacts with UnityEngine.UI.Slider.
    /// Any change of UI technology (Filled Image, UI Toolkit, 3D world-space bar,
    /// etc.) should only modify this layer — by replacing this class with another
    /// implementation of IBarView.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public class UISliderBarView : MonoBehaviour, IBarView
    {
        [SerializeField] private Slider _slider;

        private void Reset()
        {
            _slider = GetComponent<Slider>();
        }

        private void Awake()
        {
            if (_slider == null)
                _slider = GetComponent<Slider>();

            _slider.minValue = 0f;
            _slider.maxValue = 1f;
        }

        public void SetNormalizedValue(float normalizedValue)
        {
            if (_slider == null) return;
            _slider.value = normalizedValue;
        }
    }
}