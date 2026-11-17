
using System;
using System.Collections.Generic;
using Erumperem.Progression;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class KeyboardNavigablePanel : MonoBehaviour
{
    private static readonly string[] AutomaticCancelTargetNames =
    {
        "BtnBack",
        "Back",
        "BackButton",
        "BtnReturn",
        "Return",
        "ReturnButton",
        "BtnLeave",
        "Leave",
        "BtnClose",
        "Close",
        "CloseButton",
        "BtnExit",
        "Exit",
        "ExitButton",
        "ReturnToExpeditionButton"
    };

    [Header("Selection")]
    [Tooltip("Optional. Element selected when this panel opens.")]
    [SerializeField] private GameObject defaultSelected;

    [Tooltip("Optional. Element activated by Escape or Q.")]
    [SerializeField] private GameObject cancelTarget;

    [Header("Navigation")]
    [SerializeField] private bool wrapNavigation = true;
    [SerializeField] private int navigationPriority;

    private static readonly List<KeyboardNavigablePanel> ActivePanels = new List<KeyboardNavigablePanel>();
    private static long _nextActivationOrder;
    private long _activationOrder;

    public GameObject DefaultSelected => defaultSelected;
    public GameObject CancelTarget => ResolveCancelTarget();
    public bool WrapNavigation => wrapNavigation;
    public int NavigationPriority => navigationPriority;
    public Transform NavigationRoot => ResolveNavigationRoot();

    public static KeyboardNavigablePanel Current
    {
        get
        {
            CleanupActivePanels();

            KeyboardNavigablePanel bestPanel = null;

            for (int panelIndex = 0; panelIndex < ActivePanels.Count; panelIndex++)
            {
                KeyboardNavigablePanel panel = ActivePanels[panelIndex];

                if (panel == null || !panel.isActiveAndEnabled || !panel.gameObject.activeInHierarchy || !IsVisibleAndInteractive(panel.transform))
                {
                    continue;
                }

                if (bestPanel == null || panel.navigationPriority > bestPanel.navigationPriority || panel.navigationPriority == bestPanel.navigationPriority && panel._activationOrder > bestPanel._activationOrder)
                {
                    bestPanel = panel;
                }
            }

            return bestPanel;
        }
    }

    private void OnEnable()
    {
        _activationOrder = ++_nextActivationOrder;

        if (!ActivePanels.Contains(this))
        {
            ActivePanels.Add(this);
        }
    }

    private void OnDisable()
    {
        ActivePanels.Remove(this);
    }

    private void OnDestroy()
    {
        ActivePanels.Remove(this);
    }

    private Transform ResolveNavigationRoot()
    {
        SkillTreeView skillTreeView = GetComponentInParent<SkillTreeView>(true);

        if (skillTreeView != null)
        {
            return skillTreeView.transform;
        }

        return transform;
    }

    private GameObject ResolveCancelTarget()
    {
        if (IsUsableCancelTarget(cancelTarget))
        {
            return cancelTarget;
        }

        cancelTarget = null;

        if (string.Equals(gameObject.name, "PausePanel", StringComparison.OrdinalIgnoreCase))
        {
            Button[] pauseButtons = GetComponentsInChildren<Button>(true);

            foreach (Button pauseButton in pauseButtons)
            {
                if (IsUsableButton(pauseButton) && string.Equals(pauseButton.name, "Continue", StringComparison.OrdinalIgnoreCase))
                {
                    cancelTarget = pauseButton.gameObject;
                    return cancelTarget;
                }
            }
        }

        Transform navigationRoot = NavigationRoot;

        if (navigationRoot == null)
        {
            return null;
        }

        Button[] buttons = navigationRoot.GetComponentsInChildren<Button>(true);

        foreach (string targetName in AutomaticCancelTargetNames)
        {
            foreach (Button button in buttons)
            {
                if (!IsUsableButton(button))
                {
                    continue;
                }

                if (!string.Equals(button.gameObject.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cancelTarget = button.gameObject;
                return cancelTarget;
            }
        }

        return null;
    }

    private static bool IsUsableCancelTarget(GameObject target)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return false;
        }

        Button button = target.GetComponent<Button>();
        return IsUsableButton(button);
    }

    private static bool IsUsableButton(Button button)
    {
        return button != null && button.enabled && button.gameObject.activeInHierarchy && button.interactable;
    }

    private static bool IsVisibleAndInteractive(Transform root)
    {
        Transform current = root;

        while (current != null)
        {
            CanvasGroup[] groups = current.GetComponents<CanvasGroup>();

            foreach (CanvasGroup group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                if (group.alpha <= 0.001f || !group.interactable || !group.blocksRaycasts)
                {
                    return false;
                }

                if (group.ignoreParentGroups)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return true;
    }

    private static void CleanupActivePanels()
    {
        for (int panelIndex = ActivePanels.Count - 1; panelIndex >= 0; panelIndex--)
        {
            if (ActivePanels[panelIndex] == null)
            {
                ActivePanels.RemoveAt(panelIndex);
            }
        }
    }
}
