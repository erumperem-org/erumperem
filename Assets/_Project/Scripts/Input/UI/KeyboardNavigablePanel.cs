using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KeyboardNavigablePanel : MonoBehaviour
{
    [Header("Selection")]
    [Tooltip("Opcional. Elemento que recebe foco quando este painel abre. Se vazio, o sistema escolhe automaticamente o elemento navegável mais acima e à esquerda.")]
    [SerializeField] private GameObject defaultSelected;

    [Tooltip("Opcional. Elemento acionado por Escape ou Q. Use aqui o botão Voltar/Fechar do próprio painel.")]
    [SerializeField] private GameObject cancelTarget;

    [Header("Navigation")]
    [SerializeField] private bool wrapNavigation = true;

    [Tooltip("Se mais de um KeyboardNavigablePanel estiver ativo, o maior valor tem prioridade.")]
    [SerializeField] private int navigationPriority;

    private static readonly List<KeyboardNavigablePanel> ActivePanels = new();
    private static long _nextActivationOrder;
    private long _activationOrder;

    public GameObject DefaultSelected => defaultSelected;
    public GameObject CancelTarget => cancelTarget;
    public bool WrapNavigation => wrapNavigation;
    public int NavigationPriority => navigationPriority;

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
