using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;
public class GiveUpExploration : UiButtonController<ChangeSceneButtonModel>, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public PlayerInventorySaveSystem playerInventorySaveSystem;
    public ExplorationLoadContext loadContext;
    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        if (!isDisabled)
        {
            _fsm.TransitionTo(new ButtonHover(this, uiButtonView._hoverEnterEffects, uiButtonView._hoverExitEffects));
        }
    }

    void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
    {
        if (isDisabled) return;

        _fsm.TransitionTo(new ButtonPressed(this, uiButtonView._pressedEnterEffects, uiButtonView._pressedExitEffects));
        playerInventorySaveSystem.DeletesSave();
        loadContext.MoveSnapshotsToCharacterRestingPoints();

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

        if (CombatExplorationBridge.Instance != null
            && CombatExplorationBridge.Instance.TryCompleteReturnToExploration(uiButtonModel.sceneName))
        {
            return;
        }

        ScenesManager.Instance.LoadSceneByName(uiButtonModel.sceneName);
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        if (!isDisabled)
        {
            _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));
        }
    }
}
