using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;
public class ChangePanelButtonController : UiButtonController<ChangePanelButtonModel>, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    //Events
    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        if (!isDisabled)
        {
            _fsm.TransitionTo(new ButtonHover(this, uiButtonView._hoverEnterEffects, uiButtonView._hoverExitEffects));
        }
    }

    void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
    {
        if (isDisabled)
        {
            return;
        }

        _fsm.TransitionTo(new ButtonPressed(this, uiButtonView._pressedEnterEffects, uiButtonView._pressedExitEffects));

        var uiManager = UIManager.Instance;
        if (uiManager == null)
        {
            Debug.LogWarning($"{nameof(ChangePanelButtonController)}: {nameof(UIManager)}.{nameof(UIManager.Instance)} is null.", this);
            return;
        }

        if (uiButtonModel.panelToHide != null)
        {
            uiManager.ClosePanel(uiButtonModel.panelToHide);
        }

        if (uiButtonModel.panelToOpen != null)
        {
            uiManager.OpenPanel(uiButtonModel.panelToOpen);
        }
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        if (!isDisabled)
        {
            _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));
        }
    }
}
