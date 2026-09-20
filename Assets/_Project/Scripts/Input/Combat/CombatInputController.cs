using System;
using System.Collections.Generic;
using System.Linq;
using Erumperem.Combat;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum CombatInputPhase
{
    Inactive,
    SkillSelection,
    TargetSelection
}

[DisallowMultipleComponent]
public sealed class CombatInputController : MonoBehaviour
{
    [SerializeField] private InfinitySkillScroll skillScroll;
    [Header("Input")]
    [SerializeField] private CombatInputReader inputReader;

    [Header("Combat")]
    [SerializeField] private CombatPrototypeController combatSession;
    [SerializeField] private CombatSkillButtonBarUIManager skillBarUiManager;

    [Header("Enemy Direction Mapping")]
    [Tooltip("FrontRank do inimigo selecionado quando o jogador aperta Cima.")]
    [SerializeField, Min(1)] private int upEnemyRank = 1;

    [Tooltip("FrontRank do inimigo selecionado quando o jogador aperta Baixo.")]
    [SerializeField, Min(1)] private int downEnemyRank = 3;

    [Tooltip("FrontRank do inimigo selecionado quando o jogador aperta Esquerda.")]
    [SerializeField, Min(1)] private int leftEnemyRank = 4;

    [Tooltip("FrontRank do inimigo selecionado quando o jogador aperta Direita.")]
    [SerializeField, Min(1)] private int rightEnemyRank = 2;

    private CombatInputPhase _phase;
    private SkillButtonPanelView _focusedSkillPanel;
    private int? _focusedSkillSlotIndex;
    private string _focusedTargetCombatantId;

    public event Action<CombatInputPhase> PhaseChanged;
    public event Action<int?> SkillFocusChanged;
    public event Action<string> TargetFocusChanged;

