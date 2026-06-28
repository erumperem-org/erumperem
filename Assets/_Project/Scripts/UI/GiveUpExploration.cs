using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;
using System.Collections;

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
        foreach(var character in manager.Playables)
        {
            character.transform.position = character.RestingPoint.transform.position;
        }

    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        if (!isDisabled)
            _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));
    }

    private IEnumerator HandleGiveUpAsync()
    {
        _isProcessing = true;

        // 1. Deleta o save do inventário (síncrono)
        playerInventorySaveSystem.DeletesSave();

        // 2. Aguarda o reset dos snapshots para os RestingPoints
        var task = loadContext.MoveSnapshotsToCharacterRestingPoints();
        yield return new WaitUntil(() => task.IsCompleted);

        // 3. Propaga exceção se houver falha
        if (task.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha ao mover snapshots: {task.Exception}");
            _isProcessing = false;
            yield break;
        }

        // 4. Só navega DEPOIS do save estar completo
        if (string.IsNullOrWhiteSpace(uiButtonModel.sceneName))
        {
            Debug.LogError("[ChangeSceneButtonController] sceneName não configurado no botão.", this);
            _isProcessing = false;
            yield break;
        }

        if (ScenesManager.Instance == null)
        {
            Debug.LogError("[ChangeSceneButtonController] ScenesManager.Instance não encontrado.", this);
            _isProcessing = false;
            yield break;
        }

        if (CombatExplorationBridge.Instance != null
            && CombatExplorationBridge.Instance.TryCompleteReturnToExploration(uiButtonModel.sceneName))
        {
            yield break;
        }

        ScenesManager.Instance.LoadSceneByName(uiButtonModel.sceneName);
    }
}