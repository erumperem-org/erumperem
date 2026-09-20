using System.Collections.Generic;

public sealed class UiNavigationSession
{
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

        if (UiNavigationTargetCatalog.IsValid(_currentTarget))
        {
            UiNavigationEventDispatcher.Activate(_activePanel, _currentTarget, _debugActivation);
        }
    }

    public void Cancel()
    {
        if (_activePanel == null)
        {
            return;
        }

        if (UiNavigationEventDispatcher.TryExecuteCancel(_currentTarget))
        {
            return;
        }

        var cancelTargetObject = _activePanel.CancelTarget;

        if (cancelTargetObject == null)
        {
            return;
        }

        RefreshTargets();
        var cancelTarget = UiNavigationTargetCatalog.FindForGameObject(_targets, cancelTargetObject);

        if (cancelTarget != null)
        {
            UiNavigationEventDispatcher.Activate(_activePanel, cancelTarget, _debugActivation);
        }
    }

    public void Shutdown()
    {
        ClearFocus();
        _activePanel = null;
        _targets.Clear();
        _eventSystemNavigationGuard.Restore();
        _gameplayInputBlocker.Release();
    }

    private void SyncActivePanel()
    {
        var currentPanel = KeyboardNavigablePanel.Current;

        if (ReferenceEquals(currentPanel, _activePanel))
        {
            if (_activePanel != null && !UiNavigationTargetCatalog.IsValid(_currentTarget))
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
        FocusDefault();
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
    }

    private void ClearFocus()
    {
        UiNavigationEventDispatcher.ClearFocus(_currentTarget);
        _currentTarget = null;
    }
}
