using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Shake")]
public class ShakeEffectSO : UiEffectSO
{
    public float duration = 0.3f;
    public float strength = 10f;
    public int vibrato = 20;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        KillTransformTweens(context);

        yield return LinkTweenToContext(context, context.transform
            .DOShakePosition(duration, Vector3.one * strength, vibrato))
            .WaitForCompletion();
    }
}
