
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UiNavigationEventDispatcher
{
    private static Transform _settingsTextTransform;
    private static Vector3 _settingsTextOriginalScale;

    public static void Focus(UiNavigationTarget previous, UiNavigationTarget current)
    {
        RestoreSettingsTextScale();
        DispatchPointerExit(previous);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);

            if (current != null && current.Selectable != null && current.Selectable.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(current.GameObject);
            }
        }

        DispatchPointerEnter(current);
        EnlargeSettingsCategoryText(current);
    }

    public static void ClearFocus(UiNavigationTarget current)
    {
        RestoreSettingsTextScale();
        DispatchPointerExit(current);

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public static bool TryHandleSelectableMove(UiNavigationTarget current, UiNavigationDirection direction, IReadOnlyList<UiNavigationTarget> targets, out UiNavigationTarget selectedTarget)
    {
        selectedTarget = current;

        if (current == null || current.IsCustomUiButton || current.Selectable == null || EventSystem.current == null)
        {
            return false;
        }

        if (current.Selectable is Slider || current.Selectable is Scrollbar)
        {
            EventSystem.current.SetSelectedGameObject(current.GameObject);
            ExecuteMove(current.GameObject, direction);
            EventSystem.current.SetSelectedGameObject(null);
            return true;
        }

        EventSystem.current.SetSelectedGameObject(current.GameObject);
        var selectedBeforeMove = EventSystem.current.currentSelectedGameObject;

        ExecuteMove(current.GameObject, direction);

        var selectedAfterMove = EventSystem.current.currentSelectedGameObject;

        if (selectedAfterMove == null || selectedAfterMove == selectedBeforeMove || selectedAfterMove == current.GameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
            return false;
        }

        var matchingTarget = UiNavigationTargetCatalog.FindForGameObject(targets, selectedAfterMove);

        EventSystem.current.SetSelectedGameObject(null);

        if (matchingTarget == null)
        {
            return false;
        }

        selectedTarget = matchingTarget;
        return true;
    }

    public static bool TryExecuteCancel(UiNavigationTarget current)
    {
        if (EventSystem.current == null || current == null || current.GameObject == null)
        {
            return false;
        }

        var cancelEventData = new BaseEventData(EventSystem.current);
        return ExecuteEvents.Execute(current.GameObject, cancelEventData, ExecuteEvents.cancelHandler);
    }

    public static void Activate(KeyboardNavigablePanel panel, UiNavigationTarget target, bool debugActivation)
    {
        if (!UiNavigationTargetCatalog.IsValid(target) || EventSystem.current == null)
        {
            return;
        }

        if (debugActivation)
        {
            Debug.Log($"[GlobalUiKeyboardNavigation] Confirmando '{target.GameObject.name}'.", target.GameObject);
        }

        if (target.Selectable != null && !target.Selectable.IsInteractable())
        {
            return;
        }

        var pointerEventData = CreatePointerEventData(target);

        if (TryActivateThroughHierarchy(target.GameObject, pointerEventData, debugActivation))
        {
            CloseUnwiredLeaveOrClose(panel, target);
            RestoreKeyboardHoverAfterActivation(target);
            return;
        }

        if (TryActivateThroughUiRaycast(panel, pointerEventData, debugActivation))
        {
            CloseUnwiredLeaveOrClose(panel, target);
            RestoreKeyboardHoverAfterActivation(target);
            return;
        }

        EventSystem.current.SetSelectedGameObject(target.GameObject);

        var submitEventData = new BaseEventData(EventSystem.current);

        if (ExecuteEvents.Execute(target.GameObject, submitEventData, ExecuteEvents.submitHandler))
        {
            RestoreKeyboardHoverAfterActivation(target);
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);

        if (debugActivation)
        {
            Debug.LogWarning($"[GlobalUiKeyboardNavigation] Nenhuma ação encontrada para '{target.GameObject.name}'.", target.GameObject);
        }
    }

    private static bool TryActivateThroughUiRaycast(KeyboardNavigablePanel panel, PointerEventData pointerEventData, bool debugActivation)
    {
        if (panel == null || EventSystem.current == null)
        {
            return false;
        }

        var panelTransform = panel.transform;
        var raycastResults = new List<RaycastResult>();

        EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        for (var resultIndex = 0; resultIndex < raycastResults.Count; resultIndex++)
        {
            var raycastResult = raycastResults[resultIndex];

            if (raycastResult.gameObject == null)
            {
                continue;
            }

            var raycastTransform = raycastResult.gameObject.transform;

            if (raycastTransform != panelTransform && !raycastTransform.IsChildOf(panelTransform))
            {
                continue;
            }

            var pointerDownTarget = ExecuteEvents.GetEventHandler<IPointerDownHandler>(raycastResult.gameObject);
            var pointerClickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(raycastResult.gameObject);

            if (pointerDownTarget == null && pointerClickTarget == null)
            {
                continue;
            }

            pointerEventData.pointerPressRaycast = raycastResult;
            pointerEventData.rawPointerPress = raycastResult.gameObject;

            if (debugActivation)
            {
                Debug.Log($"[GlobalUiKeyboardNavigation] Raycast encontrou '{raycastResult.gameObject.name}'.", raycastResult.gameObject);
            }

            if (pointerDownTarget != null)
            {
                pointerEventData.pointerPress = pointerDownTarget;

                ExecuteEvents.Execute(pointerDownTarget, pointerEventData, ExecuteEvents.pointerDownHandler);

                if (!pointerDownTarget.activeInHierarchy)
                {
                    return true;
                }
            }

            var pointerUpTarget = ExecuteEvents.GetEventHandler<IPointerUpHandler>(raycastResult.gameObject);

            if (pointerUpTarget != null && pointerUpTarget.activeInHierarchy)
            {
                ExecuteEvents.Execute(pointerUpTarget, pointerEventData, ExecuteEvents.pointerUpHandler);
            }

            if (pointerClickTarget != null && pointerClickTarget.activeInHierarchy)
            {
                ExecuteEvents.Execute(pointerClickTarget, pointerEventData, ExecuteEvents.pointerClickHandler);
            }

            return true;
        }

        return false;
    }

    private static bool TryActivateThroughHierarchy(GameObject source, PointerEventData pointerEventData, bool debugActivation)
    {
        if (source == null)
        {
            return false;
        }

        var pointerDownTarget = ExecuteEvents.GetEventHandler<IPointerDownHandler>(source);
        var pointerClickTarget = ExecuteEvents.GetEventHandler<IPointerClickHandler>(source);

        if (pointerDownTarget == null)
        {
            pointerDownTarget = FindNonSelectableHandlerTarget<IPointerDownHandler>(source);
        }

        if (pointerDownTarget != null)
        {
            pointerEventData.pointerPress = pointerDownTarget;
            pointerEventData.rawPointerPress = source;

            ExecuteEvents.Execute(pointerDownTarget, pointerEventData, ExecuteEvents.pointerDownHandler);

            if (!pointerDownTarget.activeInHierarchy)
            {
                return true;
            }
        }

        if (pointerClickTarget == null)
        {
            pointerClickTarget = FindHandlerTargetInChildren<IPointerClickHandler>(source);
        }

        var pointerUpTarget = ExecuteEvents.GetEventHandler<IPointerUpHandler>(source);

        if (pointerUpTarget != null && pointerUpTarget.activeInHierarchy)
        {
            ExecuteEvents.Execute(pointerUpTarget, pointerEventData, ExecuteEvents.pointerUpHandler);
        }

        if (pointerClickTarget != null && pointerClickTarget.activeInHierarchy)
        {
            ExecuteEvents.Execute(pointerClickTarget, pointerEventData, ExecuteEvents.pointerClickHandler);
            return true;
        }

        if (debugActivation && pointerDownTarget != null)
        {
            Debug.Log($"[GlobalUiKeyboardNavigation] Ação executada em '{pointerDownTarget.name}'.", pointerDownTarget);
        }

        return pointerDownTarget != null;
    }

    private static void CloseUnwiredLeaveOrClose(KeyboardNavigablePanel panel, UiNavigationTarget target)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy || target == null || target.GameObject == null)
        {
            return;
        }

        string name = target.GameObject.name;

        if (name != "Leave" && name != "Close" && name != "BtnClose")
        {
            return;
        }

        Button button = target.GameObject.GetComponent<Button>();

        if (button == null)
        {
            return;
        }

        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentTarget(index) != null)
            {
                return;
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClosePanel(panel.gameObject);
        }
        else
        {
            panel.gameObject.SetActive(false);
        }
    }

    public static void RefreshHover(UiNavigationTarget target)
    {
        if (UiNavigationTargetCatalog.IsValid(target))
        {
            DispatchPointerEnter(target);
        }
    }

    private static void EnlargeSettingsCategoryText(UiNavigationTarget target)
    {
        if (target == null || target.GameObject == null)
        {
            return;
        }

        string name = target.GameObject.name;

        if (name != "BtnGraphics" && name != "BtnAudio" && name != "BtnGameplay")
        {
            return;
        }

        Transform parent = target.GameObject.transform.parent;

        if (parent == null || parent.name != "ButtonsSettings")
        {
            return;
        }

        TMP_Text label = target.GameObject.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
        {
            return;
        }

        _settingsTextTransform = label.transform;
        _settingsTextOriginalScale = _settingsTextTransform.localScale;
        _settingsTextTransform.localScale = _settingsTextOriginalScale * 1.10f;
    }

    private static void RestoreSettingsTextScale()
    {
        if (_settingsTextTransform != null)
        {
            _settingsTextTransform.localScale = _settingsTextOriginalScale;
        }

        _settingsTextTransform = null;
    }

    private static void RestoreKeyboardHoverAfterActivation(UiNavigationTarget target)
    {
        if (target == null || target.GameObject == null || EventSystem.current == null)
        {
            return;
        }

        if (target.Selectable is TMP_Dropdown || target.Selectable is Dropdown)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
    }

    private static void DispatchPointerEnter(UiNavigationTarget target)
    {
        if (!UiNavigationTargetCatalog.IsValid(target) || EventSystem.current == null)
        {
            return;
        }

        var pointerEventData = CreatePointerEventData(target);

        ExecuteEvents.Execute(target.GameObject, pointerEventData, ExecuteEvents.pointerEnterHandler);

        var additionalEventTarget = FindNonSelectableHandlerTarget<IPointerEnterHandler>(target.GameObject);

        if (additionalEventTarget != null && additionalEventTarget != target.GameObject)
        {
            ExecuteEvents.Execute(additionalEventTarget, pointerEventData, ExecuteEvents.pointerEnterHandler);
        }
    }

    private static void DispatchPointerExit(UiNavigationTarget target)
    {
        if (target == null || target.GameObject == null || EventSystem.current == null)
        {
            return;
        }

        var pointerEventData = CreatePointerEventData(target);

        ExecuteEvents.Execute(target.GameObject, pointerEventData, ExecuteEvents.pointerExitHandler);

        var additionalEventTarget = FindNonSelectableHandlerTarget<IPointerExitHandler>(target.GameObject);

        if (additionalEventTarget != null && additionalEventTarget != target.GameObject)
        {
            ExecuteEvents.Execute(additionalEventTarget, pointerEventData, ExecuteEvents.pointerExitHandler);
        }
    }

    private static void ExecuteMove(GameObject target, UiNavigationDirection direction)
    {
        var axisEventData = new AxisEventData(EventSystem.current);
        axisEventData.moveDir = ToMoveDirection(direction);
        axisEventData.moveVector = UiSpatialNavigation.ToVector(direction);

        ExecuteEvents.Execute(target, axisEventData, ExecuteEvents.moveHandler);
    }

    private static PointerEventData CreatePointerEventData(UiNavigationTarget target)
    {
        var pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.button = PointerEventData.InputButton.Left;
        pointerEventData.position = UiSpatialNavigation.GetScreenCenter(target);

        return pointerEventData;
    }

    private static GameObject FindNonSelectableHandlerTarget<THandler>(GameObject source) where THandler : IEventSystemHandler
    {
        if (source == null)
        {
            return null;
        }

        var current = source.transform;

        while (current != null)
        {
            var behaviours = current.GetComponents<MonoBehaviour>();

            for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                var behaviour = behaviours[behaviourIndex];

                if (behaviour != null && behaviour.enabled && behaviour is THandler && !(behaviour is Selectable))
                {
                    return current.gameObject;
                }
            }

            current = current.parent;
        }

        return FindNonSelectableHandlerTargetInChildren<THandler>(source);
    }

    private static GameObject FindNonSelectableHandlerTargetInChildren<THandler>(GameObject source) where THandler : IEventSystemHandler
    {
        var behaviours = source.GetComponentsInChildren<MonoBehaviour>(true);

        for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            var behaviour = behaviours[behaviourIndex];

            if (behaviour != null && behaviour.enabled && behaviour.gameObject.activeInHierarchy && behaviour is THandler && !(behaviour is Selectable))
            {
                return behaviour.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindHandlerTargetInChildren<THandler>(GameObject source) where THandler : IEventSystemHandler
    {
        var behaviours = source.GetComponentsInChildren<MonoBehaviour>(true);

        for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            var behaviour = behaviours[behaviourIndex];

            if (behaviour != null && behaviour.enabled && behaviour.gameObject.activeInHierarchy && behaviour is THandler)
            {
                return behaviour.gameObject;
            }
        }

        return null;
    }

    private static MoveDirection ToMoveDirection(UiNavigationDirection direction)
    {
        switch (direction)
        {
            case UiNavigationDirection.Up:
            {
                return MoveDirection.Up;
            }

            case UiNavigationDirection.Down:
            {
                return MoveDirection.Down;
            }

            case UiNavigationDirection.Left:
            {
                return MoveDirection.Left;
            }

            case UiNavigationDirection.Right:
            {
                return MoveDirection.Right;
            }

            default:
            {
                return MoveDirection.None;
            }
        }
    }
}
