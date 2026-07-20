using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Move")]
public class MoveEffectSO : UiEffectSO
{
    public Vector3 offset = Vector3.zero;
    public float duration = 0.15f;
    public Ease ease = Ease.OutSine;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        KillTransformTweens(context);

        yield return LinkTweenToContext(context, context.transform
            .DOBlendableLocalMoveBy(offset, duration)
            .SetEase(ease))
            .WaitForCompletion();
    }
}
