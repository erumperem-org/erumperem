using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
{
    public string hoverSound = "Hover";
    public string clickSound = "Press";

    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (btn != null && btn.interactable && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(hoverSound);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // Dispara no momento exato da pressão do clique, imune à desativação do GameObject no frame seguinte.
        if (btn != null && btn.interactable && AudioManager.instance != null)
        {
            AudioManager.instance.PlaySFX(clickSound);
        }
    }
}