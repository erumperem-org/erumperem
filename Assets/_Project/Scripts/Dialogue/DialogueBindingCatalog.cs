
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ErumperemDialogueBindings", menuName = "Erumperem/Dialogue/Runtime Bindings")]
public sealed class DialogueBindingCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("Nome exato do GameObject com NpcInteractable ou PlayableNpcInteractable.")]
        public string NpcObjectName;

        public DialogueConversation Conversation;

        [Tooltip("Executa os eventos originais somente após o encerramento da conversa.")]
        public bool RunOriginalInteractionAfterDialogue = true;
    }

    public List<Entry> Entries = new List<Entry>();
}
