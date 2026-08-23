using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents an individual torch in the scene.
/// Each torch has its own list of objects, which are activated when
/// the torch is lit and deactivated when it is unlit, according to the
/// overall state emitted by the TorchManager.
/// </summary>
public class Torch : MonoBehaviour
{
    [Header("Objects controlled by this torch")]
    [Tooltip("Activated when the torch is lit (isLit = true). Deactivated when the torch is unlit (isLit = false).")]
    [SerializeField] private List<GameObject> controlledObjects = new List<GameObject>();

    private void OnEnable()
    {
        TryScheduleSubscription();
    }

    private void OnDestroy()
    {
        if (TorchManager.Instance != null)
            TorchManager.Instance.OnTorchStateChange -= HandleTorchStateChange;
    }

    /// <summary>
    /// Ensures subscription to the event even if the TorchManager has not yet
    /// executed its Awake (a common execution order issue).
    /// </summary>
    private void TryScheduleSubscription()
    {
        if (TorchManager.Instance != null)
        {
            Subscribe();
        }
        else
        {
            // TorchManager has not initialized yet (Instance is null).
            // Try again on the next frame.
            StartCoroutine(WaitForManagerRoutine());
        }
    }

    private System.Collections.IEnumerator WaitForManagerRoutine()
    {
        while (TorchManager.Instance == null)
            yield return null;

        Subscribe();
    }

    private void Subscribe()
    {
        TorchManager.Instance.OnTorchStateChange += HandleTorchStateChange;

        // Apply the current state immediately upon subscribing,
        // so the torch reflects the correct state as soon as it is enabled.
        HandleTorchStateChange(TorchManager.Instance.IsTorchLit);
    }

    private void HandleTorchStateChange(bool isLit)
    {
        if (isLit)
            ActivateObjects();
        else
            DeactivateObjects();
    }

    /// <summary>
    /// Public wrapper used by the "Activate" test button in the Inspector
    /// (TorchEditor.cs) and also available through the component's context menu.
    /// Only applies to this torch's objects without changing the TorchManager's overall state.
    /// </summary>
    [ContextMenu("Test: Activate")]
    public void TestActivate()
    {
        ActivateObjects();
    }

    /// <summary>
    /// Public wrapper used by the "Deactivate" test button in the Inspector
    /// (TorchEditor.cs) and also available through the component's context menu.
    /// Only applies to this torch's objects without changing the TorchManager's overall state.
    /// </summary>
    [ContextMenu("Test: Deactivate")]
    public void TestDeactivate()
    {
        DeactivateObjects();
    }

    private void ActivateObjects()
    {
        SetActiveForList(controlledObjects, true);
    }

    private void DeactivateObjects()
    {
        SetActiveForList(controlledObjects, false);
    }

    private void SetActiveForList(List<GameObject> list, bool active)
    {
        foreach (var obj in list)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }
}