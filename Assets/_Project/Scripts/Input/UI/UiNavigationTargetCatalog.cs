using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class UiNavigationTargetCatalog
{
    private const string SettingsPanelName = "SettingsPanel";

    public static void Collect(KeyboardNavigablePanel panel, List<UiNavigationTarget> targets)
    {
        targets.Clear();

        if (panel == null || !panel.gameObject.activeInHierarchy)
        {
            return;
        }

        var navigationRoot = panel.NavigationRoot;

        if (navigationRoot == null || !navigationRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        var customRoots = new HashSet<Transform>();
        var processedToggleGroups = new HashSet<ToggleGroup>();
        var collapseToggleGroups = IsSettingsPanel(panel);
        var behaviours = navigationRoot.GetComponentsInChildren<MonoBehaviour>(true);

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

        var selectables = navigationRoot.GetComponentsInChildren<Selectable>(true);

        for (var selectableIndex = 0; selectableIndex < selectables.Length; selectableIndex++)
        {
            var selectable = selectables[selectableIndex];

            if (!IsUsableSelectable(selectable) || !BelongsToPanel(panel, selectable.transform) || HasCustomUiButtonAncestor(selectable.transform, customRoots))
            {
                continue;
            }

            if (collapseToggleGroups && selectable is Toggle toggle && TryAddLogicalToggleGroupTarget(panel, toggle, processedToggleGroups, targets))
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

    private static bool TryAddLogicalToggleGroupTarget(KeyboardNavigablePanel panel, Toggle sourceToggle, HashSet<ToggleGroup> processedToggleGroups, List<UiNavigationTarget> targets)
    {
        if (sourceToggle == null || sourceToggle.group == null)
        {
            return false;
        }

        var toggleGroup = sourceToggle.group;

        if (processedToggleGroups.Contains(toggleGroup))
        {
            return true;
        }

        if (!TryFindToggleGroupRepresentative(panel, toggleGroup, out var representativeToggle, out var usableToggleCount))
        {
            return false;
        }

        if (usableToggleCount < 2)
        {
            return false;
        }

        processedToggleGroups.Add(toggleGroup);

        var groupRectTransform = toggleGroup.transform as RectTransform;

        if (groupRectTransform == null)
        {
            groupRectTransform = representativeToggle.transform as RectTransform;
        }

        if (groupRectTransform == null)
        {
            return true;
        }

        targets.Add(new UiNavigationTarget(representativeToggle.gameObject, groupRectTransform, representativeToggle, false));
        return true;
    }

    private static bool TryFindToggleGroupRepresentative(KeyboardNavigablePanel panel, ToggleGroup toggleGroup, out Toggle representativeToggle, out int usableToggleCount)
    {
        representativeToggle = null;
        usableToggleCount = 0;

        if (toggleGroup == null)
        {
            return false;
        }

        var toggles = toggleGroup.GetComponentsInChildren<Toggle>(true);
        Toggle firstUsableToggle = null;

        for (var toggleIndex = 0; toggleIndex < toggles.Length; toggleIndex++)
        {
            var toggle = toggles[toggleIndex];

            if (toggle == null || toggle.group != toggleGroup || !IsUsableSelectable(toggle) || !BelongsToPanel(panel, toggle.transform))
            {
                continue;
            }

            usableToggleCount++;

            if (firstUsableToggle == null)
            {
                firstUsableToggle = toggle;
            }

            if (toggle.isOn)
            {
                representativeToggle = toggle;
            }
        }

        if (representativeToggle == null)
        {
            representativeToggle = firstUsableToggle;
        }

        return representativeToggle != null;
    }

    private static bool IsSettingsPanel(KeyboardNavigablePanel panel)
    {
        if (panel == null)
        {
            return false;
        }

        var navigationRoot = panel.NavigationRoot;

        if (navigationRoot == null)
        {
            return false;
        }

        return string.Equals(navigationRoot.gameObject.name, SettingsPanelName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool BelongsToPanel(KeyboardNavigablePanel panel, Transform targetTransform)
    {
        if (panel == null || targetTransform == null)
        {
            return false;
        }

        var navigationRoot = panel.NavigationRoot;

        if (navigationRoot == null)
        {
            return false;
        }

        if (navigationRoot != panel.transform)
        {
            return targetTransform == navigationRoot || targetTransform.IsChildOf(navigationRoot);
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