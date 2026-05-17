using Player;
using TMPro;
using UnityEngine;

public sealed class NpcInteractable : Interactable
{
    [TextArea]
    [SerializeField] private string dialogue;

    [SerializeField] private TMP_Text dialogueViewText;
    [SerializeField] private Canvas dialogueCanvas;

    public override bool CanInteract => true;

    private Transform _cam;

    private void Awake()
    {
        _cam = Camera.main.transform;

        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (dialogueCanvas == null || !dialogueCanvas.gameObject.activeSelf) return;

        // Faz o canvas olhar para a câmera e corrige rotação
        dialogueCanvas.transform.LookAt(_cam);
        dialogueCanvas.transform.Rotate(0f, 180f, 0f);
    }

    public override void ExecuteInteraction(PlayerMovementController controller)
    {
        if (dialogueCanvas == null || dialogueViewText == null) return;

        dialogueViewText.text = dialogue;
        dialogueCanvas.gameObject.SetActive(true);
    }
}
