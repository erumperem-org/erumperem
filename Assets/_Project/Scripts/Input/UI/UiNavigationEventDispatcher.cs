
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class UiNavigationEventDispatcher
{
    public static GameObject FocusedGameObject { get; private set; }

    public static void Focus(UiNavigationTarget previous, UiNavigationTarget current)
    {
        DispatchPointerEvent(previous, false);
        FocusedGameObject = current != null ? current.GameObject : null;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);

            if (current != null && current.Selectable != null && current.Selectable.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(current.GameObject);
            }
        }

        DispatchPointerEvent(current, true);
    }

    public static void ClearFocus(UiNavigationTarget current)
    {
        DispatchPointerEvent(current, false);
        FocusedGameObject = null;

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
        GameObject before = EventSystem.current.currentSelectedGameObject;
        ExecuteMove(current.GameObject, direction);
        GameObject after = EventSystem.current.currentSelectedGameObject;

        if (after == null || after == before || after == current.GameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
            return false;
        }

        UiNavigationTarget candidate = UiNavigationTargetCatalog.FindForGameObject(targets, after);
        EventSystem.current.SetSelectedGameObject(null);

        if (candidate == null)
        {
            return false;
        }

        selectedTarget = candidate;
        return true;
    }

    public static bool TryExecuteCancel(UiNavigationTarget current)
    {
        if (EventSystem.current == null || current == null || current.GameObject == null)
        {
            return false;
        }

        return ExecuteEvents.Execute(current.GameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
    }

    public static void Activate(KeyboardNavigablePanel panel, UiNavigationTarget target, bool debugActivation)
    {
        if (!UiNavigationTargetCatalog.IsValid(target) || EventSystem.current == null)
        {
            return;
        }

        if (target.Selectable != null && !target.Selectable.IsInteractable())
        {
            return;
        }

        GameObject source = target.GameObject;

        if (debugActivation)
        {
            Debug.Log("[GlobalUiKeyboardNavigation] Confirmando '" + source.name + "'.", source);
        }

        // Unity UI's mouse click calls Button.onClick. Use exactly the
        // same event for Enter/Space, including listeners added at runtime.
        Button button = source.GetComponent<Button>();

        if (button != null && button.isActiveAndEnabled && button.IsInteractable())
        {
            button.onClick.Invoke();
            CloseUnwiredExitButton(panel, source, button);
            return;
        }

        // Some of this project's buttons have no Unity Button component.
        // Their action is in UiButtonController.OnPointerDownHandled.
        PointerEventData data = CreatePointerEventData(target);
        MonoBehaviour custom = FindFirstCustomButton(source);

        if (custom is IPointerDownHandler customDown)
        {
            customDown.OnPointerDown(data);
            return;
        }

        if (ExecuteEvents.Execute(source, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler))
        {
            return;
        }

        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(source);

        if (handler != null)
        {
            ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
        }
        else if (debugActivation)
        {
            Debug.LogWarning("[GlobalUiKeyboardNavigation] Ação não encontrada em '" + source.name + "'.", source);
        }
    }

    public static void RefreshHover(UiNavigationTarget target)
    {
        DispatchPointerEvent(target, true);
    }

    private static void CloseUnwiredExitButton(KeyboardNavigablePanel panel, GameObject source, Button button)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy || source == null || button == null)
        {
            return;
        }

        string name = source.name;

        if (name != "Leave" && name != "BtnLeave" && name != "Close" && name != "BtnClose")
        {
            return;
        }

        // Fallback for prefabs whose serialized onClick contains only
        // missing targets. Do not override any valid persistent binding.
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

    private static void DispatchPointerEvent(UiNavigationTarget target, bool entering)
    {
        if (target == null || target.GameObject == null || EventSystem.current == null)
        {
            return;
        }

        if (entering && !UiNavigationTargetCatalog.IsValid(target))
        {
            return;
        }

        PointerEventData data = CreatePointerEventData(target);
        bool handledCustom = false;
        bool hasHandler = false;

        foreach (MonoBehaviour behaviour in target.GameObject.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null || !behaviour.isActiveAndEnabled)
            {
                continue;
            }

            if (IsCustomButton(behaviour))
            {
                if (handledCustom)
                {
                    continue;
                }

                handledCustom = true;
            }

            if (entering && behaviour is IPointerEnterHandler pointerEnter)
            {
                pointerEnter.OnPointerEnter(data);
                hasHandler = true;
            }
            else if (!entering && behaviour is IPointerExitHandler pointerExit)
            {
                pointerExit.OnPointerExit(data);
                hasHandler = true;
            }
        }

        if (!hasHandler)
        {
            GameObject parentHandler = entering ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target.GameObject) : ExecuteEvents.GetEventHandler<IPointerExitHandler>(target.GameObject);

            if (parentHandler != null && parentHandler != target.GameObject)
            {
                if (entering)
                {
                    ExecuteEvents.Execute(parentHandler, data, ExecuteEvents.pointerEnterHandler);
                }
                else
                {
                    ExecuteEvents.Execute(parentHandler, data, ExecuteEvents.pointerExitHandler);
                }
            }
        }
    }

    private static MonoBehaviour FindFirstCustomButton(GameObject source)
    {
        foreach (MonoBehaviour behaviour in source.GetComponents<MonoBehaviour>())
        {
            if (IsCustomButton(behaviour) && behaviour.isActiveAndEnabled)
            {
                return behaviour;
            }
        }

        return null;
    }

    private static bool IsCustomButton(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        System.Type type = behaviour.GetType();

        while (type != null)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(UiButtonController<>))
            {
                return true;
            }

            type = type.BaseType;
        }

        return false;
    }

    private static void ExecuteMove(GameObject target, UiNavigationDirection direction)
    {
        AxisEventData data = new AxisEventData(EventSystem.current);
        data.moveDir = ToMoveDirection(direction);
        data.moveVector = UiSpatialNavigation.ToVector(direction);
        ExecuteEvents.Execute(target, data, ExecuteEvents.moveHandler);
    }

    private static PointerEventData CreatePointerEventData(UiNavigationTarget target)
    {
        PointerEventData data = new PointerEventData(EventSystem.current);
        data.button = PointerEventData.InputButton.Left;
        data.position = UiSpatialNavigation.GetScreenCenter(target);
        return data;
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
