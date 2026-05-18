using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class UIElementSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler, IDragHandler
{
    public string hoverSound = "Hover";
    public string clickSound = "Press";
    
    public float dragTickInterval = 0.1f;

    private Selectable _uiElement;
    private float _lastDragTime;

    private void Awake()
    {
        _uiElement = GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_uiElement != null && _uiElement.interactable && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(hoverSound);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_uiElement != null && _uiElement.interactable && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(clickSound);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
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