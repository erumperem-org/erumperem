using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;
using System.Collections;
using System.Threading.Tasks;

public class GiveUpExploration : UiButtonController<ChangeSceneButtonModel>,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    public PlayerInventorySaveSystem playerInventorySaveSystem;
    public ExplorationLoadContext loadContext;
    public PlayableCharactersManager manager;

    private bool _isProcessing = false; // evita duplo clique durante await

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        if (!isDisabled)
            _fsm.TransitionTo(new ButtonHover(this, uiButtonView._hoverEnterEffects, uiButtonView._hoverExitEffects));
    }

    void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
    {
        if (isDisabled || _isProcessing) return;

        _fsm.TransitionTo(new ButtonPressed(this, uiButtonView._pressedEnterEffects, uiButtonView._pressedExitEffects));
        StartCoroutine(HandleGiveUpAsync());
    }

    private IEnumerator HandleGiveUpAsync()
    {
        _isProcessing = true;

        // 1. Reposiciona personagens nos RestingPoints imediatamente na cena
        foreach (var character in manager.Playables)
        {
            if (character == null || character.RestingPoint == null)
            {
                Debug.LogWarning($"[GiveUpExploration] '{character?.CharacterName}' sem RestingPoint — ignorado.");
                continue;
            }

            character.transform.SetPositionAndRotation(
                character.RestingPoint.position,
                character.RestingPoint.rotation);
        }

        // 2. Deleta o save do inventário
        playerInventorySaveSystem.DeletesSave();

        // 3. Aguarda o LoadAsync do inventário
        var loadTask = playerInventorySaveSystem.LoadAsync();
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (loadTask.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha no LoadAsync: {loadTask.Exception}");
            _isProcessing = false;
            yield break;
        }

        // 4. Salva o estado de exploração com as novas posições
        //    SaveState() é async void internamente — aguardamos via MoveSnapshotsToCharacterRestingPoints
        //    que já persiste em disco de forma awaitable
        var moveTask = loadContext.MoveSnapshotsToCharacterRestingPoints();
        yield return new WaitUntil(() => moveTask.IsCompleted);

        if (moveTask.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha ao mover snapshots: {moveTask.Exception}");
            _isProcessing = false;
            yield break;
        }

        _isProcessing = false;
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        if (!isDisabled)
            _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));
    }
}