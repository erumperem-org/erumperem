using UnityEngine;
using TMPro;
using Player;
public sealed class NPCInteractable : Interactable
{
    [TextArea]
    [SerializeField]
    private string dialogue;

    public override bool CanInteract => true;

    public TMP_Text dialogueViewText;
    public Canvas dialogueCanvas;

    private Transform cam;

    private void Awake()
    {
        cam = Camera.main.transform;

        if (dialogueCanvas != null)
            dialogueCanvas.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (dialogueCanvas == null || !dialogueCanvas.gameObject.activeSelf)
            return;

        // Faz o canvas olhar para a c�mera
        dialogueCanvas.transform.LookAt(cam);

        // Corrige rota��o (UI n�o fica de cabe�a pra baixo)
        dialogueCanvas.transform.Rotate(0f, 180f, 0f);
    }

    public override void ExecuteInteraction(PlayerMovementController controller)
    {
        if (dialogueCanvas == null || dialogueViewText == null)
            return;

        dialogueViewText.text = dialogue;
        dialogueCanvas.gameObject.SetActive(true);
    }
}