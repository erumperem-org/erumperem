using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;

public class ChangeSceneButtonController : UiButtonController<ChangeSceneButtonModel>
{
    protected override void OnPointerDownHandled(PointerEventData eventData)
    {
        if (string.IsNullOrWhiteSpace(uiButtonModel.sceneName))
        {
            Debug.LogError("[ChangeSceneButtonController] sceneName não configurado no botão.", this);
            return;
        }

        if (ScenesManager.Instance == null)
        {
            Debug.LogError("[ChangeSceneButtonController] ScenesManager.Instance não encontrado.", this);
            return;
        }

        ScenesManager.Instance.LoadSceneByName(uiButtonModel.sceneName);
    }
}
