using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [SerializeField]
    private DialogueConversation conversation;

    public void StartDialogue()
    {
        if (conversation == null)
        {
            Debug.LogWarning($"No conversation assigned to {name}.");

            return;
        }

        DialogueManager.Instance.StartDialogue(conversation);
    }
}