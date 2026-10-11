using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class GiveUpExploration : UiButtonController<ChangeSceneButtonModel>
{
    public PlayerInventorySaveSystem playerInventorySaveSystem;
    public ExplorationLoadContext loadContext;
    public PlayableCharactersManager manager;
    [SerializeField, Min(0f)] private float _transitionDurationSeconds = 2f;

    private bool _isProcessing = false;

    protected override bool ShouldHandlePointerDown(PointerEventData eventData) =>
        !isDisabled && !_isProcessing && !SceneTransitionHandler.IsTransitioning;

    protected override void OnPointerDownHandled(PointerEventData eventData)
    {
        if (manager == null) manager = FindFirstObjectByType<PlayableCharactersManager>();
        if (loadContext == null) loadContext = ExplorationLoadContext.Instance;
        if (playerInventorySaveSystem == null)
            playerInventorySaveSystem = FindFirstObjectByType<PlayerInventorySaveSystem>();
        if (manager == null || loadContext == null || playerInventorySaveSystem == null
            || manager.Main is not PlayableCharacter main || main.RestingPoint == null)
        {
            Debug.LogError("[GiveUpExploration] Referências de retorno à vila incompletas.", this);
            return;
        }
        _isProcessing = true;
        if (!SceneTransitionHandler.TryRunCoveredTransition(HandleGiveUpAsync(), _transitionDurationSeconds))
            _isProcessing = false;
    }

    private IEnumerator HandleGiveUpAsync()
    {
        try
        {
            var deleteTask = playerInventorySaveSystem.DeletesSaveAsync();
            yield return new WaitUntil(() => deleteTask.IsCompleted);

            if (deleteTask.IsFaulted || deleteTask.IsCanceled)
            {
                Debug.LogError($"[GiveUpExploration] Falha ao deletar inventário: {deleteTask.Exception}");
                yield break;
            }

            var loadTask = playerInventorySaveSystem.LoadAsync();
            yield return new WaitUntil(() => loadTask.IsCompleted);

            if (loadTask.IsFaulted || loadTask.IsCanceled)
            {
                Debug.LogError($"[GiveUpExploration] Falha no LoadAsync: {loadTask.Exception}");
                yield break;
            }

            ReturnPartyToVillage();
            loadContext.SaveState();
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void ReturnPartyToVillage()
    {
        var main = (PlayableCharacter)manager.Main;
        var companion = manager.Companion as PlayableCharacter;
        Vector3 mainPosition = main.RestingPoint.position;
        foreach (var character in manager.Playables)
        {
            if (character == null || character.RestingPoint == null) continue;
            character.MovementController.DisableMovement();
            Vector3 position = character == companion
                ? mainPosition + main.RestingPoint.right * 1.5f
                : character.RestingPoint.position;
            Quaternion rotation = character.RestingPoint.rotation;
            character.transform.SetPositionAndRotation(position, rotation);
            if (character.TryGetComponent<Rigidbody>(out var body))
            {
                body.position = position;
                body.rotation = rotation;
            }
        }

        manager.SetStateForLoad(PlayableCharacterState.Main, main, mainPosition);
        foreach (var character in manager.Playables)
        {
            if (character == null || character == main || character.RestingPoint == null) continue;
            manager.SetStateForLoad(character.CurrentState, character, character.transform.position);
        }
        Physics.SyncTransforms();
        CombatExplorationBridge.Instance?.NotifyPlayerLeftCombatEntryZone();
        CombatExplorationBridge.Instance?.BlockExplorationCombatContactsAfterSceneLoad();
    }
}
