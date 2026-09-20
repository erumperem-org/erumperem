using UnityEngine.EventSystems;

public sealed class UiEventSystemNavigationGuard
{
    private bool _captured;
    private bool _previousSendNavigationEvents;

    public void Capture()
    {
        if (_captured || EventSystem.current == null)
        {
            return;
        }

        _previousSendNavigationEvents = EventSystem.current.sendNavigationEvents;
        EventSystem.current.sendNavigationEvents = false;
        _captured = true;
    }

    public void Restore()
    {
        if (!_captured)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.sendNavigationEvents = _previousSendNavigationEvents;
        }

        _captured = false;
    }
}
