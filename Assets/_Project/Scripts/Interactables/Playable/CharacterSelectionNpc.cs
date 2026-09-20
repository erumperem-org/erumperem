using UnityEngine;

/// <summary>
/// NPC que abre o canvas de seleção de personagem quando interagido.
/// Não depende de <c>PlayerMovementController</c> — usa <see cref="InteractionContext"/>.
/// </summary>
[RequireComponent(typeof(InteractionOutline))]
public sealed class CharacterSelectionNpc : Interactable
{
    [SerializeField] private PlayableCharacter      _character;
    [SerializeField] public CharacterSelectionCanvas _canvas;

    private VillageArea _village;
    private PlayableCharactersManager _charactersManager;

    public override bool CanShowInteractionFeedback => CanInteract;

    public override bool CanInteract =>
        _character != null
        && _character.CurrentState != PlayableCharacterState.Main
        && _village != null && _village.isActiveAndEnabled
        && _village.ContainsPosition(_character.transform.position)
        && _charactersManager != null && _charactersManager.Main != null
        && _village.ContainsPosition(_charactersManager.Main.Transform.position);

    protected override void Awake()
    {
        base.Awake();
        if (_character == null)
            _character = GetComponentInParent<PlayableCharacter>();
        _village = FindFirstObjectByType<VillageArea>();
        _charactersManager = FindFirstObjectByType<PlayableCharactersManager>();
        EnsureInteractionOutline();
    }

    public override void ExecuteInteraction(InteractionContext context)
    {
        if (!CanInteract || _canvas == null) return;
        _canvas.Open(_character);
    }
}
