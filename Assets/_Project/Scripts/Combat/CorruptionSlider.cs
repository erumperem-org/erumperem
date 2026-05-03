using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// UI do slider de corrupção: não altera layout do <see cref="Slider.fillRect"/> (evita conflito com stretch do Slider).
    /// Anima só <see cref="Slider.value"/> + piscar de cor no fill (DOTween).
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public sealed class CorruptionSlider : MonoBehaviour
    {
        private const float CorruptionSliderMovementDurationSeconds = 1f;

        [SerializeField] private Slider corruptionSlider;

        [Tooltip("Opcional: se vazio, tenta o componente na cena.")]
        [SerializeField] private CombatSessionHub combatSessionHub;

        [Tooltip("Corrupção de mundo mapeada para o máximo do slider (valor normalizado = corrupção / isto).")]
        [SerializeField] private float maxCorruptionForSliderNormalization = 250f;

        [Header("Fill (piscar durante o movimento — só cor, sem escala/layout)")]
        [SerializeField] [Range(0f, 1f)] private float fillPeakWhitenessBlend = 0.55f;

        [Tooltip("Número de picos (claro) num ciclo de 1 s; cada pico inclui volta à cor base.")]
        [SerializeField] [Min(1)] private int fillBlinkPeakCount = 6;

        private Graphic _fillGraphic;

        private Color _fillGraphicBaselineColor;

        private Sequence _presentationSequence;
        private Tweener _sliderValueTween;

        private CorruptionManager _subscribedCorruptionManager;

        private bool _fillGraphicBaselineCaptured;
        private bool _corruptionEventsSubscribed;

        private void Reset()
        {
            corruptionSlider = GetComponent<Slider>();
        }

        private void Awake()
        {
            if (corruptionSlider == null)
            {
                corruptionSlider = GetComponent<Slider>();
            }
        }

        private void OnEnable()
        {
            EnsureFillGraphicBaselineCaptured();
            SubscribeCorruptionEventsIfNeeded();
            RefreshSliderFromCurrentCorruptionWithoutTween();
        }

        private void Start()
        {
            EnsureFillGraphicBaselineCaptured();
            SubscribeCorruptionEventsIfNeeded();
            RefreshSliderFromCurrentCorruptionWithoutTween();
        }

        private void OnDisable()
        {
            KillAllCorruptionTweens();
            UnsubscribeCorruptionEvents();
            RestoreFillGraphicColorBaseline();
        }

        private void EnsureFillGraphicBaselineCaptured()
        {
            if (_fillGraphicBaselineCaptured)
            {
                return;
            }

            CacheFillGraphicReference();
            RefreshFillGraphicBaselineFromCurrent();
            _fillGraphicBaselineCaptured = true;
        }

        private void SubscribeCorruptionEventsIfNeeded()
        {
            if (_corruptionEventsSubscribed)
            {
                return;
            }

            SubscribeCorruptionEvents();
            _corruptionEventsSubscribed = true;
        }

        private void CacheFillGraphicReference()
        {
            if (corruptionSlider == null || corruptionSlider.fillRect == null)
            {
                return;
            }

            _fillGraphic = corruptionSlider.fillRect.GetComponent<Graphic>();
        }

        private void RefreshFillGraphicBaselineFromCurrent()
        {
            if (_fillGraphic != null)
            {
                _fillGraphicBaselineColor = _fillGraphic.color;
            }
        }

        private void RestoreFillGraphicColorBaseline()
        {
            if (_fillGraphic != null)
            {
                _fillGraphic.color = _fillGraphicBaselineColor;
            }
        }

        private void SubscribeCorruptionEvents()
        {
            if (combatSessionHub == null)
            {
                combatSessionHub = FindFirstObjectByType<CombatSessionHub>();
            }

            if (combatSessionHub != null)
            {
                combatSessionHub.OnCombatSessionReadyForUi += OnCombatSessionReadyForUi;
            }

            _subscribedCorruptionManager = CorruptionManager.Instance;
            if (_subscribedCorruptionManager != null)
            {
                _subscribedCorruptionManager.OnMirrorCorruptionValueChanged += OnMirrorCorruptionValueChanged;
            }
            else if (combatSessionHub != null)
            {
                combatSessionHub.OnBattleCorruptionAdjusted += OnBattleCorruptionAdjustedFromHub;
            }
        }

        private void UnsubscribeCorruptionEvents()
        {
            if (!_corruptionEventsSubscribed)
            {
                return;
            }

            if (combatSessionHub != null)
            {
                combatSessionHub.OnCombatSessionReadyForUi -= OnCombatSessionReadyForUi;
                combatSessionHub.OnBattleCorruptionAdjusted -= OnBattleCorruptionAdjustedFromHub;
            }

            if (_subscribedCorruptionManager != null)
            {
                _subscribedCorruptionManager.OnMirrorCorruptionValueChanged -= OnMirrorCorruptionValueChanged;
                _subscribedCorruptionManager = null;
            }

            _corruptionEventsSubscribed = false;
        }

        private void OnCombatSessionReadyForUi(CombatPrototypeController combatSessionController)
        {
            if (combatSessionController?.BattleState == null)
            {
                return;
            }

            AnimateSliderToWorldCorruption(combatSessionController.BattleState.CorruptionValue);
        }

        private void OnMirrorCorruptionValueChanged(double newCorruptionValue)
        {
            AnimateSliderToWorldCorruption(newCorruptionValue);
        }

        private void OnBattleCorruptionAdjustedFromHub(double delta, double newCorruptionValue, int? previousTier, int newTier)
        {
            AnimateSliderToWorldCorruption(newCorruptionValue);
        }

        private void RefreshSliderFromCurrentCorruptionWithoutTween()
        {
            if (corruptionSlider == null)
            {
                return;
            }

            var worldCorruption = ResolveCurrentWorldCorruption();
            corruptionSlider.value = MapWorldCorruptionToSliderValue(worldCorruption);
        }

        private double ResolveCurrentWorldCorruption()
        {
            if (CorruptionManager.Instance != null)
            {
                return CorruptionManager.Instance.GetCorruptionValue();
            }

            return corruptionSlider.value * Mathf.Max(1f, maxCorruptionForSliderNormalization);
        }

        private float MapWorldCorruptionToSliderValue(double worldCorruption)
        {
            var denominator = Mathf.Max(1f, maxCorruptionForSliderNormalization);
            return Mathf.Clamp01((float)(worldCorruption / denominator));
        }

        private void AnimateSliderToWorldCorruption(double worldCorruption)
        {
            if (corruptionSlider == null)
            {
                return;
            }

            KillAllCorruptionTweens();
            RestoreFillGraphicColorBaseline();
            RefreshFillGraphicBaselineFromCurrent();

            var targetSliderValue = MapWorldCorruptionToSliderValue(worldCorruption);
            var peakBrightColor = _fillGraphic != null
                ? Color.Lerp(_fillGraphicBaselineColor, Color.white, Mathf.Clamp01(fillPeakWhitenessBlend))
                : Color.white;

            _presentationSequence = DOTween.Sequence().SetLink(gameObject);

            _sliderValueTween = DOTween.To(
                    () => corruptionSlider.value,
                    value => corruptionSlider.value = value,
                    targetSliderValue,
                    CorruptionSliderMovementDurationSeconds)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);

            _presentationSequence.Join(_sliderValueTween);

            if (_fillGraphic != null && fillBlinkPeakCount > 0)
            {
                var blinkSequence = DOTween.Sequence().SetLink(_fillGraphic.gameObject);
                var segmentDuration = CorruptionSliderMovementDurationSeconds / (fillBlinkPeakCount * 2f);
                for (var pulseIndex = 0; pulseIndex < fillBlinkPeakCount; pulseIndex++)
                {
                    blinkSequence.Append(_fillGraphic.DOColor(peakBrightColor, segmentDuration).SetEase(Ease.OutQuad));
                    blinkSequence.Append(_fillGraphic.DOColor(_fillGraphicBaselineColor, segmentDuration).SetEase(Ease.InQuad));
                }

                _presentationSequence.Join(blinkSequence);
            }

            _presentationSequence.OnKill(RestoreFillGraphicColorBaseline);
            _presentationSequence.OnComplete(RestoreFillGraphicColorBaseline);
        }

        private void KillAllCorruptionTweens()
        {
            _presentationSequence?.Kill();
            _presentationSequence = null;
            _sliderValueTween?.Kill();
            _sliderValueTween = null;

            if (_fillGraphic != null)
            {
                _fillGraphic.DOKill(false);
            }

            if (corruptionSlider != null)
            {
                corruptionSlider.DOKill(false);
            }
        }
    }
}
