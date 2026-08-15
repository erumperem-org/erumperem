using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;

public class CloseApplicationButtonController : UiButtonController<CloseApplicationButtonModel>
{
    protected override void OnPointerDownHandled(PointerEventData eventData)
    {
        Application.Quit();
        LoggerService.PrintLogMessage(LogLevel.Debug, "Closing Application", LogCategory.Lifecycle);
    }
}
