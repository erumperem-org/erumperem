using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Events")]
    [SerializeField]
    private UnityEvent<string> onDialogueEvent;

    private DialogueConversation currentConversation;
    private DialogueNode currentNode;

    private readonly Dictionary<string, DialogueNode>
        nodeLookup = new();

    public bool IsDialogueActive { get; private set; }

    public event Action DialogueStarted;
    public event Action DialogueEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void StartDialogue(
        DialogueConversation conversation)
    {
        if (conversation == null)
        {
            Debug.LogWarning("Attempted to start a null conversation.");

            return;
        }

        currentConversation = conversation;

        BuildNodeLookup();

        if (nodeLookup.Count == 0)
        {
            Debug.LogWarning($"Conversation {conversation.name} has no nodes.");

            return;
        }

        IsDialogueActive = true;

        DialogueStarted?.Invoke();

        string firstNode = currentConversation.FirstNodeId;

        if (string.IsNullOrWhiteSpace(firstNode))
        {
            firstNode = currentConversation.Nodes[0].Id;
        }

        GoToNode(firstNode);
    }

    private void BuildNodeLookup()
    {
        nodeLookup.Clear();

        foreach (DialogueNode node in currentConversation.Nodes)
        {
            if (node == null)
                continue;

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                Debug.LogWarning($"Dialogue node without ID in " + $"{currentConversation.name}.");

                continue;
            }

            if (nodeLookup.ContainsKey(node.Id))
            {
                Debug.LogError($"Duplicate dialogue node ID: " + $"{node.Id}");

                continue;
            }

            nodeLookup.Add(node.Id,node);
        }
    }

    private void GoToNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            EndDialogue();
            return;
        }

        if (!nodeLookup.TryGetValue(nodeId,out DialogueNode node))
        {
            Debug.LogError($"Dialogue node '{nodeId}' " +$"was not found in " +$"{currentConversation.name}.");

            EndDialogue();

            return;
        }

        currentNode = node;

        if (!string.IsNullOrWhiteSpace(node.EventId))
        {
            onDialogueEvent?.Invoke(node.EventId);
        }

        dialogueUI.ShowNode(currentNode,ContinueDialogue,SelectChoice);
    }

    public void ContinueDialogue()
    {
        if (!IsDialogueActive){return;}

        if (currentNode == null){return;}

        if (currentNode.Choices.Count > 0){return;}

        GoToNode(currentNode.NextNodeId);
    }

    private void SelectChoice(DialogueChoice choice)
    {
        if (!IsDialogueActive){return;}

        if (choice == null){return;}

        GoToNode(choice.NextNodeId);
    }

    public void EndDialogue()
    {
        if (!IsDialogueActive){return;}

        IsDialogueActive = false;

        currentNode = null;
        currentConversation = null;

        nodeLookup.Clear();

        dialogueUI.Hide();

        DialogueEnded?.Invoke();
    }
}