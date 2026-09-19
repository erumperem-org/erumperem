using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewDialogueConversation",
    menuName = "Dialogue/Conversation"
)]
public class DialogueConversation : ScriptableObject
{
    [Header("Conversation")]
    [SerializeField] private string conversationId;

    [SerializeField] private string firstNodeId;

    [Header("Nodes")]
    [SerializeField] private List<DialogueNode> nodes = new();

    public string ConversationId => conversationId;
    public string FirstNodeId => firstNodeId;
    public IReadOnlyList<DialogueNode> Nodes => nodes;
}


[Serializable]
public class DialogueNode
{
    [Header("Identification")]
    [SerializeField] private string id;

    [Header("Speaker")]
    [SerializeField] private DialogueCharacter speaker;

    [Header("Dialogue")]
    [TextArea(3, 8)]
    [SerializeField] private string text;

    [Header("Portrait Override")]
    [Tooltip("Leave empty to use the character default portrait.")]
    [SerializeField] private Sprite portraitOverride;

    [Header("Flow")]
    [Tooltip("Node shown after pressing Continue.")]
    [SerializeField] private string nextNodeId;

    [Header("Choices")]
    [SerializeField] private List<DialogueChoice> choices = new();

    [Header("Events")]
    [Tooltip("Optional event triggered when this node is displayed.")]
    [SerializeField] private string eventId;

    public string Id => id;
    public DialogueCharacter Speaker => speaker;
    public string Text => text;
    public Sprite PortraitOverride => portraitOverride;
    public string NextNodeId => nextNodeId;
    public IReadOnlyList<DialogueChoice> Choices => choices;
    public string EventId => eventId;
}


[Serializable]
public class DialogueChoice
{
    [SerializeField] private string text;

    [Tooltip("Node opened after choosing this option.")]
    [SerializeField] private string nextNodeId;

    public string Text => text;
    public string NextNodeId => nextNodeId;
}