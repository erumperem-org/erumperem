using DG.Tweening;
using Game.Core.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// Shows who won initiative at battle start, then when the player round vs enemy round begins.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatFlowHudPresenter : MonoBehaviour
    {
        private const string RuntimeRootObjectName = "CombatFlowHudRoot";
        private const float BannerEntranceDurationSeconds = 0.32f;

        [SerializeField] private TextMeshProUGUI flowBannerLabel;
        [SerializeField] private CombatSessionHub combatSessionHub;
        [SerializeField, Min(0f)] private float _bannerDisplayDurationSeconds = 1.2f;
        [SerializeField, Min(0f)] private float _bannerFadeDurationSeconds = 0.25f;
        [SerializeField] private Color _bannerAccentColor = new Color(0.65f, 0.25f, 0.23f, 0.7f);

        private RectTransform _bannerRoot;
        private CanvasGroup _bannerCanvasGroup;
        private Image _bannerAccent;
        private string _lastBannerText = string.Empty;

        private void OnEnable()
        {
            ResolveSessionHub();
            EnsureBannerCreated();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            HandleCombatSessionClosed();
        }

        private void ResolveSessionHub()
        {
            if (combatSessionHub == null)
            {
                combatSessionHub = FindFirstObjectByType<CombatSessionHub>();
            }
        }

        private void Subscribe()
        {
            if (combatSessionHub == null)
            {
                return;
            }

            combatSessionHub.OnBattleInitiativeResolved -= HandleInitiativeResolved;
            combatSessionHub.OnCombatRoundSideBegan -= HandleRoundSideBegan;
            combatSessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
            combatSessionHub.OnBattleInitiativeResolved += HandleInitiativeResolved;
            combatSessionHub.OnCombatRoundSideBegan += HandleRoundSideBegan;
            combatSessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
        }

        private void Unsubscribe()
        {
            if (combatSessionHub == null)
            {
                return;
            }

            combatSessionHub.OnBattleInitiativeResolved -= HandleInitiativeResolved;
            combatSessionHub.OnCombatRoundSideBegan -= HandleRoundSideBegan;
            combatSessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
        }

        private void HandleInitiativeResolved(Side firstActingSide)
        {
            var starterLabel = firstActingSide == Side.Allies ? "Your party" : "Enemies";
            ShowBanner($"{starterLabel} start the battle.");
        }

        private void HandleRoundSideBegan(Side actingSide)
        {
            var roundLabel = actingSide == Side.Allies ? "Your Turn" : "Enemies' Turn";
            ShowBanner(roundLabel);
            if (_bannerRoot != null && _bannerRoot.gameObject.activeInHierarchy && AudioManager.instance != null)
            {
                AudioManager.instance.PlaySFX("CombatRoundBanner");
            }
        }

        private void HandleCombatSessionClosed()
        {
            if (flowBannerLabel != null)
            {
                flowBannerLabel.text = string.Empty;
            }

            if (_bannerRoot != null)
            {
                _bannerRoot.DOKill();
                _bannerRoot.gameObject.SetActive(false);
            }
        }

        private void ShowBanner(string bannerText)
        {
            EnsureBannerCreated();
            if (flowBannerLabel == null)
            {
                return;
            }

            _lastBannerText = bannerText;
            flowBannerLabel.text = bannerText;
            _bannerRoot.gameObject.SetActive(true);
            _bannerRoot.DOKill();
            _bannerCanvasGroup.DOKill();
            _bannerRoot.localScale = Vector3.one * 0.78f;
            _bannerCanvasGroup.alpha = 0f;
            _bannerAccent.color = _bannerAccentColor;
            _bannerAccent.rectTransform.localScale = new Vector3(0.15f, 1f, 1f);
            var fadeDuration = Mathf.Max(0f, _bannerFadeDurationSeconds);
            DOTween.Sequence()
                .SetTarget(_bannerRoot)
                .SetLink(_bannerRoot.gameObject, LinkBehaviour.KillOnDisable)
                .Append(_bannerCanvasGroup.DOFade(1f, fadeDuration))
                .Join(_bannerRoot.DOScale(1.04f, BannerEntranceDurationSeconds * 0.55f).SetEase(Ease.OutCubic))
                .Append(_bannerRoot.DOScale(1f, BannerEntranceDurationSeconds * 0.45f).SetEase(Ease.OutSine))
                .Insert(0f, _bannerAccent.rectTransform.DOScaleX(1f, 0.42f).SetEase(Ease.OutCubic))
                .AppendInterval(Mathf.Max(0f, _bannerDisplayDurationSeconds))
                .Append(_bannerCanvasGroup.DOFade(0f, fadeDuration))
                .OnComplete(() => _bannerRoot.gameObject.SetActive(false));
        }

        private void EnsureBannerCreated()
        {
            if (flowBannerLabel != null && _bannerRoot != null)
            {
                return;
            }

            var overlayCanvas = ResolveOverlayCanvas();
            if (overlayCanvas == null)
            {
                return;
            }

            var existingRoot = overlayCanvas.transform.Find(RuntimeRootObjectName);
            if (existingRoot != null)
            {
                _bannerRoot = existingRoot as RectTransform;
                flowBannerLabel = existingRoot.GetComponentInChildren<TextMeshProUGUI>(true);
                ConfigureBanner();
                return;
            }

            var rootObject = new GameObject(RuntimeRootObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootObject.transform.SetParent(overlayCanvas.transform, false);
            _bannerRoot = rootObject.GetComponent<RectTransform>();

            var background = rootObject.GetComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.08f, 0.72f);
            background.raycastTarget = false;

            var labelObject = new GameObject("FlowBannerLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rootObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 2f);
            labelRect.offsetMax = new Vector2(-8f, -2f);

            flowBannerLabel = labelObject.GetComponent<TextMeshProUGUI>();
            flowBannerLabel.alignment = TextAlignmentOptions.Center;
            flowBannerLabel.fontStyle = FontStyles.Bold;
            flowBannerLabel.raycastTarget = false;
            flowBannerLabel.text = _lastBannerText;
            ConfigureBanner();
        }

        private void ConfigureBanner()
        {
            _bannerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _bannerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _bannerRoot.pivot = new Vector2(0.5f, 0.5f);
            _bannerRoot.anchoredPosition = new Vector2(0f, 40f);
            _bannerRoot.sizeDelta = new Vector2(520f, 64f);
            _bannerCanvasGroup = _bannerRoot.GetComponent<CanvasGroup>();
            if (_bannerCanvasGroup == null)
            {
                _bannerCanvasGroup = _bannerRoot.gameObject.AddComponent<CanvasGroup>();
            }

            _bannerCanvasGroup.interactable = false;
            _bannerCanvasGroup.blocksRaycasts = false;
            _bannerCanvasGroup.alpha = 0f;
            flowBannerLabel.fontSize = 32f;
            var existingAccent = _bannerRoot.Find("RoundBannerAccent");
            if (existingAccent == null)
            {
                var accentObject = new GameObject("RoundBannerAccent", typeof(RectTransform), typeof(Image));
                accentObject.transform.SetParent(_bannerRoot, false);
                _bannerAccent = accentObject.GetComponent<Image>();
            }
            else
            {
                _bannerAccent = existingAccent.GetComponent<Image>();
            }

            var accentRect = _bannerAccent.rectTransform;
            accentRect.anchorMin = accentRect.anchorMax = accentRect.pivot = new Vector2(0.5f, 0.5f);
            accentRect.anchoredPosition = new Vector2(0f, -24f);
            accentRect.sizeDelta = new Vector2(460f, 2f);
            _bannerAccent.raycastTarget = false;
            _bannerAccent.color = Color.clear;
            _bannerRoot.gameObject.SetActive(false);
        }

        private static Canvas ResolveOverlayCanvas()
        {
            var corruptionSlider = FindFirstObjectByType<CorruptionSlider>();
            if (corruptionSlider != null)
            {
                var sliderCanvas = corruptionSlider.GetComponentInParent<Canvas>();
                if (sliderCanvas != null)
                {
                    return sliderCanvas;
                }
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.isActiveAndEnabled)
                {
                    return canvas;
                }
            }

            return canvases.Length > 0 ? canvases[0] : null;
        }
    }
}
