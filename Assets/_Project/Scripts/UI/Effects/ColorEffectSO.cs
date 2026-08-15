using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[CreateAssetMenu(menuName = "UI/Effects/Color")]
public class ColorEffectSO : UiEffectSO
{
    public Color targetColor = Color.white;
    public float duration = 0.1f;

    public override IEnumerator Execute(MonoBehaviour context)
    {
        var image = context.GetComponent<Image>();
        if (image == null) yield break;

        image.DOKill();

        yield return LinkTweenToContext(context, image
            .DOColor(targetColor, duration))
            .WaitForCompletion();
    }
}
