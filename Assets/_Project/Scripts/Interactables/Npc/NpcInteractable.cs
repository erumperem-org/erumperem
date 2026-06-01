using TMPro;
using UnityEngine;

/// <summary>
/// NPC que exibe um diálogo simples quando interagido.
///
/// MUDANÇAS:
///   - Havia duas classes quase idênticas (<c>NpcInteractable</c> e <c>NPCInteractable</c>)
///     com encoding corrompido na segunda — unificadas em uma só.
///   - <c>dialogueViewText</c> e <c>dialogueCanvas</c> eram <c>public</c>; agora [SerializeField].
///   - <c>Camera.main</c> cacheado em Awake (era chamado a cada LateUpdate nas versões anteriores).
///   - <c>ExecuteInteraction</c> usa <see cref="InteractionContext"/> — não depende de controller.
/// </summary>
public sealed class NpcInteractable : Interactable
{
    [TextArea]
    [SerializeField] private string _dialogue;
    [SerializeField] private TMP_Text _dialogueText;
    [SerializeField] private Canvas   _dialogueCanvas;

    public override bool CanInteract => true;

    private Transform _camTransform;

    protected override void Awake()
    {
        base.Awake();
        // Camera.main é uma busca por tag — cacheamos uma única vez.
        _camTransform = Camera.main != null ? Camera.main.transform : null;

        if (_dialogueCanvas != null)
            _dialogueCanvas.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_camTransform == null || _dialogueCanvas == null || !_dialogueCanvas.gameObject.activeSelf)
            return;

        _dialogueCanvas.transform.LookAt(_camTransform);
        _dialogueCanvas.transform.Rotate(0f, 180f, 0f);
    }

    public override void ExecuteInteraction(InteractionContext context)
    {
        if (_dialogueCanvas == null || _dialogueText == null) return;
        _dialogueText.text = _dialogue;
        _dialogueCanvas.gameObject.SetActive(true);
    }
}
