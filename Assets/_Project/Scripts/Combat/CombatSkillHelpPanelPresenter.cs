using System.Collections;
using DG.Tweening;
using Erumperem.UI;
using TMPro;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Mostra texto da hotbar no painel; esconde/desce durante apresentação de ação; volta quando evento de fim chega.
    /// </summary>
    public sealed class CombatSkillHelpPanelPresenter : MonoBehaviour
    {
        [SerializeField] private CombatPresentationHub hub;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private TextMeshProUGUI helpBodyText;

        [Header("Esconder durante ação")]
        [Tooltip("Soma a anchoredPosition Y (ex.: -160 = painel desce).")]
        [SerializeField] private float hideAnchoredPositionDeltaY = -180f;
        [SerializeField] private float hideTweenDurationSeconds = 0.22f;
        [SerializeField] private Ease hideEase = Ease.InQuad;
        [SerializeField] private Ease showEase = Ease.OutQuad;
        [SerializeField] private bool alsoFadeCanvasGroup = true;

        private CanvasGroup _panelCanvasGroup;
        private Vector2 _restAnchoredPosition;
        private Coroutine _deferredShowRoutine;
        private bool _presentationHidingActive;

        private void Awake()
        {
            if (panelRoot != null)
            {
                _restAnchoredPosition = panelRoot.anchoredPosition;
            }

            if (panelRoot != null)
            {
                _panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
                if (_panelCanvasGroup == null && alsoFadeCanvasGroup)
                {
                    _panelCanvasGroup = panelRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        private void OnEnable()
        {
            _presentationHidingActive = false;
            if (hub == null)
            {
                return;
            }

            hub.PlayerSkillHelpTextChanged += OnPlayerSkillHelpTextChanged;
            hub.ActionPresentationStarted += OnActionPresentationStarted;
            hub.ActionPresentationEnded += OnActionPresentationEnded;
            hub.CombatEnded += OnCombatEnded;
        }

        private void OnDisable()
        {
            if (hub != null)
            {
                hub.PlayerSkillHelpTextChanged -= OnPlayerSkillHelpTextChanged;
                hub.ActionPresentationStarted -= OnActionPresentationStarted;
                hub.ActionPresentationEnded -= OnActionPresentationEnded;
                hub.CombatEnded -= OnCombatEnded;
            }

            if (_deferredShowRoutine != null)
            {
                StopCoroutine(_deferredShowRoutine);
                _deferredShowRoutine = null;
            }

            KillPanelTweens();
        }

        private void OnPlayerSkillHelpTextChanged(string text)
        {
            if (helpBodyText != null)
            {
                helpBodyText.text = PlayerFacingText.PresentForUi(text);
            }

            if (_presentationHidingActive)
            {
                return;
            }

            ApplyVisibleLayout();
        }

        private void OnActionPresentationStarted()
        {
            if (panelRoot == null)
            {
                return;
            }

            KillPanelTweens();
            _presentationHidingActive = true;

            var hiddenPosition = _restAnchoredPosition + new Vector2(0f, hideAnchoredPositionDeltaY);
            panelRoot.DOAnchorPos(hiddenPosition, hideTweenDurationSeconds)
                .SetEase(hideEase)
                .SetLink(panelRoot.gameObject);

            if (_panelCanvasGroup != null && alsoFadeCanvasGroup)
            {
                _panelCanvasGroup.DOFade(0f, hideTweenDurationSeconds)
                    .SetEase(hideEase)
                    .SetLink(panelRoot.gameObject);
            }
        }

        private void OnActionPresentationEnded()
        {
            if (panelRoot == null)
            {
                return;
            }

            if (_deferredShowRoutine != null)
            {
                StopCoroutine(_deferredShowRoutine);
            }

            _deferredShowRoutine = StartCoroutine(ShowPanelNextFrame());
        }

        private IEnumerator ShowPanelNextFrame()
        {
            yield return null;
            _deferredShowRoutine = null;
            _presentationHidingActive = false;
            ApplyVisibleLayout();
        }

        private void OnCombatEnded()
        {
            KillPanelTweens();
            _presentationHidingActive = true;
            if (panelRoot != null)
            {
                panelRoot.anchoredPosition = _restAnchoredPosition + new Vector2(0f, hideAnchoredPositionDeltaY);
            }

            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 0f;
            }

            if (helpBodyText != null)
            {
                helpBodyText.text = string.Empty;
            }
        }

        private void ApplyVisibleLayout()
        {
            if (panelRoot == null)
            {
                return;
            }

            KillPanelTweens();
            panelRoot.DOAnchorPos(_restAnchoredPosition, hideTweenDurationSeconds)
                .SetEase(showEase)
                .SetLink(panelRoot.gameObject);

            if (_panelCanvasGroup != null && alsoFadeCanvasGroup)
            {
                _panelCanvasGroup.DOFade(1f, hideTweenDurationSeconds)
                    .SetEase(showEase)
                    .SetLink(panelRoot.gameObject);
            }
        }

        private void KillPanelTweens()
        {
            if (panelRoot == null)
            {
                return;
            }

            panelRoot.DOKill(false);
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.DOKill(false);
            }
        }
    }
}
