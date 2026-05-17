using System.Collections;
using DetectionSystem.Core;
using UnityEngine;

public class PlayableDetectionReceiver : DetectionReceiver
{
    [SerializeField] private Material onExitMaterial;
    [SerializeField] private Material onEnterMaterial;
    [SerializeField] private Renderer objectRenderer;
    [SerializeField] private float transitionDuration = 0.3f;

    private Coroutine _transitionCoroutine;

    protected override void OnDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!detector.gameObject.CompareTag("Player")) return;
        base.OnDetectionEnter(detector, shapeLabel, shapeIndex);
        StartTransition(onEnterMaterial);
    }

    protected override void OnDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!detector.gameObject.CompareTag("Player")) return;
        base.OnDetectionExit(detector, shapeLabel, shapeIndex);
        StartTransition(onExitMaterial);
    }

    // ── Transição de material ─────────────────────────────────────────────

    private void StartTransition(Material target)
    {
        if (objectRenderer == null || target == null) return;

        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);

        _transitionCoroutine = StartCoroutine(TransitionRoutine(target));
    }

    private IEnumerator TransitionRoutine(Material target)
    {
        Material current = objectRenderer.material;
        Color startColor = current.color;
        Color endColor = target.color;
        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            current.color = Color.Lerp(startColor, endColor, elapsed / transitionDuration);
            yield return null;
        }

        current.color = endColor;
        objectRenderer.material = target;
    }
}
