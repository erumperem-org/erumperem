using System.Collections;
using Player;
using Services.DebugUtilities;
using UnityEngine;

public sealed class DoorInteractable : Interactable
{
    [SerializeField] public bool opened;
    [SerializeField] private Animator _animator;

    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int ResetTrigger = Animator.StringToHash("Reset");

    /// <summary>
    /// Verdadeiro enquanto a animação de abrir/fechar ainda está em execução.
    /// Bloqueia novas interações durante esse período.
    /// </summary>
    private bool _isAnimating;

    /// <summary>
    /// Bloqueia interação enquanto a animação roda, evitando
    /// que triggers se acumulem e o estado fique inconsistente.
    /// </summary>
    public override bool CanInteract => !_isAnimating;
    void Awake()
    {
        base.Init();
    }

    // ── Interação ──────────────────────────────────────────────────────────

    public override void ExecuteInteraction(PlayerMovementController controller)
    {
        // Dupla checagem: o PlayerDetectionSystem já verifica CanInteract,
        // mas protegemos aqui também contra chamadas diretas.
        if (_isAnimating) return;

        opened = !opened;

        ApplyAnimatorState();

        controller._inputReader.IsPlayerInteracting = true;
        StartCoroutine(ReenableAfterAnimation(controller));
    }

    // ── Reset ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Força a porta para o estado fechado, cancelando qualquer animação em curso.
    /// </summary>
    public void ResetDoor()
    {
        // Cancela coroutines pendentes para evitar que ReenableAfterAnimation
        // reabilite o movimento de um controller que pode já não existir.
        StopAllCoroutines();
        _isAnimating = false;
        opened = false;

        if (_animator != null)
        {
            _animator.SetTrigger(ResetTrigger);
        }

        LoggerService.PrintLogMessage(LogLevel.Debug, "Door reset",
            LogCategory.Environment, LogCategory.Interaction);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void ApplyAnimatorState()
    {
        if (_animator == null) return;

        if (opened)
        {
            _animator.SetTrigger(OpenTrigger);
            LoggerService.PrintLogMessage(LogLevel.Debug, "Door opened",
                LogCategory.Environment, LogCategory.Interaction);
        }
        else
        {
            _animator.SetTrigger(ResetTrigger);
            LoggerService.PrintLogMessage(LogLevel.Debug, "Door closed",
                LogCategory.Environment, LogCategory.Interaction);
        }
    }
}