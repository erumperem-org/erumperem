using DG.Tweening;
using Erumperem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Hover handler for one TokenIcon: shows a child <see cref="TokenDescriptionPanelChildName"/>
    /// with a DOTween punch and assigns rich-formatted text to a child
    /// <see cref="TokenDescriptionTextChildName"/>. Auto-resolves children by name in
    /// <see cref="OnValidate"/> / <see cref="Awake"/>; you can override via the inspector.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class TokenIconHoverDescriptionView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        public const string TokenDescriptionPanelChildName = "TokenDescriptionPanel";
        public const string TokenDescriptionTextChildName = "TokenDescriptionText";

        private const string PanelPunchTweenId = "TokenDescPanelPunch";
        private const string PanelFadeTweenId = "TokenDescPanelFade";

        [Header("Bindings (auto-resolved by name if empty)")]
        [Tooltip("Painel raiz da tooltip; é activado/desactivado e recebe o punch.")]
        [SerializeField] private RectTransform _descriptionPanelRoot;

        [Tooltip("TMP que recebe o texto formatado (já passa por PlayerFacingText.PresentForUi).")]
        [SerializeField] private TextMeshProUGUI _descriptionTextLabel;

        [Tooltip("CanvasGroup opcional sobre o painel, para fade in/out. Auto-criado se vazio.")]
        [SerializeField] private CanvasGroup _descriptionPanelCanvasGroup;

        [Header("Punch tween")]
        [SerializeField] private Vector3 _panelPunchScale = new Vector3(0.18f, 0.22f, 0f);
        [SerializeField] private float _panelPunchDuration = 0.32f;
        [SerializeField] private int _panelPunchVibrato = 8;
        [SerializeField] private float _panelPunchElasticity = 0.6f;

        [Header("Fade tween")]
        [SerializeField] private float _panelFadeInSeconds = 0.12f;
        [SerializeField] private float _panelFadeOutSeconds = 0.08f;

        [Header("Behavior")]
        [Tooltip("Quando ON, desliga o RaycastTarget de todas as imagens / TMP do painel para que " +
                 "o cursor continue a contar como sobre o ícone (evita pingue-pongue de Enter/Exit).")]
        [SerializeField] private bool _disableRaycastOnPanelChildren = true;

        [Tooltip("Se true, esconde o painel imediatamente ao perder o hover (sem esperar fade).")]
        [SerializeField] private bool _hideImmediatelyOnExit = false;

        private string _authoredDescriptionMarkup = string.Empty;
        private bool _isPointerInside;

        private void Reset() => AutoResolveChildrenByName();

        private void OnValidate()
        {
            if (_descriptionPanelRoot == null || _descriptionTextLabel == null)
            {
                AutoResolveChildrenByName();
            }
        }

        private void Awake()
        {
            if (_descriptionPanelRoot == null || _descriptionTextLabel == null)
            {
                AutoResolveChildrenByName();
            }

            EnsureCanvasGroup();
            ApplyRaycastBlockingPolicy();
            HidePanelImmediate();
        }

        private void OnDisable()
        {
            KillPanelTweens();
            HidePanelImmediate();
            _isPointerInside = false;
        }

        /// <summary>Called by the strip presenter when this slot is configured for a specific token / DOT.</summary>
        public void Configure(string authoredMarkupDescription)
        {
            _authoredDescriptionMarkup = authoredMarkupDescription ?? string.Empty;
            if (_isPointerInside)
            {
                AssignFormattedTextToLabel();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            if (string.IsNullOrEmpty(_authoredDescriptionMarkup) || _descriptionPanelRoot == null)
            {
                return;
            }

            AssignFormattedTextToLabel();
            ShowPanelWithPunch();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            HidePanelWithFade();
        }

        private void AssignFormattedTextToLabel()
        {
            if (_descriptionTextLabel == null)
            {
                return;
            }

            _descriptionTextLabel.text = PlayerFacingText.PresentForUi(_authoredDescriptionMarkup);
        }

        private void ShowPanelWithPunch()
        {
            KillPanelTweens();
            _descriptionPanelRoot.gameObject.SetActive(true);
            _descriptionPanelRoot.localScale = Vector3.one;

            if (_descriptionPanelCanvasGroup != null)
            {
                _descriptionPanelCanvasGroup.alpha = 0f;
                _descriptionPanelCanvasGroup
                    .DOFade(1f, _panelFadeInSeconds)
                    .SetId(GetTweenIdScopedToInstance(PanelFadeTweenId))
                    .SetLink(_descriptionPanelRoot.gameObject);
            }

            _descriptionPanelRoot
                .DOPunchScale(_panelPunchScale, _panelPunchDuration, _panelPunchVibrato, _panelPunchElasticity)
                .SetId(GetTweenIdScopedToInstance(PanelPunchTweenId))
                .SetLink(_descriptionPanelRoot.gameObject);
        }

        private void HidePanelWithFade()
        {
            if (_descriptionPanelRoot == null || !_descriptionPanelRoot.gameObject.activeSelf)
            {
                return;
            }

            KillPanelTweens();

            if (_hideImmediatelyOnExit || _descriptionPanelCanvasGroup == null || _panelFadeOutSeconds <= 0f)
            {
                HidePanelImmediate();
                return;
            }

            _descriptionPanelCanvasGroup
                .DOFade(0f, _panelFadeOutSeconds)
                .SetId(GetTweenIdScopedToInstance(PanelFadeTweenId))
                .SetLink(_descriptionPanelRoot.gameObject)
                .OnComplete(HidePanelImmediate);
        }

        private void HidePanelImmediate()
        {
            if (_descriptionPanelRoot == null)
            {
                return;
            }

            if (_descriptionPanelCanvasGroup != null)
            {
                _descriptionPanelCanvasGroup.alpha = 0f;
            }

            _descriptionPanelRoot.gameObject.SetActive(false);
            _descriptionPanelRoot.localScale = Vector3.one;
        }

        private void EnsureCanvasGroup()
        {
            if (_descriptionPanelRoot == null)
            {
                return;
            }

            if (_descriptionPanelCanvasGroup == null)
            {
                _descriptionPanelCanvasGroup = _descriptionPanelRoot.GetComponent<CanvasGroup>();
                if (_descriptionPanelCanvasGroup == null)
                {
                    _descriptionPanelCanvasGroup = _descriptionPanelRoot.gameObject.AddComponent<CanvasGroup>();
                }
            }

            _descriptionPanelCanvasGroup.alpha = 0f;
            _descriptionPanelCanvasGroup.interactable = false;
            _descriptionPanelCanvasGroup.blocksRaycasts = false;
        }

        private void ApplyRaycastBlockingPolicy()
        {
            if (!_disableRaycastOnPanelChildren || _descriptionPanelRoot == null)
            {
                return;
            }

            foreach (var graphic in _descriptionPanelRoot.GetComponentsInChildren<Graphic>(includeInactive: true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void KillPanelTweens()
        {
            DOTween.Kill(GetTweenIdScopedToInstance(PanelPunchTweenId), false);
            DOTween.Kill(GetTweenIdScopedToInstance(PanelFadeTweenId), false);
        }

        private string GetTweenIdScopedToInstance(string baseId) => $"{baseId}_{GetInstanceID()}";

        private void AutoResolveChildrenByName()
        {
            var rectTransform = (RectTransform)transform;
            if (_descriptionPanelRoot == null)
            {
                _descriptionPanelRoot = FindDescendantRectTransformByName(rectTransform, TokenDescriptionPanelChildName);
            }

            if (_descriptionTextLabel == null && _descriptionPanelRoot != null)
            {
                var textTransform = FindDescendantRectTransformByName(_descriptionPanelRoot, TokenDescriptionTextChildName);
                if (textTransform != null)
                {
                    _descriptionTextLabel = textTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            if (_descriptionTextLabel == null && _descriptionPanelRoot != null)
            {
                _descriptionTextLabel = _descriptionPanelRoot.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        private static RectTransform FindDescendantRectTransformByName(RectTransform root, string childName)
        {
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var child = root.GetChild(childIndex);
                if (child.name == childName && child is RectTransform childAsRect)
                {
                    return childAsRect;
                }

                if (child is RectTransform childRect)
                {
                    var nested = FindDescendantRectTransformByName(childRect, childName);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }
    }
}
