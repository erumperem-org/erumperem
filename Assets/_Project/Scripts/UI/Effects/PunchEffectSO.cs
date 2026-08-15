using System.Collections;
using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Punch")]
public class PunchEffectSO : UiEffectSO
{
    public Vector3 punch = new Vector3(0.2f, 0.2f, 0f);
    public float duration = 0.3f;
    public int vibrato = 10;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        KillTransformTweens(context);

        yield return LinkTweenToContext(context, context.transform
            .DOPunchScale(punch, duration, vibrato))
            .WaitForCompletion();
    }
}
