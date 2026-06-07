using System.Collections;
using DetectionSystem.Core;
using UnityEngine;

/// <summary>
/// Contexto imutável passado a cada <see cref="Interactable.ExecuteInteraction"/>.
///
/// Desacopla o interactable de qualquer tipo concreto do domínio Player:
/// o interactable nunca precisa saber que existe um <c>PlayerMovementController</c>
/// ou um <c>PlayerInputReader</c> — recebe apenas o que precisa para agir.
/// </summary>
public sealed class InteractionContext
{
    /// <summary>
    /// Bloqueia e desbloqueia o input do jogador durante animações.
    /// </summary>
    public System.Action<bool> SetInputBlocked { get; }

    /// <summary>Inventário do personagem que iniciou a interação. Pode ser null.</summary>
    public PlayerInventorySystem Inventory { get; }

    public InteractionContext(System.Action<bool> setInputBlocked, PlayerInventorySystem inventory = null)
    {
        SetInputBlocked = setInputBlocked ?? (_ => { });
        Inventory       = inventory;
    }
}

/// <summary>
/// Base de todos os objetos interagíveis da cena.
///
/// MUDANÇAS em relação à versão anterior:
///   - <c>ExecuteInteraction</c> recebe <see cref="InteractionContext"/> em vez de
///     <c>PlayerMovementController</c> — quebra a dependência direta com o domínio Player.
///   - <c>Init()</c> removido do public: a autoconfiguração do receiver acontece em Awake.
///   - <c>ReenableAfterAnimation</c> usa o callback do contexto em vez de acessar
///     <c>_inputReader.IsPlayerInteracting</c> de fora.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Duração em segundos até o input ser reativado após a animação.")]
    [SerializeField] private float _reenableDelay = 9f;

    /// <summary>Receiver de detecção associado a este objeto. Preenchido em Awake.</summary>
    public DetectionReceiver Receiver { get; private set; }

    public abstract bool CanInteract { get; }

    public virtual void ExecuteInteraction(InteractionContext context) { }

    // ── Unity lifecycle ───────────────────────────────────────────────────

    protected virtual void Awake()
    {
        Receiver = GetComponent<DetectionReceiver>();
        if (Receiver is InteractableDetectionReceiver idr)
            idr.SetInteractable(this);
    }

    // ── Helpers protegidos ────────────────────────────────────────────────

    protected IEnumerator ReenableAfterAnimation(InteractionContext context)
    {
        yield return new WaitForSeconds(_reenableDelay);
        context.SetInputBlocked(false);
    }
}
