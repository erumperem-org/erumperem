using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Selectable))]
public class UIElementSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IDragHandler
{
    public string hoverSound = "Hover";
    public string clickSound = "Press";
    
    public float dragTickInterval = 0.1f;

    private Selectable _uiElement;
    private float _lastDragTime;
    private Slider _slider;
    private float _lastValue;

    private void Awake()
    {
        _uiElement = GetComponent<Selectable>();
        _slider = _uiElement as Slider;
    }

    private void OnEnable()
    {
        if (_slider == null) return;
        _lastValue = _slider.value;
        _slider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnDisable()
    {
        if (_slider != null) _slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        if (Mathf.Approximately(value, _lastValue)) return;
        _lastValue = value;
        if (!_slider.IsInteractable() || Time.unscaledTime < _lastDragTime) return;
        _lastDragTime = Time.unscaledTime + Mathf.Max(0.08f, dragTickInterval);
        AudioManager.instance?.PlaySliderTick(hoverSound);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_slider != null) return;
        if (_uiElement != null && _uiElement.interactable && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(hoverSound);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_slider != null) return;
        if (_uiElement != null && _uiElement.interactable && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(clickSound);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_slider != null) return;
        if (_uiElement != null && _uiElement.interactable && (_uiElement is Slider || _uiElement is Scrollbar) && AudioManager.instance != null)
        {
            if (Time.unscaledTime >= _lastDragTime + dragTickInterval)
            {
                AudioManager.instance.PlaySFX(hoverSound);
                _lastDragTime = Time.unscaledTime;
            }
        }
    }
}
