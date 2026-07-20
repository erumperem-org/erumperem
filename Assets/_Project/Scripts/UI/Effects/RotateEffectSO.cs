using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Rotate")]
public class RotateEffectSO : UiEffectSO
{
    public Vector3 targetRotation = Vector3.zero;
    public float duration = 0.15f;
    public Ease ease = Ease.OutSine;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        KillTransformTweens(context);

        yield return LinkTweenToContext(context, context.transform
            .DORotate(targetRotation, duration)
            .SetEase(ease))
            .WaitForCompletion();
    }
}
