using System.Collections;
using DetectionSystem.Core;
using UnityEngine;

/// <summary>
/// Receiver de detecção que anima a transição de material de um interactable
/// quando o jogador entra ou sai da área de detecção.
///
/// MUDANÇAS:
///   - <c>interactable</c> era <c>public</c>; agora é privado e preenchido
///     via <see cref="SetInteractable"/> — chamado pela própria base <see cref="Interactable.Awake"/>.
///   - Nenhum sistema externo precisa mais escrever nesse campo.
/// </summary>
public sealed class InteractableDetectionReceiver : DetectionReceiver
{
    [SerializeField] private Material _onExitMaterial;
    [SerializeField] private Material _onEnterMaterial;
    [SerializeField] private Renderer _objectRenderer;
    [SerializeField] private float    _transitionDuration = 0.3f;

    private Interactable _interactable;
    private Coroutine    _transitionCoroutine;

    // ── API interna (chamada por Interactable.Awake) ───────────────────────

    internal void SetInteractable(Interactable interactable) => _interactable = interactable;

    // ── DetectionReceiver overrides ───────────────────────────────────────

    protected override void OnDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
    {
        if (_interactable != null && !_interactable.CanInteract) return;
        base.OnDetectionEnter(detector, shapeLabel, shapeIndex);
        StartTransition(_onEnterMaterial);
    }

    protected override void OnDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
    {
        base.OnDetectionExit(detector, shapeLabel, shapeIndex);
        StartTransition(_onExitMaterial);
    }

    // ── Transição de material ─────────────────────────────────────────────

    private void StartTransition(Material target)
    {
        if (_objectRenderer == null || target == null) return;
        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(TransitionRoutine(target));
    }

    private IEnumerator TransitionRoutine(Material target)
    {
        Material current  = _objectRenderer.material;
        Color    start    = current.color;
        Color    end      = target.color;
        float    elapsed  = 0f;

        while (elapsed < _transitionDuration)
        {
            elapsed      += Time.deltaTime;
            current.color = Color.Lerp(start, end, elapsed / _transitionDuration);
            yield return null;
        }

        current.color            = end;
        _objectRenderer.material = target;
        _transitionCoroutine     = null;
    }
}
