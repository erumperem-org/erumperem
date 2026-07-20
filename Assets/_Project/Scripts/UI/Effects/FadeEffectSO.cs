using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Fade")]
public class FadeEffectSO : UiEffectSO
{
    public float targetAlpha = 1f;
    public float duration = 0.2f;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        var canvasGroup = context.GetComponent<CanvasGroup>();
        if (canvasGroup == null) yield break;

        canvasGroup.DOKill();

        yield return LinkTweenToContext(context, canvasGroup
            .DOFade(targetAlpha, duration))
            .WaitForCompletion();
    }
}
