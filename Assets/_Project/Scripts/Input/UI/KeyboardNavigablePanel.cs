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
    [Tooltip("Opcional. Elemento que recebe foco quando este painel abre. Se vazio, o sistema escolhe automaticamente um elemento navegável.")]
    [SerializeField] private GameObject defaultSelected;

    [Tooltip("Opcional. Elemento acionado por Escape ou Q. Se vazio, o sistema procura automaticamente um botão de voltar/fechar.")]
    [SerializeField] private GameObject cancelTarget;

    [Header("Navigation")]
    [SerializeField] private bool wrapNavigation = true;

    [Tooltip("Se mais de um KeyboardNavigablePanel estiver ativo, o maior valor tem prioridade.")]
    [SerializeField] private int navigationPriority;

    private static readonly List<KeyboardNavigablePanel> ActivePanels = new();
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

            for (var panelIndex = 0; panelIndex < ActivePanels.Count; panelIndex++)
            {
                var panel = ActivePanels[panelIndex];

                if (panel == null || !panel.isActiveAndEnabled || !panel.gameObject.activeInHierarchy)
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
        var skillTreeView = GetComponentInParent<SkillTreeView>(true);

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

        var navigationRoot = NavigationRoot;

        if (navigationRoot == null)
        {
            return null;
        }

        var buttons = navigationRoot.GetComponentsInChildren<Button>(true);

        for (var nameIndex = 0; nameIndex < AutomaticCancelTargetNames.Length; nameIndex++)
        {
            var targetName = AutomaticCancelTargetNames[nameIndex];

            for (var buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
            {
                var button = buttons[buttonIndex];

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

        var button = target.GetComponent<Button>();
        return IsUsableButton(button);
    }

    private static bool IsUsableButton(Button button)
    {
        return button != null && button.enabled && button.gameObject.activeInHierarchy && button.interactable;
    }

    private static void CleanupActivePanels()
    {
        for (var panelIndex = ActivePanels.Count - 1; panelIndex >= 0; panelIndex--)
        {
            if (ActivePanels[panelIndex] == null)
            {
                ActivePanels.RemoveAt(panelIndex);
            }
        }
    }
}