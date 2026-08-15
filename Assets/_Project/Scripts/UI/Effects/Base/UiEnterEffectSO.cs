using System.Collections;
using UnityEngine;
using DG.Tweening;

public abstract class UiEffectSO : ScriptableObject
{
    public abstract IEnumerator Execute(MonoBehaviour context);

    protected static void KillTransformTweens(MonoBehaviour context)
    {
        context.transform.DOKill();
    }

    protected static Tween LinkTweenToContext(MonoBehaviour context, Tween tween)
    {
        return tween.SetLink(context.gameObject);
    }
}
