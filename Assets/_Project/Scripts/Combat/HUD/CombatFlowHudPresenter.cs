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
        private const float BannerPunchDurationSeconds = 0.32f;

        [SerializeField] private TextMeshProUGUI flowBannerLabel;
        [SerializeField] private CombatSessionHub combatSessionHub;

        private RectTransform _bannerRoot;
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
            var roundLabel = actingSide == Side.Allies ? "Player round" : "Enemy round";
            ShowBanner($"{roundLabel} begins.");
        }

        private void HandleCombatSessionClosed()
        {
            if (flowBannerLabel != null)
            {
                flowBannerLabel.text = string.Empty;
            }

            if (_bannerRoot != null)
            {
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
            _bannerRoot.localScale = Vector3.one;
            _bannerRoot.DOPunchScale(new Vector3(0.08f, 0.12f, 0f), BannerPunchDurationSeconds, 7, 0.55f)
                .SetLink(_bannerRoot.gameObject);
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
                return;
            }

            var rootObject = new GameObject(RuntimeRootObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rootObject.transform.SetParent(overlayCanvas.transform, false);
            _bannerRoot = rootObject.GetComponent<RectTransform>();
            _bannerRoot.anchorMin = new Vector2(0.5f, 1f);
            _bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            _bannerRoot.pivot = new Vector2(0.5f, 1f);
            _bannerRoot.anchoredPosition = new Vector2(0f, -18f);
            _bannerRoot.sizeDelta = new Vector2(420f, 40f);

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
            flowBannerLabel.fontSize = 22f;
            flowBannerLabel.fontStyle = FontStyles.Bold;
            flowBannerLabel.raycastTarget = false;
            flowBannerLabel.text = _lastBannerText;
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
