using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Scale")]
public class ScaleEffectSO : UiEffectSO
{
    public float targetScale = 1f;
    public float duration = 0.15f;
    public Ease ease = Ease.OutSine;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        KillTransformTweens(context);

        yield return LinkTweenToContext(context, context.transform
            .DOScale(Vector3.one * targetScale, duration)
            .SetEase(ease))
            .WaitForCompletion();
    }
}
