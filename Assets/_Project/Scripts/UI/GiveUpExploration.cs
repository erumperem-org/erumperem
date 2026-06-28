using UnityEngine;
using UnityEngine.EventSystems;
using Services.DebugUtilities;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
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

        // ── PASSO 1: apaga inventário do disco e limpa memória ────────────────
        var deleteTask = playerInventorySaveSystem.DeletesSaveAsync();
        yield return new WaitUntil(() => deleteTask.IsCompleted);

        if (deleteTask.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha ao deletar inventário: {deleteTask.Exception}");
            _isProcessing = false;
            yield break;
        }

        // ── PASSO 3: carrega inventário (vazio, pois foi deletado) ────────────
        var loadTask = playerInventorySaveSystem.LoadAsync();
        yield return new WaitUntil(() => loadTask.IsCompleted);

        if (loadTask.IsFaulted)
        {
            Debug.LogError($"[GiveUpExploration] Falha no LoadAsync: {loadTask.Exception}");
            _isProcessing = false;
            yield break;
        }

        foreach(var character in manager.Playables)
        {
            character.transform.position = character.RestingPoint.transform.position;
        }

        FindAnyObjectByType<ExplorationLoadContext>().SaveState();
        _isProcessing = false;
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        if (!isDisabled)
            _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));
    }
}