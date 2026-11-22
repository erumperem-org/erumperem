
using System;
using System.Collections.Generic;
using Erumperem.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
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

    public GameObject DefaultSelected => ResolveDefaultSelected();
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

    private GameObject ResolveDefaultSelected()
    {
        if (defaultSelected != null && defaultSelected.activeInHierarchy)
        {
            return defaultSelected;
        }

        if (string.Equals(gameObject.name, "MainMenuPanel", StringComparison.OrdinalIgnoreCase))
        {
            return FindActiveControlByName("NewGame");
        }

        if (string.Equals(gameObject.name, "PausePanel", StringComparison.OrdinalIgnoreCase))
        {
            return FindActiveControlByName("Continue");
        }

        return null;
    }

    private GameObject FindActiveControlByName(string targetName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.gameObject.activeInHierarchy && string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase) && IsUsableCancelTarget(candidate.gameObject))
            {
                return candidate.gameObject;
            }
        }

        return null;
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
            cancelTarget = FindActiveControlByName("Continue");
            return cancelTarget;
        }

        foreach (string targetName in AutomaticCancelTargetNames)
        {
            GameObject target = FindActiveControlByName(targetName);

            if (target != null)
            {
                cancelTarget = target;
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

        Selectable selectable = target.GetComponent<Selectable>();

        if (selectable != null && (!selectable.enabled || !selectable.IsInteractable()))
        {
            return false;
        }

        MonoBehaviour[] behaviours = target.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null && behaviour.isActiveAndEnabled && (behaviour is IPointerDownHandler || behaviour is IPointerClickHandler || behaviour is ISubmitHandler))
            {
                return true;
            }
        }

        return selectable != null && selectable.isActiveAndEnabled;
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
