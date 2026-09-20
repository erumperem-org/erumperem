using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class UiNavigationTargetCatalog
{
    public static void Collect(KeyboardNavigablePanel panel, List<UiNavigationTarget> targets)
    {
        targets.Clear();

        if (panel == null || !panel.gameObject.activeInHierarchy)
        {
            return;
        }

        var customRoots = new HashSet<Transform>();
        var behaviours = panel.GetComponentsInChildren<MonoBehaviour>(true);

        for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            var behaviour = behaviours[behaviourIndex];

            if (!IsUsableCustomUiButton(behaviour) || !BelongsToPanel(panel, behaviour.transform))
            {
                continue;
            }

            var rectTransform = behaviour.transform as RectTransform;

            if (rectTransform == null)
            {
                continue;
            }

            customRoots.Add(behaviour.transform);
            targets.Add(new UiNavigationTarget(behaviour.gameObject, rectTransform, behaviour.GetComponent<Selectable>(), true));
        }

        var selectables = panel.GetComponentsInChildren<Selectable>(true);

        for (var selectableIndex = 0; selectableIndex < selectables.Length; selectableIndex++)
        {
            var selectable = selectables[selectableIndex];

            if (!IsUsableSelectable(selectable) || !BelongsToPanel(panel, selectable.transform) || HasCustomUiButtonAncestor(selectable.transform, customRoots))
            {
                continue;
            }

            var rectTransform = selectable.transform as RectTransform;

            if (rectTransform == null)
            {
                continue;
            }

            targets.Add(new UiNavigationTarget(selectable.gameObject, rectTransform, selectable, false));
        }
    }

    public static bool IsValid(UiNavigationTarget target)
    {
        if (target == null || target.GameObject == null || target.RectTransform == null || !target.GameObject.activeInHierarchy)
        {
            return false;
        }

        if (!target.IsCustomUiButton)
        {
            return IsUsableSelectable(target.Selectable);
        }

        var behaviours = target.GameObject.GetComponents<MonoBehaviour>();

        for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
        {
            if (IsUsableCustomUiButton(behaviours[behaviourIndex]))
            {
                return true;
            }
        }

        return false;
    }

    public static UiNavigationTarget FindForGameObject(IReadOnlyList<UiNavigationTarget> targets, GameObject targetObject)
    {
        if (targets == null || targetObject == null)
        {
            return null;
        }

        for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
        {
            var target = targets[targetIndex];

            if (target.GameObject == targetObject)
            {
                return target;
            }
        }

        return null;
    }

    private static bool BelongsToPanel(KeyboardNavigablePanel panel, Transform targetTransform)
    {
        if (panel == null || targetTransform == null)
        {
            return false;
        }

        var nearestPanel = targetTransform.GetComponentInParent<KeyboardNavigablePanel>(true);
        return nearestPanel == panel;
    }

    private static bool HasCustomUiButtonAncestor(Transform targetTransform, HashSet<Transform> customRoots)
    {
        var current = targetTransform;

        while (current != null)
        {
            if (customRoots.Contains(current))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsUsableSelectable(Selectable selectable)
    {
        return selectable != null && selectable.enabled && selectable.gameObject.activeInHierarchy && selectable.IsInteractable() && AreParentCanvasGroupsInteractive(selectable.transform);
    }

    private static bool IsUsableCustomUiButton(MonoBehaviour behaviour)
    {
        if (behaviour == null || !behaviour.enabled || !behaviour.gameObject.activeInHierarchy || !IsCustomUiButtonControllerType(behaviour.GetType()))
        {
            return false;
        }

        if (TryReadCustomButtonDisabled(behaviour, out var isDisabled) && isDisabled)
        {
            return false;
        }

        return AreParentCanvasGroupsInteractive(behaviour.transform);
    }

    private static bool IsCustomUiButtonControllerType(Type type)
    {
        var currentType = type;

        while (currentType != null)
        {
            if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(UiButtonController<>))
            {
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool TryReadCustomButtonDisabled(MonoBehaviour behaviour, out bool isDisabled)
    {
        isDisabled = false;
        var currentType = behaviour.GetType();

        while (currentType != null)
        {
            var field = currentType.GetField("isDisabled", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            if (field != null && field.FieldType == typeof(bool))
            {
                isDisabled = (bool)field.GetValue(behaviour);
                return true;
            }

            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool AreParentCanvasGroupsInteractive(Transform targetTransform)
    {
        var current = targetTransform;

        while (current != null)
        {
            var canvasGroups = current.GetComponents<CanvasGroup>();

            for (var groupIndex = 0; groupIndex < canvasGroups.Length; groupIndex++)
            {
                var canvasGroup = canvasGroups[groupIndex];

                if (canvasGroup == null)
                {
                    continue;
                }

                if (!canvasGroup.interactable || !canvasGroup.blocksRaycasts || canvasGroup.alpha <= 0.001f)
                {
                    return false;
                }

                if (canvasGroup.ignoreParentGroups)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return true;
    }
}
