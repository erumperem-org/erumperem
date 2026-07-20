using UnityEngine;
using UnityEngine.EventSystems;
using Core.StateMachine.FiniteStateMachine;

public abstract class UiButtonController<T> : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    where T : UiButtonModel
{
    [SerializeField] protected T uiButtonModel;
    [SerializeField] protected UiButtonView uiButtonView;
    [SerializeField] protected bool isDisabled;
    protected MooreFiniteStateMachine<UiState> _fsm = new MooreFiniteStateMachine<UiState>();

    private void Start() => _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));

    public void SetDisabled(bool disabled)
    {
        isDisabled = disabled;
        _fsm.TransitionTo(isDisabled ? new ButtonDisabled(this, uiButtonView._disabledEnterEffects, uiButtonView._disabledExitEffects) : new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));
    }

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        if (!ShouldHandlePointerEnter(eventData))
        {
            return;
        }

        TransitionToHoverState();
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        if (!ShouldHandlePointerExit(eventData))
        {
            return;
        }

        TransitionToDefaultState();
    }

    void IPointerDownHandler.OnPointerDown(PointerEventData eventData)
    {
        if (!ShouldHandlePointerDown(eventData))
        {
            return;
        }

        TransitionToPressedState();
        OnPointerDownHandled(eventData);
    }

    protected virtual bool ShouldHandlePointerEnter(PointerEventData eventData) => !isDisabled;

    protected virtual bool ShouldHandlePointerExit(PointerEventData eventData) => !isDisabled;

    protected virtual bool ShouldHandlePointerDown(PointerEventData eventData) => !isDisabled;

    protected void TransitionToHoverState() =>
        _fsm.TransitionTo(new ButtonHover(this, uiButtonView._hoverEnterEffects, uiButtonView._hoverExitEffects));

    protected void TransitionToDefaultState() =>
        _fsm.TransitionTo(new ButtonDefault(this, uiButtonView._defaultEnterEffects, uiButtonView._defaultExitEffects));

    protected void TransitionToPressedState() =>
        _fsm.TransitionTo(new ButtonPressed(this, uiButtonView._pressedEnterEffects, uiButtonView._pressedExitEffects));

    protected virtual void OnPointerDownHandled(PointerEventData eventData) { }
}
