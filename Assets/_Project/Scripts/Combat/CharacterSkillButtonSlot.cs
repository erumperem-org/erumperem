using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    [DisallowMultipleComponent]
    public sealed class SkillButtonSlotPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CharacterSkillButtonSlot _host;

        public void Init(CharacterSkillButtonSlot host) => _host = host;

        public void OnPointerEnter(PointerEventData eventData) => _host?.HandlePointerEnter();

        public void OnPointerExit(PointerEventData eventData) => _host?.HandlePointerExit();
    }

    [DisallowMultipleComponent]
    public sealed class CharacterSkillButtonSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float PointerEnterScale = 1.05f;
        private const float PointerEnterDuration = 0.08f;
        private const float PointerExitDuration = 0.08f;
        private const float ClickPunchScale = 0.12f;
        private const float ClickPunchDuration = 0.12f;
        private const int ClickPunchVibrato = 6;
        private const float ClickPunchElasticity = 0.4f;
        private const float SelectedLocalScaleY = 0.88f;
        private const float SelectedPositionYOffset = -3f;
        private const float SelectionTweenDuration = 0.1f;
        private const string DefaultDescriptionPanelName = "SkillDescriptionPanel";
        private const string DefaultDescriptionTextName = "SkillDescriptionText";
        private const float DescriptionShowDuration = 0.16f;
        private const float DescriptionHideDuration = 0.1f;
        private const Ease DescriptionShowEase = Ease.OutBack;
        private const Ease DescriptionHideEase = Ease.InCubic;

        [SerializeField] private string descriptionPanelName = DefaultDescriptionPanelName;
        [SerializeField] private string descriptionTextName = DefaultDescriptionTextName;
        [Tooltip("CanvasGroup (fade) no painel; se faltar, é criada em runtime.")]
        [SerializeField] private bool ensureCanvasGroupOnDescriptionPanel = true;

        private Button _skillButton;
        private RectTransform _buttonRect;
        private Image _panelBackgroundImage;
        private Image _buttonImage;
        private RectTransform _rootRectTransform;
        private RectTransform _descriptionPanel;
        private TextMeshProUGUI _descriptionText;
        private CanvasGroup _descriptionCanvasGroup;
        private Vector3 _descriptionPanelBaseScale = Vector3.one;
        private SkillButtonSlotPointerRelay _pointerRelay;
        private CharacterSkillButtonsRowView _parentRow;
        private CanvasGroup _buttonCanvasGroup;

        private Action _onClick;
        private bool _isInteractable;
        private bool _isSelected;
        private string _playerDescriptionLine = string.Empty;
        private bool _isDescriptionPanelVisible;
        private bool _descriptionLayoutInitialized;
        private Vector2 _anchoredBase;
        private Vector3 _localScaleBase = Vector3.one;
        private Color _panelColorBase;
        private Color _buttonColorBase;
        private bool? _lastAppliedIsSelected;

        private void Awake()
        {
            _rootRectTransform = (RectTransform)transform;
            _panelBackgroundImage = GetComponent<Image>();
            _skillButton = GetComponentInChildren<Button>(true);
            if (_skillButton != null)
            {
                _buttonImage = _skillButton.targetGraphic as Image;
                _buttonRect = _skillButton.transform as RectTransform;
            }

            if (_rootRectTransform != null)
            {
                _anchoredBase = _rootRectTransform.anchoredPosition;
                _localScaleBase = _rootRectTransform.localScale;
            }

            if (_panelBackgroundImage != null)
            {
                _panelColorBase = _panelBackgroundImage.color;
                _panelBackgroundImage.raycastTarget = true;
            }

            if (_buttonImage != null)
            {
                _buttonColorBase = _buttonImage.color;
            }
        }

        private void Start()
        {
            CacheParentRow();
            TryResolveDescriptionUi();
        }

        public void Wire(Action onClick)
        {
            _onClick = onClick;
            if (_skillButton == null)
            {
                return;
            }

            _skillButton.onClick.RemoveAllListeners();
            _skillButton.onClick.AddListener(() =>
            {
                if (!_skillButton.interactable)
                {
                    return;
                }

                PlayClickFeedback();
                _onClick?.Invoke();
            });

            _pointerRelay = _skillButton.gameObject.GetComponent<SkillButtonSlotPointerRelay>();
            if (_pointerRelay == null)
            {
                _pointerRelay = _skillButton.gameObject.AddComponent<SkillButtonSlotPointerRelay>();
            }

            _pointerRelay.Init(this);
            if (_skillButton != null && _skillButton.targetGraphic != null)
            {
                _skillButton.targetGraphic.raycastTarget = true;
            }

            TryResolveDescriptionUi();
        }

        public void OnPointerEnter(PointerEventData eventData) => HandlePointerEnter();

        public void OnPointerExit(PointerEventData eventData) => HandlePointerExit();

        private void CacheParentRow() =>
            _parentRow = GetComponentInParent<CharacterSkillButtonsRowView>();

        /// <summary>Fecha a modal deste slot (chamada pela row quando o rato muda de botão no mesmo ecrã).</summary>
        public void DismissDescriptionFromSiblings() => HideDescriptionPanel();

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                ForceHideDescriptionPanelImmediate();
                _lastAppliedIsSelected = null;
            }

            gameObject.SetActive(visible);
        }

        public void ApplyVisuals(Color skillColor, bool interactable, bool selected, string playerDescriptionLine)
        {
            _playerDescriptionLine = playerDescriptionLine ?? string.Empty;
            _isInteractable = interactable;
            _isSelected = selected;
            if (_skillButton != null)
            {
                _skillButton.interactable = interactable;
                _buttonCanvasGroup = _buttonCanvasGroup != null
                    ? _buttonCanvasGroup
                    : _skillButton.GetComponent<CanvasGroup>();
                if (_buttonCanvasGroup == null)
                {
                    _buttonCanvasGroup = _skillButton.gameObject.AddComponent<CanvasGroup>();
                }

                _buttonCanvasGroup.interactable = true;
                _buttonCanvasGroup.blocksRaycasts = interactable;
            }

            if (_panelBackgroundImage != null)
            {
                var c = Color.Lerp(_panelColorBase, skillColor, 0.85f);
                c.a = _panelColorBase.a;
                _panelBackgroundImage.color = c;
            }

            if (_buttonImage != null)
            {
                var c = Color.Lerp(_buttonColorBase, skillColor, 0.6f);
                c.a = interactable ? 1f : 0.45f;
                _buttonImage.color = c;
            }

            if (_rootRectTransform == null)
            {
                return;
            }

            var wasFirstLayoutApply = !_lastAppliedIsSelected.HasValue;
            var selectionStateChanged = !wasFirstLayoutApply && _lastAppliedIsSelected.Value != selected;
            _lastAppliedIsSelected = selected;
            if (selectionStateChanged || wasFirstLayoutApply)
            {
                _rootRectTransform.DOKill(false);
                if (wasFirstLayoutApply && !selected)
                {
                    _rootRectTransform.localScale = _localScaleBase;
                    _rootRectTransform.anchoredPosition = new Vector2(
                        _anchoredBase.x,
                        _anchoredBase.y);
                }
                else if (selected)
                {
                    var targetScale = new Vector3(
                        _localScaleBase.x,
                        SelectedLocalScaleY * _localScaleBase.y,
                        _localScaleBase.z);
                    _rootRectTransform
                        .DOScale(targetScale, SelectionTweenDuration)
                        .SetEase(Ease.OutCubic)
                        .SetLink(gameObject);
                    _rootRectTransform
                        .DOAnchorPosY(
                            _anchoredBase.y + SelectedPositionYOffset,
                            SelectionTweenDuration)
                        .SetEase(Ease.OutCubic)
                        .SetLink(gameObject);
                }
                else
                {
                    _rootRectTransform
                        .DOScale(_localScaleBase, SelectionTweenDuration)
                        .SetEase(Ease.OutCubic)
                        .SetLink(gameObject);
                    _rootRectTransform
                        .DOAnchorPosY(_anchoredBase.y, SelectionTweenDuration)
                        .SetEase(Ease.OutCubic)
                        .SetLink(gameObject);
                }
            }

            if (_isDescriptionPanelVisible && _descriptionText != null)
            {
                _descriptionText.text = _playerDescriptionLine;
                _descriptionText.color = new Color(1f, 1f, 1f, 1f);
                _descriptionText.ForceMeshUpdate(true);
            }
        }

        public void HandlePointerEnter()
        {
            CacheParentRow();
            _parentRow?.DismissOtherDescriptionPanels(this);
            ShowDescriptionIfPossible();
            TweenButtonHoverEnter();
            if (_rootRectTransform == null || _isSelected)
            {
                return;
            }

            if (!_isInteractable)
            {
                return;
            }

            _rootRectTransform.DOKill(false);
            _rootRectTransform
                .DOScale(_localScaleBase * PointerEnterScale, PointerEnterDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        public void HandlePointerExit()
        {
            HideDescriptionPanel();
            TweenButtonHoverExit();
            HandlePointerExited(animate: true);
        }

        private void TweenButtonHoverEnter()
        {
            if (_buttonRect == null)
            {
                return;
            }

            _buttonRect.DOKill(false);
            _buttonRect
                .DOPunchScale(new Vector3(0.06f, 0.06f, 0.06f), 0.14f, 6, 0.3f)
                .SetLink(_buttonRect.gameObject);
        }

        private void TweenButtonHoverExit()
        {
            if (_buttonRect == null)
            {
                return;
            }

            _buttonRect.DOKill(false);
        }

        private void HandlePointerExited(bool animate)
        {
            if (_rootRectTransform == null || _isSelected)
            {
                return;
            }

            if (!animate)
            {
                _rootRectTransform.localScale = _localScaleBase;
                return;
            }

            _rootRectTransform.DOKill(false);
            _rootRectTransform
                .DOScale(_localScaleBase, PointerExitDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }

        private void PlayClickFeedback()
        {
            if (_rootRectTransform == null)
            {
                return;
            }

            _rootRectTransform
                .DOPunchScale(new Vector3(ClickPunchScale, ClickPunchScale * 1.2f, 0f), ClickPunchDuration, ClickPunchVibrato, ClickPunchElasticity)
                .SetLink(gameObject);
        }

        private void OnDisable()
        {
            ForceHideDescriptionPanelImmediate();
            if (_rootRectTransform != null)
            {
                _rootRectTransform.DOKill(false);
            }

            if (_buttonRect != null)
            {
                _buttonRect.DOKill(false);
            }
        }

        private void TryResolveDescriptionUi()
        {
            if (_skillButton == null)
            {
                return;
            }

            if (_descriptionPanel == null)
            {
                _descriptionPanel = FindDescendantByName(
                        _skillButton.transform,
                        descriptionPanelName)
                    as RectTransform;
            }

            if (_descriptionPanel == null)
            {
                return;
            }

            if (!_descriptionLayoutInitialized)
            {
                _descriptionPanelBaseScale = _descriptionPanel.localScale.sqrMagnitude < 0.0001f
                    ? Vector3.one
                    : _descriptionPanel.localScale;
            }

            if (_descriptionText == null)
            {
                if (!string.IsNullOrEmpty(descriptionTextName))
                {
                    var textTransform = FindDescendantByName(
                        _descriptionPanel,
                        descriptionTextName);
                    if (textTransform != null)
                    {
                        _descriptionText = textTransform.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (_descriptionText == null)
                {
                    _descriptionText = _descriptionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
                }
            }

            if (ensureCanvasGroupOnDescriptionPanel)
            {
                if (_descriptionCanvasGroup == null)
                {
                    _descriptionCanvasGroup = _descriptionPanel.GetComponent<CanvasGroup>();
                    if (_descriptionCanvasGroup == null)
                    {
                        _descriptionCanvasGroup = _descriptionPanel.gameObject.AddComponent<CanvasGroup>();
                    }
                }
            }

            if (_descriptionLayoutInitialized)
            {
                return;
            }

            _descriptionLayoutInitialized = true;
            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.alpha = 0f;
                _descriptionCanvasGroup.interactable = false;
                _descriptionCanvasGroup.blocksRaycasts = false;
            }

            _descriptionPanel.localScale = Vector3.zero;
            if (!_isDescriptionPanelVisible)
            {
                _descriptionPanel.gameObject.SetActive(false);
            }
        }

        private static Transform FindDescendantByName(Transform searchRoot, string nameToMatch)
        {
            if (searchRoot == null || string.IsNullOrEmpty(nameToMatch))
            {
                return null;
            }

            var all = searchRoot.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < all.Length; index++)
            {
                var t = all[index];
                if (t != null && string.Equals(t.name, nameToMatch, StringComparison.Ordinal))
                {
                    return t;
                }
            }

            for (var index = 0; index < all.Length; index++)
            {
                var t = all[index];
                if (t != null && string.Equals(t.name, nameToMatch, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }

        private void ShowDescriptionIfPossible()
        {
            TryResolveDescriptionUi();
            if (string.IsNullOrEmpty(_playerDescriptionLine) || _descriptionPanel == null)
            {
                return;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = _playerDescriptionLine;
                _descriptionText.color = new Color(1f, 1f, 1f, 1f);
                _descriptionText.ForceMeshUpdate(true);
            }

            if (_descriptionCanvasGroup == null)
            {
                _descriptionCanvasGroup = _descriptionPanel.GetComponent<CanvasGroup>();
            }

            _descriptionPanel.DOKill(false);
            _descriptionPanel.gameObject.SetActive(true);
            _isDescriptionPanelVisible = true;
            _descriptionPanel.localScale = Vector3.zero;
            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.alpha = 0f;
            }

            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.DOKill(false);
                _descriptionCanvasGroup
                    .DOFade(1f, DescriptionShowDuration * 0.9f)
                    .SetEase(DescriptionShowEase)
                    .SetLink(_descriptionPanel.gameObject);
            }

            _descriptionPanel
                .DOScale(_descriptionPanelBaseScale, DescriptionShowDuration)
                .SetEase(DescriptionShowEase)
                .SetLink(_descriptionPanel.gameObject);
        }

        private void HideDescriptionPanel()
        {
            if (_descriptionPanel == null || !_isDescriptionPanelVisible)
            {
                return;
            }

            _descriptionPanel.DOKill(false);
            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.DOKill(false);
            }

            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup
                    .DOFade(0f, DescriptionHideDuration)
                    .SetEase(DescriptionHideEase)
                    .SetLink(_descriptionPanel.gameObject);
            }

            _descriptionPanel
                .DOScale(0f, DescriptionHideDuration)
                .SetEase(DescriptionHideEase)
                .SetLink(_descriptionPanel.gameObject)
                .OnComplete(() =>
                {
                    if (_descriptionPanel != null)
                    {
                        if (_descriptionCanvasGroup != null)
                        {
                            _descriptionCanvasGroup.alpha = 0f;
                        }

                        _descriptionPanel.gameObject.SetActive(false);
                    }

                    _isDescriptionPanelVisible = false;
                });
        }

        private void ForceHideDescriptionPanelImmediate()
        {
            if (_descriptionPanel == null)
            {
                return;
            }

            _descriptionPanel.DOKill(false);
            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.DOKill(false);
                _descriptionCanvasGroup.alpha = 0f;
            }

            _descriptionPanel.localScale = Vector3.zero;
            _descriptionPanel.gameObject.SetActive(false);
            _isDescriptionPanelVisible = false;
        }
    }
}
