using UnityEngine;

public class DialogueTest : MonoBehaviour
{
    [SerializeField] private DialogueTrigger dialogueTrigger;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            dialogueTrigger.StartDialogue();
        }
    }
}