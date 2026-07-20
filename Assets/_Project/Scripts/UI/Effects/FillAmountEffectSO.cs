using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/FillAmount")]
public class FillAmountEffectSO : UiEffectSO
{
    public float targetFill = 1f;
    public float duration = 0.2f;
    public Ease ease = Ease.OutSine;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        var image = context.GetComponent<Image>();
        if (image == null) yield break;

        image.DOKill();

        yield return LinkTweenToContext(context, image
            .DOFillAmount(targetFill, duration)
            .SetEase(ease))
            .WaitForCompletion();
    }
}
