using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities.Console;
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
        if (!isDisabled)
        {
            _fsm.TransitionTo(new ButtonPressed(this, uiButtonView._pressedEnterEffects, uiButtonView._pressedExitEffects));
            UIManager.Instance.ClosePanel(uiButtonModel.panelToHide);
            UIManager.Instance.OpenPanel(uiButtonModel.panelToOpen);
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
