using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;

public class ChangePanelButtonController : UiButtonController<ChangePanelButtonModel>
{
    protected override void OnPointerDownHandled(PointerEventData eventData)
    {
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
}
