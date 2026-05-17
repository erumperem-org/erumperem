using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities.Console;
public class CloseApplicationButtonController : UiButtonController<CloseApplicationButtonModel>, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
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
            Application.Quit();
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Closing Application");
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
