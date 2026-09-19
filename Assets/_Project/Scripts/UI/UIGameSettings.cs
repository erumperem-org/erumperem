using UnityEngine;
using UnityEngine.UI;

public sealed class UIGameSettings : MonoBehaviour
{
    [Header("Game")]
    [SerializeField] private Toggle specialCursorToggle;

    private void OnEnable()
    {
        if (specialCursorToggle == null)
        {
            Debug.LogWarning("UIGameSettings: Special Cursor Toggle não foi configurado.", this);
            return;
        }

        bool specialCursorEnabled = CursorManager.Instance != null ? CursorManager.Instance.SpecialCursorEnabled : CursorManager.GetSavedSpecialCursorEnabled();

        specialCursorToggle.SetIsOnWithoutNotify(specialCursorEnabled);
        specialCursorToggle.onValueChanged.AddListener(HandleSpecialCursorChanged);
    }

    private void OnDisable()
    {
        if (specialCursorToggle != null)
        {
            specialCursorToggle.onValueChanged.RemoveListener(HandleSpecialCursorChanged);
        }
    }

    private void HandleSpecialCursorChanged(bool enabled)
    {
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetSpecialCursorEnabled(enabled);
            return;
        }

        CursorManager.SaveSpecialCursorEnabledPreference(enabled);
    }
}