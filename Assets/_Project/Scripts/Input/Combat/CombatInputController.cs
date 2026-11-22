using System;
using System.Collections.Generic;
using System.Linq;
using Erumperem.Combat;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

public enum CombatInputPhase
{
    Inactive,
    SkillSelection,
    TargetSelection
}

[DisallowMultipleComponent]
public sealed class CombatInputController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private CombatInputReader inputReader;

    [Header("Combat")]
    [SerializeField] private CombatPrototypeController combatSession;
    [SerializeField] private CombatSkillButtonBarUIManager skillBarUiManager;

    [Header("Enemy Direction Mapping - Stable Slots")]
    [Tooltip("Slot lógico fixo do inimigo selecionado quando o jogador aperta Cima.")]
    [SerializeField, Min(1)] private int upEnemyRank = 1;

    [Tooltip("Slot lógico fixo do inimigo selecionado quando o jogador aperta Baixo.")]
    [SerializeField, Min(1)] private int downEnemyRank = 3;

    [Tooltip("Slot lógico fixo do inimigo selecionado quando o jogador aperta Esquerda.")]
    [SerializeField, Min(1)] private int leftEnemyRank = 4;

    [Tooltip("Slot lógico fixo do inimigo selecionado quando o jogador aperta Direita.")]
    [SerializeField, Min(1)] private int rightEnemyRank = 2;

    private CombatInputPhase _phase;
    private string _focusedTargetCombatantId;

    public event Action<CombatInputPhase> PhaseChanged;
    public event Action<string> TargetFocusChanged;

    public CombatInputPhase CurrentPhase => _phase;
    public string FocusedTargetCombatantId => _focusedTargetCombatantId;

    private CombatSkillBarSelectionController SkillBarSelection =>
        skillBarUiManager != null ? skillBarUiManager.SelectionController : null;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeInput();
        SubscribeInputMode();
    }

    private void OnDisable()
    {
        UnsubscribeInput();
        UnsubscribeInputMode();
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
        if (inputReader == null)
        {
            return;
        }

        inputReader.NavigateRequested -= HandleNavigateRequested;
        inputReader.ConfirmRequested -= HandleConfirmRequested;
        inputReader.CancelRequested -= HandleCancelRequested;

        inputReader.NavigateRequested += HandleNavigateRequested;
        inputReader.ConfirmRequested += HandleConfirmRequested;
        inputReader.CancelRequested += HandleCancelRequested;
    }

    private void UnsubscribeInput()
    {
        if (inputReader == null)
        {
            return;
        }

        inputReader.NavigateRequested -= HandleNavigateRequested;
        inputReader.ConfirmRequested -= HandleConfirmRequested;
        inputReader.CancelRequested -= HandleCancelRequested;
    }

    private void SubscribeInputMode()
    {
        if (InputDeviceModeManager.Instance == null)
        {
            return;
        }

        InputDeviceModeManager.Instance.ModeChanged -= HandleInputModeChanged;
        InputDeviceModeManager.Instance.ModeChanged += HandleInputModeChanged;
    }

    private void UnsubscribeInputMode()
    {
        if (InputDeviceModeManager.Instance == null)
        {
            return;
        }

        InputDeviceModeManager.Instance.ModeChanged -= HandleInputModeChanged;
    }

    private void HandleInputModeChanged(InputControlMode mode)
    {
        if (mode == InputControlMode.Mouse)
        {
            SetFocusedTarget(null);
            return;
        }

        if (_phase == CombatInputPhase.TargetSelection)
        {
            PrepareInitialTarget();
        }
    }

    private void SyncPhaseWithCombat()
    {
        var desiredPhase = ResolveDesiredPhase();

        if (desiredPhase == _phase)
        {
            return;
        }

        SetPhase(desiredPhase);

        if (_phase == CombatInputPhase.TargetSelection)
        {
            PrepareInitialTarget();
            return;
        }

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
        if (_phase == phase)
        {
            return;
        }

        _phase = phase;
        PhaseChanged?.Invoke(_phase);
    }

    private void HandleNavigateRequested(CombatInputDirection direction)
    {
        InputDeviceModeManager.UseKeyboardMode();

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
        InputDeviceModeManager.UseKeyboardMode();

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
        InputDeviceModeManager.UseKeyboardMode();

        if (_phase != CombatInputPhase.TargetSelection || combatSession == null)
        {
            return;
        }

        combatSession.ClearSkillBarSelection();
        SetFocusedTarget(null);
        SyncPhaseWithCombat();
    }

    /// <summary>Esquerda/Cima anda para trás no carrossel, Direita/Baixo para frente (dá a volta na lista).</summary>
    private void NavigateSkills(CombatInputDirection direction)
    {
        var step =
            direction == CombatInputDirection.Left ||
            direction == CombatInputDirection.Up
                ? -1
                : 1;

        SkillBarSelection?.MoveCursor(step);
    }

    /// <summary>Espaço seleciona a skill do cursor — o mesmo caminho do clique do mouse.</summary>
    private void ConfirmSkill()
    {
        SkillBarSelection?.ConfirmCursor();
        SyncPhaseWithCombat();
    }

    private void NavigateTargets(CombatInputDirection direction)
    {
        if (!TryGetSelectedSkillContext(
                out var actor,
                out _,
                out var skill,
                out var candidates))
        {
            return;
        }

        /*
         * IMPORTANTE:
         *
         * Antes o código verificava:
         *
         * (int)skill.TargetKind == 0
         *
         * Isso aceitava somente OneEnemy.
         *
         * Agora usamos a classificação oficial do Game.Core.
         * Portanto entram:
         *
         * OneEnemy
         * UpToThreeEnemies
         * AllEnemies
         */
        if (!IsEnemyTarget(skill) ||
            combatSession == null ||
            combatSession.BattleState == null)
        {
            return;
        }

        var stableEnemyRoster =
            ResolveEnemyRoster(
                combatSession.BattleState,
                actor);

        if (!CombatDirectionalTargetResolver.TryResolve(
                stableEnemyRoster,
                candidates,
                direction,
                upEnemyRank,
                downEnemyRank,
                leftEnemyRank,
                rightEnemyRank,
                out var target))
        {
            return;
        }

        PreviewTarget(target);
    }

    private void PrepareInitialTarget()
    {
        if (!TryGetSelectedSkillContext(
                out var actor,
                out _,
                out var skill,
                out var candidates))
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
                SetFocusedTarget(
                    candidates[0].Identity.Id);
            }

            return;
        }

        if (!IsEnemyTarget(skill))
        {
            SetFocusedTarget(null);
            return;
        }

        var currentSelectedEnemy =
            combatSession.CurrentSelectedEnemy;

        if (currentSelectedEnemy != null &&
            candidates.Contains(currentSelectedEnemy))
        {
            SetFocusedTarget(
                currentSelectedEnemy.Identity.Id);

            return;
        }

        /*
         * Se houver apenas um inimigo válido, já usamos ele como alvo.
         * Isso também funciona para UpToThreeEnemies quando só resta
         * um inimigo vivo.
         */
        if (candidates.Count == 1)
        {
            PreviewTarget(candidates[0]);
            return;
        }

        /*
         * Com múltiplos inimigos deixamos o jogador escolher a direção.
         *
         * Isso é importante principalmente para UpToThreeEnemies,
         * pois o inimigo escolhido define qual inimigo oposto fica fora
         * da área.
         */
        SetFocusedTarget(null);
    }

    private void PreviewTarget(Combatant target)
    {
        if (target == null || combatSession == null)
        {
            return;
        }

        if (!CombatExistingTargetInputAdapter.TryPreviewTarget(
                combatSession,
                target))
        {
            return;
        }

        SetFocusedTarget(
            target.Identity.Id);
    }

    private void ConfirmTarget()
    {
        if (combatSession == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_focusedTargetCombatantId))
        {
            PrepareInitialTarget();
        }

        if (string.IsNullOrEmpty(_focusedTargetCombatantId))
        {
            return;
        }

        var target =
            combatSession.FindCombatantById(
                _focusedTargetCombatantId);

        if (target == null)
        {
            return;
        }

        /*
         * O adapter chama o mesmo pipeline usado pelo mouse:
         *
         * PlayerActionBuilder
         *      ↓
         * SkillTargetResolver
         *      ↓
         * BattleSimulator
         *
         * Assim UpToThreeEnemies usa a regra real do Game.Core.
         */
        if (!CombatExistingTargetInputAdapter.TryConfirmTarget(
                combatSession,
                target))
        {
            return;
        }

        SetFocusedTarget(null);
        SetPhase(CombatInputPhase.Inactive);
    }

    private void SetFocusedTarget(string combatantId)
    {
        if (string.Equals(
                _focusedTargetCombatantId,
                combatantId,
                StringComparison.Ordinal))
        {
            return;
        }

        _focusedTargetCombatantId =
            combatantId;

        TargetFocusChanged?.Invoke(
            _focusedTargetCombatantId);
    }

    private bool TryGetSelectedSkillContext(
        out Combatant actor,
        out int zeroBasedSlot,
        out SkillDefinition skill,
        out List<Combatant> validTargets)
    {
        actor = null;
        zeroBasedSlot = default;
        skill = null;
        validTargets =
            new List<Combatant>();

        if (combatSession == null ||
            combatSession.BattleState == null ||
            combatSession.BattleSimulator == null)
        {
            return false;
        }

        combatSession.GetSkillBarSelection(
            out var selectedSlot,
            out var selectedOwnerCombatantId);

        if (!selectedSlot.HasValue ||
            string.IsNullOrEmpty(selectedOwnerCombatantId))
        {
            return false;
        }

        actor =
            combatSession.FindCombatantById(
                selectedOwnerCombatantId);

        if (actor == null ||
            actor.Health.IsDead ||
            !combatSession.IsPlayerCommandingCombatant(actor))
        {
            return false;
        }

        zeroBasedSlot =
            selectedSlot.Value;

        var battleState =
            combatSession.BattleState;

        var skillIds =
            actor.SkillLoadout.Skills
                .Where(
                    skillId =>
                        battleState.SkillsById
                            .ContainsKey(skillId))
                .Take(
                    CharacterSkillButtonsRowView
                        .MaxVisibleSlots)
                .ToList();

        if (zeroBasedSlot < 0 ||
            zeroBasedSlot >= skillIds.Count)
        {
            return false;
        }

        if (!battleState.SkillsById.TryGetValue(
                skillIds[zeroBasedSlot],
                out skill))
        {
            return false;
        }

        var candidatePool =
            ResolveCandidatePool(
                battleState,
                actor,
                skill);

        for (var candidateIndex = 0;
             candidateIndex < candidatePool.Count;
             candidateIndex++)
        {
            var candidate =
                candidatePool[candidateIndex];

            if (candidate == null)
            {
                continue;
            }

            /*
             * Mantemos mortos fora da seleção normal.
             * Essa regra é a mesma que já existia aqui.
             */
            if (candidate.Health.IsDead)
            {
                continue;
            }

            /*
             * Não assumimos que um alvo é válido só por estar no roster.
             *
             * O PlayerActionBuilder é usado como validação final,
             * portanto Taunt, regras da skill e SkillTargetResolver
             * continuam sendo respeitados.
             */
            if (PlayerActionBuilder.TryCreate(
                    battleState,
                    combatSession.BattleSimulator,
                    actor,
                    zeroBasedSlot,
                    candidate) != null)
            {
                validTargets.Add(candidate);
            }
        }

        return true;
    }

    private static bool IsEnemyTarget(
        SkillDefinition skill)
    {
        if (skill == null)
        {
            return false;
        }

        return SkillTargetKindRules
            .DirectsPrimaryDamageAtEnemies(
                skill.TargetKind);
    }

    private static bool IsAllyTarget(
        SkillDefinition skill)
    {
        if (skill == null)
        {
            return false;
        }

        return SkillTargetKindRules
            .DirectsPrimaryDamageAtAllies(
                skill.TargetKind);
    }

    private static bool IsSelfTarget(
        SkillDefinition skill)
    {
        if (skill == null)
        {
            return false;
        }

        return SkillTargetKindRules
            .IsSelfOnly(
                skill.TargetKind);
    }

    private static IReadOnlyList<Combatant> ResolveEnemyRoster(
        BattleState battleState,
        Combatant actor)
    {
        if (battleState == null ||
            actor == null)
        {
            return Array.Empty<Combatant>();
        }

        return actor.Position.Side == Side.Allies
            ? battleState.Enemies.ToList()
            : battleState.Allies.ToList();
    }

    private static List<Combatant> ResolveCandidatePool(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill)
    {
        if (IsSelfTarget(skill))
        {
            return new List<Combatant>
            {
                actor
            };
        }

        if (IsAllyTarget(skill))
        {
            return actor.Position.Side == Side.Allies
                ? battleState.Allies.ToList()
                : battleState.Enemies.ToList();
        }

        if (IsEnemyTarget(skill))
        {
            return actor.Position.Side == Side.Allies
                ? battleState.Enemies.ToList()
                : battleState.Allies.ToList();
        }

        return new List<Combatant>();
    }
}