using System;
using DG.Tweening;
using Game.Core.Config;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// UI do slider de corrupção: não altera layout do <see cref="Slider.fillRect"/> (evita conflito com stretch do Slider).
    /// Anima só <see cref="Slider.value"/> + piscar de cor no fill (DOTween).
    /// Presentation is 0–100; the real world value may exceed 200.
    /// The tier icon beside the bar explains the current tier, and each skull mark along the bar (<c>Lvl1</c>–<c>Lvl4</c>)
    /// explains what its tier does — both only while the mouse is over them.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    public sealed class CorruptionSlider : MonoBehaviour
    {
        private const float CorruptionSliderMovementDurationSeconds = 1f;
        private const string AlongBarDangerMarksChildName = "Lvls";
        private const string TierMarkNamePrefix = "Lvl";
        private const string RuntimeTierMarkHoverPanelObjectName = "CorruptionTierMarkHoverPanel";
        private const float TierMarkHoverPanelWidthPixels = 220f;
        private const float TooltipFontSize = 13f;
        private const string RuntimeTierIconObjectName = "CorruptionTierIcon";
        private const string RuntimeTierHoverPanelObjectName = "CorruptionTierHoverPanel";
        private const string RuntimeTierHoverTextObjectName = "CorruptionTierHoverText";
        private const float TierIconSizePixels = 36f;
        private const float TierIconGapFromBarPixels = 8f;

        [SerializeField] private Slider corruptionSlider;

        [Tooltip("Opcional: se vazio, tenta o componente na cena.")]
        [SerializeField] private CombatSessionHub combatSessionHub;

        [Tooltip("Ignored at runtime. Presentation is always 0–100 (real corruption may exceed 200).")]
        [SerializeField] private float maxCorruptionForSliderNormalization = 100f;

        [Header("Fill (piscar durante o movimento — só cor, sem escala/layout)")]
        [SerializeField][Range(0f, 1f)] private float fillPeakWhitenessBlend = 0.55f;

        [Tooltip("Número de picos (claro) num ciclo de 1 s; cada pico inclui volta à cor base.")]
        [SerializeField][Min(1)] private int fillBlinkPeakCount = 6;

        [Header("Tier icon (beside the bar, not along it)")]
        [SerializeField] private Image corruptionTierIcon;
        [SerializeField] private Sprite[] corruptionTierIconsByTier = new Sprite[5];
        [SerializeField] private TextMeshProUGUI corruptionTierHoverLabel;

        private Graphic _fillGraphic;
        private Color _fillGraphicBaselineColor;
        private Sequence _presentationSequence;
        private Tweener _sliderValueTween;
        private CorruptionManager _subscribedCorruptionManager;
        private bool _fillGraphicBaselineCaptured;
        private bool _corruptionEventsSubscribed;
        private int _presentedCorruptionTier = -1;
        private TextMeshProUGUI _tierMarkHoverLabel;
        private bool _tierMarkHoverViewsCreated;

        private static readonly Color[] FallbackTierIconColors =
        {
            new Color(0.55f, 0.62f, 0.55f, 1f),
            new Color(0.78f, 0.72f, 0.32f, 1f),
            new Color(0.86f, 0.52f, 0.22f, 1f),
            new Color(0.82f, 0.28f, 0.18f, 1f),
            new Color(0.55f, 0.12f, 0.18f, 1f),
        };

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
            if (global::InputDeviceModeManager.Instance != null)
            {
                global::InputDeviceModeManager.Instance.ModeChanged -= HandleInputModeChanged;
                global::InputDeviceModeManager.Instance.ModeChanged += HandleInputModeChanged;
            }

            EnsureTierMarkHoverViewsCreated();
            EnsureTierIconCreated();
            EnsureFillGraphicBaselineCaptured();
            SubscribeCorruptionEventsIfNeeded();
            RefreshSliderFromCurrentCorruptionWithoutTween();
            RefreshTierHoverCopy(); // GARANTE A ATUALIZAÇÃO DO TEXTO AO HABILITAR
        }

        private void Start()
        {
            EnsureTierMarkHoverViewsCreated();
            EnsureTierIconCreated();
            EnsureFillGraphicBaselineCaptured();
            SubscribeCorruptionEventsIfNeeded();
            RefreshSliderFromCurrentCorruptionWithoutTween();
            RefreshTierHoverCopy(); // GARANTE A ATUALIZAÇÃO DO TEXTO NO START
        }

        private void OnDisable()
        {
            KillAllCorruptionTweens();
            UnsubscribeCorruptionEvents();
            RestoreFillGraphicColorBaseline();
            HideTierHoverPanel();
            HideTierMarkHoverPanel();

            if (global::InputDeviceModeManager.Instance != null)
            {
                global::InputDeviceModeManager.Instance.ModeChanged -= HandleInputModeChanged;
            }
        }

        /// <summary>Em modo teclado o EventSystem é desligado e não manda PointerExit: nenhum balão pode ficar preso aberto.</summary>
        private void HandleInputModeChanged(InputControlMode mode)
        {
            if (mode == InputControlMode.Keyboard)
            {
                HideTierHoverPanel();
                HideTierMarkHoverPanel();
            }
        }

        /// <summary>Cada caveira ao longo da barra mostra, no hover, o que o seu tier faz.</summary>
        private void EnsureTierMarkHoverViewsCreated()
        {
            if (_tierMarkHoverViewsCreated)
            {
                return;
            }

            var dangerMarksRoot = FindDescendantByName(transform, AlongBarDangerMarksChildName);
            if (dangerMarksRoot == null)
            {
                return;
            }

            _tierMarkHoverViewsCreated = true;
            dangerMarksRoot.gameObject.SetActive(true);

            for (var childIndex = 0; childIndex < dangerMarksRoot.childCount; childIndex++)
            {
                var mark = dangerMarksRoot.GetChild(childIndex) as RectTransform;
                if (mark == null ||
                    !mark.name.StartsWith(TierMarkNamePrefix, StringComparison.Ordinal) ||
                    !int.TryParse(mark.name.Substring(TierMarkNamePrefix.Length), out var markTier))
                {
                    continue;
                }

                if (mark.TryGetComponent<Graphic>(out var markGraphic))
                {
                    markGraphic.raycastTarget = true;
                }

                if (!mark.TryGetComponent<CorruptionHoverRelay>(out var relay))
                {
                    relay = mark.gameObject.AddComponent<CorruptionHoverRelay>();
                }

                relay.Bind(
                    () => ShowTierMarkHoverPanel(mark, markTier),
                    HideTierMarkHoverPanel);
            }
        }

        private void ShowTierMarkHoverPanel(RectTransform mark, int markTier)
        {
            EnsureTierMarkHoverPanelCreated();
            if (_tierMarkHoverLabel == null || mark == null)
            {
                return;
            }

            _tierMarkHoverLabel.text = Erumperem.UI.PlayerFacingText.PresentForUi(BuildTierHoverMarkup(markTier));

            var hoverRoot = (RectTransform)_tierMarkHoverLabel.transform.parent;
            var markBottomWorld = mark.TransformPoint(new Vector3(mark.rect.center.x, mark.rect.yMin, 0f));
            hoverRoot.localPosition = transform.InverseTransformPoint(markBottomWorld) + new Vector3(0f, -6f, 0f);
            hoverRoot.SetAsLastSibling();
            ShowHoverRoot(hoverRoot);
        }

        private void HideTierMarkHoverPanel()
        {
            if (_tierMarkHoverLabel != null)
            {
                HideHoverRoot(_tierMarkHoverLabel.transform.parent);
            }
        }

        private void EnsureTierMarkHoverPanelCreated()
        {
            if (_tierMarkHoverLabel != null)
            {
                return;
            }

            var hoverPanelObject = new GameObject(
                RuntimeTierMarkHoverPanelObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            hoverPanelObject.transform.SetParent(transform, false);
            var hoverPanelRect = hoverPanelObject.GetComponent<RectTransform>();
            var center = new Vector2(0.5f, 0.5f);
            hoverPanelRect.anchorMin = center;
            hoverPanelRect.anchorMax = center;
            hoverPanelRect.pivot = new Vector2(0.5f, 1f);
            hoverPanelRect.sizeDelta = new Vector2(TierMarkHoverPanelWidthPixels, 0f);

            hoverPanelObject.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.92f);

            var layout = hoverPanelObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var sizeFitter = hoverPanelObject.GetComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var hoverTextObject = new GameObject("CorruptionTierMarkHoverText", typeof(RectTransform), typeof(TextMeshProUGUI));
            hoverTextObject.transform.SetParent(hoverPanelObject.transform, false);
            _tierMarkHoverLabel = hoverTextObject.GetComponent<TextMeshProUGUI>();
            _tierMarkHoverLabel.fontSize = TooltipFontSize;
            _tierMarkHoverLabel.alignment = TextAlignmentOptions.TopLeft;
            _tierMarkHoverLabel.textWrappingMode = TextWrappingModes.Normal;

            DisableRaycasts(hoverPanelObject.transform);
            hoverPanelObject.SetActive(false);
        }

        private static string BuildTierHoverMarkup(int corruptionTier) =>
            CorruptionPresentation.BuildTierHoverAuthoredMarkup(
                corruptionTier,
                CombatBalanceConfig.CreateDefault().GetTierModifiers(corruptionTier));

        private static void ShowHoverRoot(Transform hoverRoot)
        {
            hoverRoot.DOKill();
            hoverRoot.gameObject.SetActive(true);
            hoverRoot.localScale = Vector3.one;
            hoverRoot.DOPunchScale(new Vector3(0.08f, 0.1f, 0f), 0.28f, 6, 0.55f)
                .SetLink(hoverRoot.gameObject);
        }

        private static void HideHoverRoot(Transform hoverRoot)
        {
            hoverRoot.DOKill();
            hoverRoot.gameObject.SetActive(false);
            hoverRoot.localScale = Vector3.one;
        }

        /// <summary>Balões não podem receber o mouse, senão roubam o hover de quem os abriu e piscam.</summary>
        private static void DisableRaycasts(Transform root)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static Transform FindDescendantByName(Transform root, string descendantName)
        {
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var child = root.GetChild(childIndex);
                if (string.Equals(child.name, descendantName, StringComparison.Ordinal))
                {
                    return child;
                }

                var nested = FindDescendantByName(child, descendantName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private void EnsureTierIconCreated()
        {
            if (corruptionTierIcon == null)
            {
                var existingIconTransform = transform.Find(RuntimeTierIconObjectName);
                if (existingIconTransform != null)
                {
                    corruptionTierIcon = existingIconTransform.GetComponent<Image>();
                }
            }

            if (corruptionTierIcon == null)
            {
                var iconObject = new GameObject(RuntimeTierIconObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObject.transform.SetParent(transform, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(1f, 0.5f);
                iconRect.anchorMax = new Vector2(1f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(TierIconGapFromBarPixels, 0f);
                iconRect.sizeDelta = new Vector2(TierIconSizePixels, TierIconSizePixels);
                corruptionTierIcon = iconObject.GetComponent<Image>();
                corruptionTierIcon.raycastTarget = true;
            }

            if (!corruptionTierIcon.TryGetComponent<CorruptionHoverRelay>(out var hoverRelay))
            {
                hoverRelay = corruptionTierIcon.gameObject.AddComponent<CorruptionHoverRelay>();
            }

            hoverRelay.Bind(ShowTierHoverPanel, HideTierHoverPanel);

            if (corruptionTierHoverLabel == null)
            {
                var hoverPanelTransform = corruptionTierIcon.transform.Find(RuntimeTierHoverPanelObjectName);
                if (hoverPanelTransform != null)
                {
                    var hoverTextTransform = hoverPanelTransform.Find(RuntimeTierHoverTextObjectName);
                    if (hoverTextTransform != null)
                    {
                        corruptionTierHoverLabel = hoverTextTransform.GetComponent<TextMeshProUGUI>();
                    }
                }
            }

            if (corruptionTierHoverLabel == null)
            {
                CreateRuntimeHoverPanel();
            }

            // O balão do prefab vem ativo: ele só pode aparecer com o mouse em cima da caveira.
            if (corruptionTierHoverLabel != null)
            {
                corruptionTierHoverLabel.enableAutoSizing = false;
                corruptionTierHoverLabel.fontSize = TooltipFontSize;
                DisableRaycasts(corruptionTierHoverLabel.transform.parent);
                HideTierHoverPanel();
            }
        }

        private void CreateRuntimeHoverPanel()
        {
            var hoverPanelObject = new GameObject(
                RuntimeTierHoverPanelObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            hoverPanelObject.transform.SetParent(corruptionTierIcon.transform, false);
            var hoverPanelRect = hoverPanelObject.GetComponent<RectTransform>();
            hoverPanelRect.anchorMin = new Vector2(0.5f, 0f);
            hoverPanelRect.anchorMax = new Vector2(0.5f, 0f);
            hoverPanelRect.pivot = new Vector2(0.5f, 1f);
            hoverPanelRect.anchoredPosition = new Vector2(0f, -6f);
            hoverPanelRect.sizeDelta = new Vector2(280f, 120f);

            var hoverPanelImage = hoverPanelObject.GetComponent<Image>();
            hoverPanelImage.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
            hoverPanelImage.raycastTarget = false;

            var hoverTextObject = new GameObject(RuntimeTierHoverTextObjectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            hoverTextObject.transform.SetParent(hoverPanelObject.transform, false);
            var hoverTextRect = hoverTextObject.GetComponent<RectTransform>();
            hoverTextRect.anchorMin = Vector2.zero;
            hoverTextRect.anchorMax = Vector2.one;
            hoverTextRect.offsetMin = new Vector2(10f, 8f);
            hoverTextRect.offsetMax = new Vector2(-10f, -8f);

            corruptionTierHoverLabel = hoverTextObject.GetComponent<TextMeshProUGUI>();
            corruptionTierHoverLabel.fontSize = 16f;
            corruptionTierHoverLabel.alignment = TextAlignmentOptions.TopLeft;
            corruptionTierHoverLabel.enableWordWrapping = true;
            corruptionTierHoverLabel.raycastTarget = false;

            HideTierHoverPanel();
        }

        internal void ShowTierHoverPanel()
        {
            if (corruptionTierHoverLabel == null)
            {
                return;
            }

            RefreshTierHoverCopy();
            ShowHoverRoot(corruptionTierHoverLabel.transform.parent);
        }

        internal void HideTierHoverPanel()
        {
            if (corruptionTierHoverLabel == null)
            {
                return;
            }

            HideHoverRoot(corruptionTierHoverLabel.transform.parent);
        }

        public void RefreshTierHoverCopy()
        {
            if (corruptionTierHoverLabel == null)
            {
                return;
            }

            var corruptionTier = CorruptionTierCalculator.GetTier(ResolveCurrentWorldCorruption());
            corruptionTierHoverLabel.text = Erumperem.UI.PlayerFacingText.PresentForUi(BuildTierHoverMarkup(corruptionTier));
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
                _subscribedCorruptionManager.OnCorruptionTierChanged += OnCorruptionTierChanged;
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
                _subscribedCorruptionManager.OnCorruptionTierChanged -= OnCorruptionTierChanged;
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

        private void OnCorruptionTierChanged(int previousTier, int newTier)
        {
            RefreshTierIcon(newTier);
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
            RefreshTierIcon(CorruptionTierCalculator.GetTier(worldCorruption));
        }

        private double ResolveCurrentWorldCorruption()
        {
            if (CorruptionManager.Instance != null)
            {
                return CorruptionManager.Instance.GetCorruptionValue();
            }

            return 0d;
        }

        private static float MapWorldCorruptionToSliderValue(double worldCorruption) =>
            CorruptionPresentation.ToPresentedSliderNormalizedValue(worldCorruption);

        private void RefreshTierIcon(int corruptionTier)
        {
            EnsureTierIconCreated();
            if (corruptionTierIcon == null)
            {
                return;
            }

            _presentedCorruptionTier = corruptionTier;
            var clampedTier = Mathf.Clamp(corruptionTier, 0, 4);
            Sprite tierSprite = null;
            if (corruptionTierIconsByTier != null &&
                clampedTier < corruptionTierIconsByTier.Length)
            {
                tierSprite = corruptionTierIconsByTier[clampedTier];
            }

            if (tierSprite != null)
            {
                corruptionTierIcon.sprite = tierSprite;
                corruptionTierIcon.color = Color.white;
            }
            else
            {
                corruptionTierIcon.sprite = null;
                corruptionTierIcon.color = FallbackTierIconColors[clampedTier];
            }

            // Atualiza sempre o texto do label para o tier correto
            RefreshTierHoverCopy();
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
            RefreshTierIcon(CorruptionTierCalculator.GetTier(worldCorruption));

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

        private sealed class CorruptionHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private Action _onEnter;
            private Action _onExit;

            public void Bind(Action onEnter, Action onExit)
            {
                _onEnter = onEnter;
                _onExit = onExit;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _onEnter?.Invoke();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _onExit?.Invoke();
            }
        }
    }
}