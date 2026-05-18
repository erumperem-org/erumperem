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
    private bool _isEntered;

    // ── DetectionReceiver overrides ───────────────────────────────────────

    protected override void OnDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!detector.gameObject.CompareTag("Player")) return;
        if (shapeLabel != "CharactersDetectionArea") return;

        base.OnDetectionEnter(detector, shapeLabel, shapeIndex);
        _isEntered = true;
        StartTransition(onEnterMaterial);
    }

    protected override void OnDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (!detector.gameObject.CompareTag("Player")) return;
        if (shapeLabel != "CharactersDetectionArea") return;

        base.OnDetectionExit(detector, shapeLabel, shapeIndex);
        ApplyExit();
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Chamado pelo PlayableCharacterStatesBuilder sempre que o Main muda.
    /// Reseta o receiver para o estado de saída caso o detector que estava
    /// dentro ainda não tenha saído fisicamente da shape.
    /// </summary>
    public void ForceExit()
    {
        if (!_isEntered) return;
        ApplyExit();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ApplyExit()
    {
        _isEntered = false;
        StartTransition(onExitMaterial);
    }

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
        Color endColor   = target.color;
        float elapsed    = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            current.color = Color.Lerp(startColor, endColor, elapsed / transitionDuration);
            yield return null;
        }

        current.color        = endColor;
        objectRenderer.material = target;
        _transitionCoroutine = null;
    }
}