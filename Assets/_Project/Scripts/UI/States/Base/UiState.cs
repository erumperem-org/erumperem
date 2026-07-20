using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public abstract class UiState : IState
{
    protected MonoBehaviour         Context      { get; }
    protected List<UiEffectSO> EnterEffects { get; }
    protected List<UiEffectSO>  ExitEffects  { get; }

    private Coroutine _enterCoroutine;

    protected UiState(
        MonoBehaviour         context,
        List<UiEffectSO> enterEffects,
        List<UiEffectSO>  exitEffects)
    {
        Context      = context;
        EnterEffects = enterEffects ?? new List<UiEffectSO>(0);
        ExitEffects  = exitEffects  ?? new List<UiEffectSO>(0);
    }

    public abstract string StateName { get; }

    public virtual void OnEnter()
    {
        _enterCoroutine = Context.StartCoroutine(RunEnterEffects());
    }

    public virtual void OnExit()
    {
        if (_enterCoroutine != null)
        {
            Context.StopCoroutine(_enterCoroutine);
            _enterCoroutine = null;
        }

        KillEnterEffectTweens();

        Context.StartCoroutine(RunExitEffects());
    }

    private void KillEnterEffectTweens()
    {
        Context.transform.DOKill();

        if (Context.TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup.DOKill();
        }

        if (Context.TryGetComponent(out Image image))
        {
            image.DOKill();
        }
    }

    private IEnumerator RunEnterEffects()
    {
        foreach (var effect in EnterEffects)
            yield return effect.Execute(Context);
    }

    private IEnumerator RunExitEffects()
    {
        foreach (var effect in ExitEffects)
            yield return effect.Execute(Context);
    }
}
