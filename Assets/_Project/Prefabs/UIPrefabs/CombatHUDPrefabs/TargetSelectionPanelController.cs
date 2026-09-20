using UnityEngine;

[DisallowMultipleComponent]
public class TargetSelectionPanelController : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("O controlador de input do combate. Se deixado nulo, o script tentará encontrar na cena.")]
    [SerializeField] private CombatInputController inputController;

    [Tooltip("O painel da UI que deve ser ativado Apenas durante a seleção de alvos/inimigos.")]
    [SerializeField] private GameObject targetSelectionPanel;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (inputController != null)
        {
            // Inscreve no evento para atualizar instantaneamente quando a fase mudar
            inputController.PhaseChanged += HandlePhaseChanged;
            
            // Sincroniza o estado inicial com a fase atual
            UpdatePanelState(inputController.CurrentPhase);
        }
    }

    private void OnDisable()
    {
        if (inputController != null)
        {
            inputController.PhaseChanged -= HandlePhaseChanged;
        }
    }

    private void ResolveReferences()
    {
        if (inputController == null)
        {
            inputController = FindFirstObjectByType<CombatInputController>();
        }
    }

    private void HandlePhaseChanged(CombatInputPhase newPhase)
    {
        UpdatePanelState(newPhase);
    }

    private void UpdatePanelState(CombatInputPhase phase)
    {
        if (targetSelectionPanel == null)
        {
            return;
        }

        // Ativa apenas se a fase for TargetSelection; desativa se for SkillSelection ou Inactive
        bool isTargeting = (phase == CombatInputPhase.TargetSelection);
        
        if (targetSelectionPanel.activeSelf != isTargeting)
        {
            targetSelectionPanel.SetActive(isTargeting);
        }
    }
}
