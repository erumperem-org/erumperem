using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UiNavigationSession
{
    private const string SettingsPanelName = "SettingsPanel";
    private const string BackButtonName = "BtnBack";
    private const string GraphicsButtonName = "BtnGraphics";
    private const string AudioButtonName = "BtnAudio";
    private const string GameplayButtonName = "BtnGameplay";
    private const string GraphicsPanelName = "GraphicsPanel";
    private const string AudioPanelName = "AudioPanel";
    private const string GameplayPanelName = "GameplayPanel";
    private const float ScrollViewportPadding = 24f;

    private readonly List<UiNavigationTarget> _targets = new();
    private readonly UiEventSystemNavigationGuard _eventSystemNavigationGuard = new();
    private readonly UiGameplayInputBlocker _gameplayInputBlocker = new();
    private readonly bool _debugActivation;

    private KeyboardNavigablePanel _activePanel;
    private UiNavigationTarget _currentTarget;

    public bool HasActivePanel => _activePanel != null;

    public UiNavigationSession(bool debugActivation)
    {
        _debugActivation = debugActivation;
    }

    public void Tick()
    {
        SyncActivePanel();
        _gameplayInputBlocker.SetBlocked(_activePanel != null);

        if (_activePanel == null)
        {
            return;
        }

        if (InputDeviceModeManager.IsMouseMode)
        {
            ClearFocus();
        }
    }

    public void ActivateKeyboardMode()
    {
        SyncActivePanel();

        if (_activePanel == null)
        {
            return;
        }

        RefreshTargets();

        if (!UiNavigationTargetCatalog.IsValid(_currentTarget))
        {
            FocusDefault();
        }
    }

    public void Navigate(UiNavigationDirection direction)
    {
        RefreshTargets();

        if (_targets.Count == 0)
        {
            ClearFocus();
            return;
        }

        if (!UiNavigationTargetCatalog.IsValid(_currentTarget))
        {
            FocusDefault();
            return;
        }

        if (IsSettingsPanel())
        {
            NavigateSettings(direction);
            return;
        }

        if (UiNavigationEventDispatcher.TryHandleSelectableMove(_currentTarget, direction, _targets, out var selectableTarget))
        {
            if (!ReferenceEquals(selectableTarget, _currentTarget))
            {
                Focus(selectableTarget);
            }

            return;
        }

        if (UiSpatialNavigation.TryFindDirectional(_targets, _currentTarget, direction, out var target))
        {
            Focus(target);
            return;
        }

        if (_activePanel != null && _activePanel.WrapNavigation && UiSpatialNavigation.TryFindWrapped(_targets, _currentTarget, direction, out target))
        {
            Focus(target);
        }
    }

    public void Confirm()
    {
        if (_activePanel == null)
        {
            return;
        }

        EnsureCurrentTarget();

        if (!UiNavigationTargetCatalog.IsValid(_currentTarget))
        {
            return;
        }

        if (IsSettingsPanel() && TryToggleCurrentSettingsToggle())
        {
            return;
        }

        if (IsSettingsPanel() && TryEnterCurrentSettingsSection())
        {
            return;
        }

        UiNavigationEventDispatcher.Activate(_activePanel, _currentTarget, _debugActivation);
    }

    public void Cancel()
    {
        if (_activePanel == null)
        {
            return;
        }

        if (IsSettingsPanel())
        {
            HideSettingsDropdowns();
            ActivateCancelTarget();
            return;
        }

        if (UiNavigationEventDispatcher.TryExecuteCancel(_currentTarget))
        {
            return;
        }

        ActivateCancelTarget();
    }

    public void Shutdown()
    {
        ClearFocus();
        _activePanel = null;
        _targets.Clear();
        _eventSystemNavigationGuard.Restore();
        _gameplayInputBlocker.Release();
    }

    private void NavigateSettings(UiNavigationDirection direction)
    {
        if (direction == UiNavigationDirection.Right && TryEnterCurrentSettingsSection())
        {
            return;
        }

        if (TryHandleSettingsValueControl(direction))
        {
            return;
        }

        if (direction == UiNavigationDirection.Left && TryReturnToSettingsSectionButton())
        {
            return;
        }

        if (UiSpatialNavigation.TryFindDirectional(_targets, _currentTarget, direction, out var target))
        {
            Focus(target);
            return;
        }

        if (_activePanel != null && _activePanel.WrapNavigation && UiSpatialNavigation.TryFindWrapped(_targets, _currentTarget, direction, out target))
        {
            Focus(target);
        }
    }

    private bool TryToggleCurrentSettingsToggle()
    {
        if (_currentTarget == null || !(_currentTarget.Selectable is Toggle currentToggle))
        {
            return false;
        }

        var toggleGroup = currentToggle.group;

        if (toggleGroup == null)
        {
            return false;
        }

        var toggles = toggleGroup.GetComponentsInChildren<Toggle>(true);
        var usableToggles = new List<Toggle>();

        for (var toggleIndex = 0; toggleIndex < toggles.Length; toggleIndex++)
        {
            var toggle = toggles[toggleIndex];

            if (toggle == null || toggle.group != toggleGroup || !toggle.enabled || !toggle.gameObject.activeInHierarchy || !toggle.IsInteractable())
            {
                continue;
            }

            usableToggles.Add(toggle);
        }

        if (usableToggles.Count < 2)
        {
            return false;
        }

        var activeToggleIndex = -1;

        for (var toggleIndex = 0; toggleIndex < usableToggles.Count; toggleIndex++)
        {
            if (usableToggles[toggleIndex].isOn)
            {
                activeToggleIndex = toggleIndex;
                break;
            }
        }

        var nextToggleIndex = activeToggleIndex >= 0 ? (activeToggleIndex + 1) % usableToggles.Count : 0;
        var nextToggle = usableToggles[nextToggleIndex];

        nextToggle.isOn = true;

        Canvas.ForceUpdateCanvases();
        RefreshTargets();

        var updatedTarget = FindTargetForToggleGroup(toggleGroup);

        if (updatedTarget != null)
        {
            Focus(updatedTarget);
        }

        return true;
    }

    private UiNavigationTarget FindTargetForToggleGroup(ToggleGroup toggleGroup)
    {
        if (toggleGroup == null)
        {
            return null;
        }

        for (var targetIndex = 0; targetIndex < _targets.Count; targetIndex++)
        {
            var target = _targets[targetIndex];

            if (target == null || !(target.Selectable is Toggle toggle))
            {
                continue;
            }

            if (toggle.group == toggleGroup)
            {
                return target;
            }
        }

        return null;
    }

    private bool TryEnterCurrentSettingsSection()
    {
        if (!TryGetSectionPanelName(_currentTarget, out var sectionPanelName))
        {
            return false;
        }

        var sectionPanel = FindDescendantByName(_activePanel.transform, sectionPanelName);

        if (sectionPanel == null)
        {
            return true;
        }

        if (!sectionPanel.gameObject.activeInHierarchy)
        {
            UiNavigationEventDispatcher.Activate(_activePanel, _currentTarget, _debugActivation);
            Canvas.ForceUpdateCanvases();
        }

        RefreshTargets();

        sectionPanel = FindDescendantByName(_activePanel.transform, sectionPanelName);

        if (sectionPanel == null || !sectionPanel.gameObject.activeInHierarchy)
        {
            return true;
        }

        var firstTarget = FindTopLeftTargetInside(sectionPanel);

        if (firstTarget != null)
        {
            Focus(firstTarget);
        }

        return true;
    }

    private bool TryReturnToSettingsSectionButton()
    {
        if (_currentTarget == null || _currentTarget.GameObject == null)
        {
            return false;
        }

        if (IsSettingsSectionButton(_currentTarget.GameObject.name))
        {
            return false;
        }

        var sectionButtonName = FindSectionButtonNameForTarget(_currentTarget);

        if (string.IsNullOrEmpty(sectionButtonName))
        {
            return false;
        }

        var sectionButtonTarget = FindTargetByName(sectionButtonName);

        if (!UiNavigationTargetCatalog.IsValid(sectionButtonTarget))
        {
            return false;
        }

        Focus(sectionButtonTarget);
        return true;
    }

    private bool TryHandleSettingsValueControl(UiNavigationDirection direction)
    {
        if (_currentTarget == null || _currentTarget.Selectable == null)
        {
            return false;
        }

        if (_currentTarget.Selectable is Toggle && _currentTarget.Selectable.GetComponentInParent<ToggleGroup>() != null)
        {
            return false;
        }

        if (_currentTarget.Selectable is Slider slider)
        {
            var isHorizontal = slider.direction == Slider.Direction.LeftToRight || slider.direction == Slider.Direction.RightToLeft;
            var isAdjustmentDirection = isHorizontal ? direction == UiNavigationDirection.Left || direction == UiNavigationDirection.Right : direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Down;

            if (!isAdjustmentDirection)
            {
                return false;
            }

            UiNavigationEventDispatcher.TryHandleSelectableMove(_currentTarget, direction, _targets, out _);
            return true;
        }

        if (_currentTarget.Selectable is Scrollbar scrollbar)
        {
            var isHorizontal = scrollbar.direction == Scrollbar.Direction.LeftToRight || scrollbar.direction == Scrollbar.Direction.RightToLeft;
            var isAdjustmentDirection = isHorizontal ? direction == UiNavigationDirection.Left || direction == UiNavigationDirection.Right : direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Down;

            if (!isAdjustmentDirection)
            {
                return false;
            }

            UiNavigationEventDispatcher.TryHandleSelectableMove(_currentTarget, direction, _targets, out _);
            return true;
        }

        return false;
    }

    private void ActivateCancelTarget()
    {
        if (_activePanel == null)
        {
            return;
        }

        GameObject cancelTargetObject = _activePanel.CancelTarget;

        if (cancelTargetObject == null && IsSettingsPanel())
        {
            cancelTargetObject = FindDescendantByName(_activePanel.transform, BackButtonName)?.gameObject;
        }

        if (cancelTargetObject == null)
        {
            return;
        }

        RefreshTargets();

        var cancelTarget = UiNavigationTargetCatalog.FindForGameObject(_targets, cancelTargetObject);

        if (cancelTarget != null)
        {
            UiNavigationEventDispatcher.Activate(_activePanel, cancelTarget, _debugActivation);
            return;
        }

        var cancelButton = cancelTargetObject.GetComponent<Button>();

        if (cancelButton == null || !cancelButton.enabled || !cancelButton.interactable || !cancelButton.gameObject.activeInHierarchy)
        {
            return;
        }

        cancelButton.onClick.Invoke();
    }

    private bool IsSettingsPanel()
    {
        if (_activePanel == null)
        {
            return false;
        }

        var navigationRoot = _activePanel.NavigationRoot;

        if (navigationRoot == null)
        {
            return false;
        }

        return string.Equals(navigationRoot.gameObject.name, SettingsPanelName, StringComparison.OrdinalIgnoreCase);
    }

    private void HideSettingsDropdowns()
    {
        if (_activePanel == null)
        {
            return;
        }

        var dropdowns = _activePanel.GetComponentsInChildren<TMP_Dropdown>(true);

        for (var dropdownIndex = 0; dropdownIndex < dropdowns.Length; dropdownIndex++)
        {
            var dropdown = dropdowns[dropdownIndex];

            if (dropdown != null)
            {
                dropdown.Hide();
            }
        }
    }

    private bool TryGetSectionPanelName(UiNavigationTarget target, out string sectionPanelName)
    {
        sectionPanelName = null;

        if (target == null || target.GameObject == null)
        {
            return false;
        }

        switch (target.GameObject.name)
        {
            case GraphicsButtonName:
            {
                sectionPanelName = GraphicsPanelName;
                return true;
            }

            case AudioButtonName:
            {
                sectionPanelName = AudioPanelName;
                return true;
            }

            case GameplayButtonName:
            {
                sectionPanelName = GameplayPanelName;
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    private string FindSectionButtonNameForTarget(UiNavigationTarget target)
    {
        if (target == null || target.GameObject == null || _activePanel == null)
        {
            return null;
        }

        var current = target.GameObject.transform;

        while (current != null && current != _activePanel.transform)
        {
            switch (current.gameObject.name)
            {
                case GraphicsPanelName:
                {
                    return GraphicsButtonName;
                }

                case AudioPanelName:
                {
                    return AudioButtonName;
                }

                case GameplayPanelName:
                {
                    return GameplayButtonName;
                }
            }

            current = current.parent;
        }

        return null;
    }

    private UiNavigationTarget FindTopLeftTargetInside(Transform sectionPanel)
    {
        UiNavigationTarget bestTarget = null;
        var bestPosition = Vector2.zero;

        for (var targetIndex = 0; targetIndex < _targets.Count; targetIndex++)
        {
            var candidate = _targets[targetIndex];

            if (!UiNavigationTargetCatalog.IsValid(candidate) || candidate.GameObject == null)
            {
                continue;
            }

            var candidateTransform = candidate.GameObject.transform;

            if (candidateTransform != sectionPanel && !candidateTransform.IsChildOf(sectionPanel))
            {
                continue;
            }

            var candidatePosition = UiSpatialNavigation.GetScreenCenter(candidate);

            if (bestTarget == null || candidatePosition.y > bestPosition.y + 0.01f || Mathf.Abs(candidatePosition.y - bestPosition.y) <= 0.01f && candidatePosition.x < bestPosition.x)
            {
                bestTarget = candidate;
                bestPosition = candidatePosition;
            }
        }

        return bestTarget;
    }

    private UiNavigationTarget FindTargetByName(string targetName)
    {
        for (var targetIndex = 0; targetIndex < _targets.Count; targetIndex++)
        {
            var target = _targets[targetIndex];

            if (target != null && target.GameObject != null && string.Equals(target.GameObject.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return null;
    }

    private static bool IsSettingsSectionButton(string objectName)
    {
        return string.Equals(objectName, GraphicsButtonName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objectName, AudioButtonName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(objectName, GameplayButtonName, StringComparison.OrdinalIgnoreCase);
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        var transforms = root.GetComponentsInChildren<Transform>(true);

        for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
        {
            var candidate = transforms[transformIndex];

            if (candidate != null && string.Equals(candidate.gameObject.name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private void SyncActivePanel()
    {
        var currentPanel = KeyboardNavigablePanel.Current;

        if (ReferenceEquals(currentPanel, _activePanel))
        {
            if (_activePanel != null && InputDeviceModeManager.IsKeyboardMode && !UiNavigationTargetCatalog.IsValid(_currentTarget))
            {
                RefreshTargets();
                FocusDefault();
            }

            return;
        }

        ClearFocus();
        _activePanel = currentPanel;

        if (_activePanel == null)
        {
            _targets.Clear();
            _eventSystemNavigationGuard.Restore();
            return;
        }

        _eventSystemNavigationGuard.Capture();
        RefreshTargets();

        if (InputDeviceModeManager.IsKeyboardMode)
        {
            FocusDefault();
        }
    }

    private void EnsureCurrentTarget()
    {
        if (UiNavigationTargetCatalog.IsValid(_currentTarget))
        {
            return;
        }

        RefreshTargets();
        FocusDefault();
    }

    private void RefreshTargets()
    {
        UiNavigationTargetCatalog.Collect(_activePanel, _targets);
    }

    private void FocusDefault()
    {
        Focus(UiSpatialNavigation.FindDefault(_activePanel, _targets));
    }

    private void Focus(UiNavigationTarget target)
    {
        if (!UiNavigationTargetCatalog.IsValid(target) || ReferenceEquals(target, _currentTarget))
        {
            return;
        }

        UiNavigationEventDispatcher.Focus(_currentTarget, target);
        _currentTarget = target;
        EnsureTargetVisibleInScrollRect(_currentTarget);
    }

    private void ClearFocus()
    {
        if (_currentTarget == null)
        {
            return;
        }

        UiNavigationEventDispatcher.ClearFocus(_currentTarget);
        _currentTarget = null;
    }

    private static void EnsureTargetVisibleInScrollRect(UiNavigationTarget target)
    {
        if (target == null || target.RectTransform == null)
        {
            return;
        }

        var currentTransform = target.RectTransform.parent;

        while (currentTransform != null)
        {
            var scrollRect = currentTransform.GetComponent<ScrollRect>();

            if (scrollRect != null && scrollRect.enabled && scrollRect.gameObject.activeInHierarchy && scrollRect.content != null)
            {
                if (target.RectTransform == scrollRect.content || target.RectTransform.IsChildOf(scrollRect.content))
                {
                    ScrollTargetIntoView(scrollRect, target.RectTransform);
                    return;
                }
            }

            currentTransform = currentTransform.parent;
        }
    }

    private static void ScrollTargetIntoView(ScrollRect scrollRect, RectTransform targetRect)
    {
        if (scrollRect == null || targetRect == null || scrollRect.content == null)
        {
            return;
        }

        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;

        if (viewport == null || scrollRect.content.parent == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        var targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, targetRect);
        var viewportRect = viewport.rect;
        var viewportOffset = Vector2.zero;

        if (scrollRect.vertical)
        {
            var topLimit = viewportRect.yMax - ScrollViewportPadding;
            var bottomLimit = viewportRect.yMin + ScrollViewportPadding;

            if (targetBounds.max.y > topLimit)
            {
                viewportOffset.y = targetBounds.max.y - topLimit;
            }
            else if (targetBounds.min.y < bottomLimit)
            {
                viewportOffset.y = targetBounds.min.y - bottomLimit;
            }
        }

        if (scrollRect.horizontal)
        {
            var rightLimit = viewportRect.xMax - ScrollViewportPadding;
            var leftLimit = viewportRect.xMin + ScrollViewportPadding;

            if (targetBounds.max.x > rightLimit)
            {
                viewportOffset.x = targetBounds.max.x - rightLimit;
            }
            else if (targetBounds.min.x < leftLimit)
            {
                viewportOffset.x = targetBounds.min.x - leftLimit;
            }
        }

        if (viewportOffset.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        var worldOffset = viewport.TransformVector(new Vector3(viewportOffset.x, viewportOffset.y, 0f));
        var contentParent = scrollRect.content.parent;
        var contentParentOffset = contentParent.InverseTransformVector(worldOffset);
        var anchoredPosition = scrollRect.content.anchoredPosition;

        if (scrollRect.horizontal)
        {
            anchoredPosition.x -= contentParentOffset.x;
        }

        if (scrollRect.vertical)
        {
            anchoredPosition.y -= contentParentOffset.y;
        }

        scrollRect.StopMovement();
        scrollRect.velocity = Vector2.zero;
        scrollRect.content.anchoredPosition = anchoredPosition;

        Canvas.ForceUpdateCanvases();
    }
}