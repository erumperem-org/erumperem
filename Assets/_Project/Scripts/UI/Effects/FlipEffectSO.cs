using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Flip")]
public class FlipEffectSO : UiEffectSO
{
    public float duration = 0.2f;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        KillTransformTweens(context);

        yield return LinkTweenToContext(context, context.transform
            .DORotate(new Vector3(0f, 90f, 0f), duration * 0.5f))
            .WaitForCompletion();

        yield return LinkTweenToContext(context, context.transform
            .DORotate(Vector3.zero, duration * 0.5f))
            .WaitForCompletion();
    }
}