    public CombatInputPhase CurrentPhase => _phase;
    public int? FocusedSkillSlotIndex => _focusedSkillSlotIndex;
    public string FocusedTargetCombatantId => _focusedTargetCombatantId;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeInput();
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        ClearSkillHover();
        SetFocusedTarget(null);
        SetPhase(CombatInputPhase.Inactive);
    }

    private void Update()
    {
        ResolveReferences();
        SyncPhaseWithCombat();
    }

    private void ResolveReferences()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<CombatInputReader>();
        }

        if (combatSession == null)
        {
            combatSession = FindFirstObjectByType<CombatPrototypeController>();
        }

        if (skillBarUiManager == null)
        {
            skillBarUiManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
        }
    }

    private void SubscribeInput()
    {
        if (inputReader == null) return;

        inputReader.NavigateRequested -= HandleNavigateRequested;
        inputReader.ConfirmRequested -= HandleConfirmRequested;
        inputReader.CancelRequested -= HandleCancelRequested;
        inputReader.NavigateRequested += HandleNavigateRequested;
        inputReader.ConfirmRequested += HandleConfirmRequested;
        inputReader.CancelRequested += HandleCancelRequested;
    }

    private void UnsubscribeInput()
    {
        if (inputReader == null) return;

        inputReader.NavigateRequested -= HandleNavigateRequested;
        inputReader.ConfirmRequested -= HandleConfirmRequested;
        inputReader.CancelRequested -= HandleCancelRequested;
    }

    private void SyncPhaseWithCombat()
    {
        var desiredPhase = ResolveDesiredPhase();

        if (desiredPhase == _phase)
        {
            if (_phase == CombatInputPhase.SkillSelection && !IsFocusedSkillPanelUsable())
            {
                FocusInitialSkill();
            }

            return;
        }

        SetPhase(desiredPhase);

        if (_phase == CombatInputPhase.SkillSelection)
        {
            SetFocusedTarget(null);
            FocusInitialSkill();
            return;
        }

        if (_phase == CombatInputPhase.TargetSelection)
        {
            ClearSkillHover();
            PrepareInitialTarget();
            return;
        }

        ClearSkillHover();
        SetFocusedTarget(null);
    }

    private CombatInputPhase ResolveDesiredPhase()
    {
        if (combatSession == null || !combatSession.IsBattleOngoing || combatSession.IsActionPresentationOngoing)
        {
            return CombatInputPhase.Inactive;
        }

        var pendingCombatantId = combatSession.PendingPlayerCombatantId;

        if (string.IsNullOrEmpty(pendingCombatantId))
        {
            return CombatInputPhase.Inactive;
        }

        var pendingCombatant = combatSession.FindCombatantById(pendingCombatantId);

        if (pendingCombatant == null || !combatSession.IsPlayerCommandingCombatant(pendingCombatant))
        {
            return CombatInputPhase.Inactive;
        }

        combatSession.GetSkillBarSelection(out var selectedSlot, out var selectedOwnerCombatantId);

        if (selectedSlot.HasValue && string.Equals(selectedOwnerCombatantId, pendingCombatantId, StringComparison.Ordinal))
        {
            return CombatInputPhase.TargetSelection;
        }

        return CombatInputPhase.SkillSelection;
    }

    private void SetPhase(CombatInputPhase phase)
    {
        if (_phase == phase) return;

        _phase = phase;
        PhaseChanged?.Invoke(_phase);
    }

    private void HandleNavigateRequested(CombatInputDirection direction)
    {
        if (_phase == CombatInputPhase.SkillSelection)
        {
            NavigateSkills(direction);
            return;
        }

        if (_phase == CombatInputPhase.TargetSelection)
        {
            NavigateTargets(direction);
        }
    }

    private void HandleConfirmRequested()
    {
        if (_phase == CombatInputPhase.SkillSelection)
        {
            ConfirmSkill();
            return;
        }

        if (_phase == CombatInputPhase.TargetSelection)
        {
            ConfirmTarget();
        }
    }

    private void HandleCancelRequested()
    {
        if (_phase != CombatInputPhase.TargetSelection || combatSession == null)
        {
            return;
        }

        combatSession.ClearSkillBarSelection();
        SetFocusedTarget(null);
        SyncPhaseWithCombat();
    }

    private void NavigateSkills(CombatInputDirection direction)
    {
        var panels = GetInteractableSkillPanels();

        if (panels.Count == 0)
        {
            ClearSkillHover();
            return;
        }

        var currentIndex = _focusedSkillPanel != null ? panels.IndexOf(_focusedSkillPanel) : -1;

        if (currentIndex < 0)
        {
            FocusSkillPanel(panels[0]);
            return;
        }

        var step = direction == CombatInputDirection.Left || direction == CombatInputDirection.Up ? -1 : 1;
        var nextIndex = (currentIndex + step + panels.Count) % panels.Count;
    
        FocusSkillPanel(panels[nextIndex]);
    }

    private bool IsFocusedSkillPanelUsable()
    {
        if (_focusedSkillPanel == null || !_focusedSkillPanel.gameObject.activeInHierarchy)
        {
            return false;
        }

        var button = _focusedSkillPanel.GetComponentInChildren<Button>(true);
        return button != null && button.interactable;
    }

    private void FocusInitialSkill()
    {
        var panels = GetInteractableSkillPanels();

        if (panels.Count == 0)
        {
            ClearSkillHover();
            return;
        }

        if (_focusedSkillSlotIndex.HasValue)
        {
            var matchingPanel = panels.FirstOrDefault(panel => panel.ZeroBasedSlotIndex == _focusedSkillSlotIndex.Value);

            if (matchingPanel != null)
            {
                FocusSkillPanel(matchingPanel);
                return;
            }
        }

        FocusSkillPanel(panels[0]);
    }

    private List<SkillButtonPanelView> GetInteractableSkillPanels()
    {
        var result = new List<SkillButtonPanelView>();
        var row = skillBarUiManager != null ? skillBarUiManager.SkillsRowView : null;

        if (row == null) return result;

        var panels = row.GetComponentsInChildren<SkillButtonPanelView>(true);

        for (var panelIndex = 0; panelIndex < panels.Length; panelIndex++)
        {
            var panel = panels[panelIndex];

            if (panel == null || !panel.gameObject.activeInHierarchy) continue;

            var button = panel.GetComponentInChildren<Button>(true);

            if (button == null || !button.interactable) continue;

            result.Add(panel);
        }

        result.Sort((left, right) => left.ZeroBasedSlotIndex.CompareTo(right.ZeroBasedSlotIndex));
        return result;
    }

    private void FocusSkillPanel(SkillButtonPanelView panel)
    {
        if (panel == null || ReferenceEquals(panel, _focusedSkillPanel))
        {
            return;
        }

        if (_focusedSkillPanel != null)
        {
            _focusedSkillPanel.HandlePointerExit();
        }

        _focusedSkillPanel = panel;
        _focusedSkillSlotIndex = panel.ZeroBasedSlotIndex;
        _focusedSkillPanel.HandlePointerEnter();

        var button = _focusedSkillPanel.GetComponentInChildren<Button>(true);

        if (button != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        if (skillScroll != null)
        {
            skillScroll.ScrollToItem(panel.transform.GetSiblingIndex());
        }

        SkillFocusChanged?.Invoke(_focusedSkillSlotIndex);
    }

    private void ClearSkillHover()
    {
        if (_focusedSkillPanel != null)
        {
            _focusedSkillPanel.HandlePointerExit();
            _focusedSkillPanel = null;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void ConfirmSkill()
    {
        if (combatSession == null) return;

        if (_focusedSkillPanel == null)
        {
            FocusInitialSkill();
        }

        if (_focusedSkillPanel == null) return;

        var ownerCombatantId = combatSession.PendingPlayerCombatantId;

        if (string.IsNullOrEmpty(ownerCombatantId)) return;

        var selectedSlot = _focusedSkillPanel.ZeroBasedSlotIndex;

        if (!combatSession.TrySelectSkillBarSlot(ownerCombatantId, selectedSlot))
        {
            combatSession.NotifySkillBarSlotRequestFailed(selectedSlot);
            return;
        }

        _focusedSkillSlotIndex = selectedSlot;
        SyncPhaseWithCombat();
    }

    // ── Target Navigation (Teclado / Gamepad) ────────────────────────────

    private void NavigateTargets(CombatInputDirection direction)
    {
        if (!TryGetSelectedSkillContext(out _, out _, out var skill, out var candidates))
        {
            return;
        }

        if (!IsEnemyTarget(skill))
        {
            return;
        }

        if (!CombatDirectionalTargetResolver.TryResolve(candidates, direction, upEnemyRank, downEnemyRank, leftEnemyRank, rightEnemyRank, out var target))
        {
            return;
        }

        PreviewTarget(target);
    }

    private void PrepareInitialTarget()
    {
        if (!TryGetSelectedSkillContext(out var actor, out _, out var skill, out var candidates))
        {
            SetFocusedTarget(null);
            return;
        }

        if (IsSelfTarget(skill))
        {
            SetFocusedTarget(actor.Identity.Id);
            return;
        }

        if (IsAllyTarget(skill))
        {
            if (candidates.Count > 0)
            {
                SetFocusedTarget(candidates[0].Identity.Id);
            }

            return;
        }

        var currentSelectedEnemy = combatSession.CurrentSelectedEnemy;

        if (currentSelectedEnemy != null && candidates.Contains(currentSelectedEnemy))
        {
            SetFocusedTarget(currentSelectedEnemy.Identity.Id);
            return;
        }

        if (candidates.Count == 1)
        {
            PreviewTarget(candidates[0]);
            return;
        }

        SetFocusedTarget(null);
    }

    private void PreviewTarget(Combatant target)
    {
        if (target == null || combatSession == null)
        {
            return;
        }

        if (!CombatExistingTargetInputAdapter.TryPreviewTarget(combatSession, target))
        {
            return;
        }

        SetFocusedTarget(target.Identity.Id);
    }

    private void ConfirmTarget()
    {
        if (combatSession == null) return;

        if (string.IsNullOrEmpty(_focusedTargetCombatantId))
        {
            PrepareInitialTarget();
        }

        if (string.IsNullOrEmpty(_focusedTargetCombatantId))
        {
            return;
        }

        var target = combatSession.FindCombatantById(_focusedTargetCombatantId);

        if (target == null) return;

        if (!CombatExistingTargetInputAdapter.TryConfirmTarget(combatSession, target))
        {
            return;
        }

        SetFocusedTarget(null);
        ClearSkillHover();
        SetPhase(CombatInputPhase.Inactive);
    }

    private void SetFocusedTarget(string combatantId)
    {
        if (string.Equals(_focusedTargetCombatantId, combatantId, StringComparison.Ordinal))
        {
            return;
        }

        _focusedTargetCombatantId = combatantId;
        TargetFocusChanged?.Invoke(_focusedTargetCombatantId);
    }

    private bool TryGetSelectedSkillContext(out Combatant actor, out int zeroBasedSlot, out SkillDefinition skill, out List<Combatant> validTargets)
    {
        actor = null;
        zeroBasedSlot = default;
        skill = null;
        validTargets = new List<Combatant>();

        if (combatSession == null || combatSession.BattleState == null || combatSession.BattleSimulator == null)
        {
            return false;
        }

        combatSession.GetSkillBarSelection(out var selectedSlot, out var selectedOwnerCombatantId);

        if (!selectedSlot.HasValue || string.IsNullOrEmpty(selectedOwnerCombatantId))
        {
            return false;
        }

        actor = combatSession.FindCombatantById(selectedOwnerCombatantId);

        if (actor == null || actor.Health.IsDead || !combatSession.IsPlayerCommandingCombatant(actor))
        {
            return false;
        }

        zeroBasedSlot = selectedSlot.Value;
        var battleState = combatSession.BattleState;
        var skillIds = actor.SkillLoadout.Skills.Where(skillId => battleState.SkillsById.ContainsKey(skillId)).Take(CharacterSkillButtonsRowView.MaxVisibleSlots).ToList();

        if (zeroBasedSlot < 0 || zeroBasedSlot >= skillIds.Count)
        {
            return false;
        }

        if (!battleState.SkillsById.TryGetValue(skillIds[zeroBasedSlot], out skill))
        {
            return false;
        }

        var candidatePool = ResolveCandidatePool(battleState, actor, skill);

        for (var candidateIndex = 0; candidateIndex < candidatePool.Count; candidateIndex++)
        {
            var candidate = candidatePool[candidateIndex];

            if (candidate == null || candidate.Health.IsDead) continue;

            if (PlayerActionBuilder.TryCreate(battleState, combatSession.BattleSimulator, actor, zeroBasedSlot, candidate) != null)
            {
                validTargets.Add(candidate);
            }
        }

        return true;
    }

    private static bool IsEnemyTarget(SkillDefinition skill)
    {
        return skill != null && (int)skill.TargetKind == 0;
    }

    private static bool IsAllyTarget(SkillDefinition skill)
    {
        return skill != null && (int)skill.TargetKind == 1;
    }

    private static bool IsSelfTarget(SkillDefinition skill)
    {
        return skill != null && (int)skill.TargetKind == 2;
    }

    private static List<Combatant> ResolveCandidatePool(BattleState battleState, Combatant actor, SkillDefinition skill)
    {
        if (IsSelfTarget(skill))
        {
            return new List<Combatant> { actor };
        }

        if (IsAllyTarget(skill))
        {
            return (actor.Position.Side == Side.Allies ? battleState.Allies : battleState.Enemies).ToList();
        }

        return (actor.Position.Side == Side.Allies ? battleState.Enemies : battleState.Allies).ToList();
    }
}

//fiz umas mudanças nesses códigos que referenciam ou são o proprio hover marker p evitar uns erros de áudio que estavam acontecendo