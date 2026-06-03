using System.Collections;
using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Porta interagível: abre/fecha com animação e bloqueia o input durante ela.
///
/// MUDANÇAS:
///   - <c>ExecuteInteraction(PlayerMovementController)</c> →
///     <c>ExecuteInteraction(InteractionContext)</c>.
///   - Acesso a <c>controller._inputReader.IsPlayerInteracting</c> eliminado;
///     agora usa <c>context.SetInputBlocked(true/false)</c>.
///   - <c>opened</c> era <c>public</c>; agora é propriedade com setter privado.
/// </summary>
public sealed class DoorInteractable : Interactable
{
    [SerializeField] private Animator _animator;
    [SerializeField] private bool     _startOpened;

    private static readonly int OpenTrigger  = Animator.StringToHash("Open");
    private static readonly int ResetTrigger = Animator.StringToHash("Reset");

    private bool _isAnimating;

    public bool IsOpened { get; private set; }

    public override bool CanInteract => !_isAnimating;

    protected override void Awake()
    {
        base.Awake();
        IsOpened = _startOpened;
    }

    // ── Interação ──────────────────────────────────────────────────────────

    public override void ExecuteInteraction(InteractionContext context)
    {
        if (_isAnimating) return;

        IsOpened    = !IsOpened;
        _isAnimating = true;

        ApplyAnimatorState();
        context.SetInputBlocked(true);
        StartCoroutine(ReenableAfterAnimation(context));
    }

    // ── Reset ──────────────────────────────────────────────────────────────

    public void ResetDoor()
    {
        StopAllCoroutines();
        _isAnimating = false;
        IsOpened     = false;

        _animator?.SetTrigger(ResetTrigger);

        LoggerService.PrintLogMessage(LogLevel.Debug, "Door reset",
            LogCategory.Environment, LogCategory.Interaction);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ApplyAnimatorState()
    {
        if (_animator == null) return;

        if (IsOpened)
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

    // ReenableAfterAnimation agora também reseta _isAnimating
    protected new IEnumerator ReenableAfterAnimation(InteractionContext context)
    {
        yield return base.ReenableAfterAnimation(context);
        _isAnimating = false;
    }
}
