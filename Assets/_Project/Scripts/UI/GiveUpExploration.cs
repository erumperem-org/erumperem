using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class GiveUpExploration : UiButtonController<ChangeSceneButtonModel>
{
    public PlayerInventorySaveSystem playerInventorySaveSystem;
    public ExplorationLoadContext loadContext;
    public PlayableCharactersManager manager;

    private bool _isProcessing = false;

    protected override bool ShouldHandlePointerDown(PointerEventData eventData) => !isDisabled && !_isProcessing;

    protected override void OnPointerDownHandled(PointerEventData eventData)
    {
        StartCoroutine(HandleGiveUpAsync());
    }

    private IEnumerator HandleGiveUpAsync()
    {
        _isProcessing = true;

        var deleteTask = playerInventorySaveSystem.DeletesSaveAsync();
        yield return new WaitUntil(() => deleteTask.IsCompleted);

        if (deleteTask.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha ao deletar inventário: {deleteTask.Exception}");
            _isProcessing = false;
            yield break;
        }

        var loadTask = playerInventorySaveSystem.LoadAsync();
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (loadTask.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha no LoadAsync: {loadTask.Exception}");
            _isProcessing = false;
            yield break;
        }

        foreach (var character in manager.Playables)
        {
            character.transform.position = character.RestingPoint.transform.position;
        }

        FindAnyObjectByType<ExplorationLoadContext>().SaveState();
        _isProcessing = false;
    }
}
