using System;
using DG.Tweening;
using Erumperem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// Controla o painel visual de uma skill no combate.
    /// Mouse:
    /// - Hover apenas anima o botão.
    /// - Clique seleciona, centraliza e mostra a descrição completa.
    ///
    /// Teclado:
    /// - O foco mostra a descrição completa.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillButtonPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const float PointerEnterScale = 1.05f;
        private const float PointerEnterDuration = 0.08f;
        private const float PointerExitDuration = 0.08f;
        private const float SelectedLocalScale = 1.2f;
        private const float SelectionTweenDuration = 0.1f;

        private const string DefaultDescriptionPanelName = "SkillDescriptionPanel";
        private const string DefaultDescriptionTextName = "SkillDescriptionText";

        private const float DescriptionShowDuration = 0.16f;
        private const float DescriptionHideDuration = 0.1f;

        private const Ease DescriptionShowEase = Ease.OutBack;
        private const Ease DescriptionHideEase = Ease.InCubic;

        [SerializeField] private string descriptionPanelName = DefaultDescriptionPanelName;
        [SerializeField] private string descriptionTextName = DefaultDescriptionTextName;

        [Tooltip("CanvasGroup usado para fade do painel. Se não existir, é criado em runtime.")]
        [SerializeField] private bool ensureCanvasGroupOnDescriptionPanel = true;

        [Header("Animação")]
        [SerializeField] private bool useSelectionScaleTween = true;

        public event Action<SkillButtonPanelView> PointerEntered;
        public event Action<SkillButtonPanelView> PointerExited;

        private Button _skillButton;
        private RectTransform _buttonRect;
        private Image _panelBackgroundImage;
        private Image _buttonImage;
        private RectTransform _rootRectTransform;

        private RectTransform _descriptionPanel;
        private TextMeshProUGUI _descriptionText;
        private CanvasGroup _descriptionCanvasGroup;

        private Vector3 _descriptionPanelBaseScale = Vector3.one;

        private Vector2 _rootAnchoredBase;
        private Vector3 _rootLocalScaleBase = Vector3.one;
        private Vector2 _rootSizeDeltaBase;

        private Vector2 _buttonAnchoredBase;
        private Vector3 _buttonLocalScaleBase = Vector3.one;
        private Vector2 _buttonSizeDeltaBase;

        private Color _panelColorBase;
        private Color _buttonColorBase;

        private CanvasGroup _buttonCanvasGroup;
        private SkillButtonSlotPointerRelay _pointerRelay;
        private CharacterSkillButtonsRowView _parentRow;

        private bool _isInteractable;
        private bool _isSelected;

        private Color _skillIconColor = Color.white;

        private Selectable.Transition _buttonTransitionBeforeSelectionLock =
            Selectable.Transition.ColorTint;

        private bool _isButtonTransitionLockedForSelection;

        private string _playerDescriptionLine = string.Empty;

        private bool _isDescriptionPanelVisible;
        private bool _descriptionLayoutInitialized;
        private bool? _lastAppliedIsSelected;

        private TextMeshProUGUI _hotkeyDigitLabel;

        private int _zeroBasedSlotIndex;

        private bool _initialLayoutCaptured;
        private bool _isPointerInside;
        private bool _descriptionPinnedByMouseSelection;

        private InfinitySkillScroll _skillScroll;

        public int ZeroBasedSlotIndex => _zeroBasedSlotIndex;

        private void Awake()
        {
            EnsureButtonReferencesResolved();
        }

        private void Start()
        {
            CaptureInitialLayout();
            CacheParentRow();
            TryResolveDescriptionUi();
        }

        public void Wire(int zeroBasedSlotIndex, Action onSlotClickRequested)
        {
            EnsureButtonReferencesResolved();

            _zeroBasedSlotIndex = zeroBasedSlotIndex;

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

                onSlotClickRequested?.Invoke();

                if (!global::InputDeviceModeManager.IsMouseMode)
                {
                    return;
                }

                CacheParentRow();

                _parentRow?.DismissOtherDescriptionPanels(this);

                _descriptionPinnedByMouseSelection = true;

                CenterOnThisSkill();

                ShowFullDescription();
            });

            _pointerRelay =
                _skillButton.gameObject.GetComponent<SkillButtonSlotPointerRelay>();

            if (_pointerRelay == null)
            {
                _pointerRelay =
                    _skillButton.gameObject.AddComponent<SkillButtonSlotPointerRelay>();
            }

            _pointerRelay.Init(this);

            if (_skillButton.targetGraphic != null)
            {
                _skillButton.targetGraphic.raycastTarget = true;
            }

            TryCacheHotkeyDigitLabel();
            TryResolveDescriptionUi();
        }

        private void EnsureButtonReferencesResolved()
        {
            if (_rootRectTransform == null)
            {
                _rootRectTransform = transform as RectTransform;
            }

            if (_panelBackgroundImage == null)
            {
                _panelBackgroundImage = GetComponent<Image>();

                if (_panelBackgroundImage != null)
                {
                    _panelColorBase = _panelBackgroundImage.color;
                    _panelBackgroundImage.raycastTarget = true;
                }
            }

            if (_skillButton == null)
            {
                _skillButton = GetComponent<Button>();

                if (_skillButton == null)
                {
                    _skillButton = GetComponentInChildren<Button>(true);
                }
            }

            if (_skillButton == null)
            {
                return;
            }

            if (_buttonImage == null)
            {
                _buttonImage = _skillButton.targetGraphic as Image;

                if (_buttonImage != null)
                {
                    _buttonColorBase = _buttonImage.color;
                }
            }

            if (_buttonRect == null)
            {
                _buttonRect = _skillButton.transform as RectTransform;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HandlePointerEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HandlePointerExit();
        }

        public void DismissDescriptionFromSiblings()
        {
            HideDescriptionPanel();
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                ForceHideDescriptionPanelImmediate();

                _lastAppliedIsSelected = null;
                _isSelected = false;

                if (_isButtonTransitionLockedForSelection &&
                    _skillButton != null)
                {
                    _skillButton.transition =
                        _buttonTransitionBeforeSelectionLock;

                    _isButtonTransitionLockedForSelection = false;
                }
            }

            gameObject.SetActive(visible);
        }

        public void ApplyVisuals(
            Sprite skillIconOrNull,
            Color skillIconColor,
            bool interactable,
            bool selected,
            string playerDescriptionLine,
            int hotkeyLabelOneToSeven)
        {
            var wasSelected = _isSelected;

            _playerDescriptionLine =
                PlayerFacingText.PresentForUi(
                    playerDescriptionLine ?? string.Empty);

            _isInteractable = interactable;
            _isSelected = selected;
            _skillIconColor = skillIconColor;

            if (!selected)
            {
                _descriptionPinnedByMouseSelection = false;
            }

            TryCacheHotkeyDigitLabel();

            if (_hotkeyDigitLabel != null &&
                hotkeyLabelOneToSeven >= 1 &&
                hotkeyLabelOneToSeven <=
                CharacterSkillButtonsRowView.MaxVisibleSlots)
            {
                _hotkeyDigitLabel.text =
                    hotkeyLabelOneToSeven.ToString();
            }

            ApplySkillIconVisuals(
                skillIconOrNull,
                skillIconColor,
                interactable);

            if (_skillButton != null)
            {
                _skillButton.interactable = interactable;

                _buttonCanvasGroup =
                    _buttonCanvasGroup != null
                        ? _buttonCanvasGroup
                        : _skillButton.GetComponent<CanvasGroup>();

                if (_buttonCanvasGroup == null)
                {
                    _buttonCanvasGroup =
                        _skillButton.gameObject.AddComponent<CanvasGroup>();
                }

                _buttonCanvasGroup.interactable = true;
                _buttonCanvasGroup.blocksRaycasts = interactable;
            }

            if (_rootRectTransform == null)
            {
                SyncSelectedPressedChrome();
                return;
            }

            if (!_initialLayoutCaptured)
            {
                CaptureInitialLayout();
            }

            var wasFirstLayoutApply =
                !_lastAppliedIsSelected.HasValue;

            var selectionStateChanged =
                !wasFirstLayoutApply &&
                _lastAppliedIsSelected.Value != selected;

            _lastAppliedIsSelected = selected;

            if (selectionStateChanged ||
                wasFirstLayoutApply)
            {
                KillTweensOnButtonChrome();

                if (wasFirstLayoutApply &&
                    !selected)
                {
                    RestoreRootLayout();
                }
                else if (useSelectionScaleTween)
                {
                    if (selected)
                    {
                        var targetScale =
                            GetSelectedRootScale();

                        _rootRectTransform
                            .DOScale(
                                targetScale,
                                SelectionTweenDuration)
                            .SetEase(Ease.OutCubic)
                            .SetLink(gameObject)
                            .OnComplete(
                                RestoreRootLayoutForCurrentSelection);
                    }
                    else
                    {
                        _rootRectTransform
                            .DOScale(
                                _rootLocalScaleBase,
                                SelectionTweenDuration)
                            .SetEase(Ease.OutCubic)
                            .SetLink(gameObject)
                            .OnComplete(
                                RestoreRootLayoutForCurrentSelection);
                    }
                }
                else
                {
                    RestoreRootLayoutForCurrentSelection();
                }
            }
            else
            {
                RestoreRootLayoutForCurrentSelection();
            }

            SyncSelectedPressedChrome();

            if (wasSelected &&
                !selected &&
                _isDescriptionPanelVisible)
            {
                HideDescriptionPanel();
                return;
            }

            if (_isDescriptionPanelVisible)
            {
                UpdateDescriptionText();
            }
        }

        public void HandlePointerEnter()
        {
            _isPointerInside = true;

            CacheParentRow();

            /*
             * MOUSE:
             * Hover não altera o painel de descrição.
             *
             * TECLADO:
             * CombatInputController chama este método para simular o foco,
             * então mostramos a descrição normalmente.
             */
            if (!global::InputDeviceModeManager.IsMouseMode)
            {
                _parentRow?.DismissOtherDescriptionPanels(this);
                ShowFullDescription();
            }

            TweenButtonHoverEnter();

            PointerEntered?.Invoke(this);

            if (_rootRectTransform == null ||
                _isSelected)
            {
                return;
            }

            if (!_isInteractable)
            {
                return;
            }

            _rootRectTransform.DOKill(false);

            _rootRectTransform
                .DOScale(
                    _rootLocalScaleBase * PointerEnterScale,
                    PointerEnterDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(
                    RestoreRootLayoutForCurrentSelection);
        }

        public void HandlePointerExit()
        {
            _isPointerInside = false;

            /*
             * Em Mouse Mode o hover nunca abriu a descrição.
             *
             * Se a descrição estiver aberta porque a skill foi clicada,
             * ela permanece aberta.
             *
             * Em Keyboard Mode o foco funciona como antes e a descrição
             * do item anterior é fechada ao sair dele.
             */
            if (!global::InputDeviceModeManager.IsMouseMode)
            {
                HideDescriptionPanel();
            }
            else if (!_isSelected &&
                     !_descriptionPinnedByMouseSelection)
            {
                HideDescriptionPanel();
            }

            TweenButtonHoverExit();

            HandlePointerExited(
                animate: true);

            PointerExited?.Invoke(this);
        }

        private void CaptureInitialLayout()
        {
            if (_rootRectTransform != null)
            {
                _rootAnchoredBase =
                    _rootRectTransform.anchoredPosition;

                _rootLocalScaleBase =
                    _rootRectTransform.localScale;

                _rootSizeDeltaBase =
                    _rootRectTransform.sizeDelta;
            }

            if (_buttonRect != null)
            {
                _buttonAnchoredBase =
                    _buttonRect.anchoredPosition;

                _buttonLocalScaleBase =
                    _buttonRect.localScale;

                _buttonSizeDeltaBase =
                    _buttonRect.sizeDelta;
            }

            _initialLayoutCaptured =
                _rootRectTransform != null;
        }

        private void RestoreRootLayout()
        {
            if (_rootRectTransform == null)
            {
                return;
            }

            _rootRectTransform.anchoredPosition =
                _rootAnchoredBase;

            _rootRectTransform.localScale =
                _rootLocalScaleBase;

            _rootRectTransform.sizeDelta =
                _rootSizeDeltaBase;
        }

        private void RestoreRootLayoutForCurrentSelection()
        {
            if (_rootRectTransform == null)
            {
                return;
            }

            _rootRectTransform.anchoredPosition =
                _rootAnchoredBase;

            _rootRectTransform.sizeDelta =
                _rootSizeDeltaBase;

            _rootRectTransform.localScale =
                _isSelected
                    ? GetSelectedRootScale()
                    : _rootLocalScaleBase;
        }

        private Vector3 GetSelectedRootScale()
        {
            return new Vector3(
                SelectedLocalScale *
                _rootLocalScaleBase.x,

                SelectedLocalScale *
                _rootLocalScaleBase.y,

                SelectedLocalScale *
                _rootLocalScaleBase.z);
        }

        private void RestoreButtonLayout()
        {
            if (_buttonRect == null)
            {
                return;
            }

            _buttonRect.anchoredPosition =
                _buttonAnchoredBase;

            _buttonRect.localScale =
                _buttonLocalScaleBase;

            _buttonRect.sizeDelta =
                _buttonSizeDeltaBase;
        }

        private void KillTweensOnButtonChrome()
        {
            if (_rootRectTransform != null)
            {
                _rootRectTransform.DOKill(false);
            }

            if (_buttonRect != null)
            {
                _buttonRect.DOKill(false);
            }
        }

        private void TweenButtonHoverEnter()
        {
            if (_buttonRect == null ||
                _isSelected)
            {
                return;
            }

            _buttonRect.DOKill(false);

            _buttonRect
                .DOPunchScale(
                    new Vector3(
                        0.06f,
                        0.06f,
                        0.06f),
                    0.14f,
                    6,
                    0.3f)
                .SetLink(
                    _buttonRect.gameObject)
                .OnComplete(() =>
                {
                    RestoreButtonLayout();
                });
        }

        private void TweenButtonHoverExit()
        {
            if (_buttonRect == null)
            {
                return;
            }

            _buttonRect.DOKill(false);

            RestoreButtonLayout();
        }

        private void HandlePointerExited(
            bool animate)
        {
            if (_rootRectTransform == null ||
                _isSelected)
            {
                return;
            }

            if (!animate)
            {
                RestoreRootLayout();
                return;
            }

            _rootRectTransform.DOKill(false);

            _rootRectTransform
                .DOScale(
                    _rootLocalScaleBase,
                    PointerExitDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(
                    RestoreRootLayoutForCurrentSelection);
        }

        private void PlayClickFeedback()
        {
            if (_rootRectTransform == null)
            {
                return;
            }

            _rootRectTransform
                .DOScale(
                    new Vector3(
                        0.95f,
                        0.95f,
                        0.95f),
                    0.1f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(
                    RestoreRootLayoutForCurrentSelection);
        }

        private void ApplySkillIconVisuals(
            Sprite skillIconOrNull,
            Color skillIconColor,
            bool interactable)
        {
            EnsureButtonReferencesResolved();

            if (_buttonImage == null)
            {
                return;
            }

            if (skillIconOrNull != null)
            {
                _buttonImage.sprite =
                    skillIconOrNull;
            }

            _buttonImage.preserveAspect = false;

            _skillIconColor =
                skillIconColor;

            _buttonImage.color =
                BuildDisplayedSkillIconColor(
                    skillIconColor,
                    interactable,
                    _isSelected);
        }

        private void SyncSelectedPressedChrome()
        {
            EnsureButtonReferencesResolved();

            if (_skillButton == null ||
                _buttonImage == null)
            {
                return;
            }

            if (_isSelected)
            {
                if (!_isButtonTransitionLockedForSelection)
                {
                    _buttonTransitionBeforeSelectionLock =
                        _skillButton.transition;

                    _isButtonTransitionLockedForSelection =
                        true;
                }

                _skillButton.transition =
                    Selectable.Transition.None;

                _buttonImage.color =
                    BuildDisplayedSkillIconColor(
                        _skillIconColor,
                        _isInteractable,
                        isSelected: true);

                _buttonImage.CrossFadeColor(
                    _buttonImage.color,
                    0f,
                    true,
                    true);

                return;
            }

            if (_isButtonTransitionLockedForSelection)
            {
                _skillButton.transition =
                    _buttonTransitionBeforeSelectionLock;

                _isButtonTransitionLockedForSelection =
                    false;
            }

            _buttonImage.color =
                BuildDisplayedSkillIconColor(
                    _skillIconColor,
                    _isInteractable,
                    isSelected: false);

            _buttonImage.CrossFadeColor(
                _buttonImage.color,
                0f,
                true,
                true);
        }

        private Color BuildDisplayedSkillIconColor(
            Color skillIconColor,
            bool interactable,
            bool isSelected)
        {
            var displayedColor =
                skillIconColor;

            displayedColor.a =
                interactable
                    ? skillIconColor.a
                    : skillIconColor.a * 0.45f;

            return displayedColor;
        }

        private void CacheParentRow()
        {
            _parentRow =
                GetComponentInParent<CharacterSkillButtonsRowView>();
        }

        private void OnDisable()
        {
            ForceHideDescriptionPanelImmediate();

            KillTweensOnButtonChrome();

            if (_isButtonTransitionLockedForSelection &&
                _skillButton != null)
            {
                _skillButton.transition =
                    _buttonTransitionBeforeSelectionLock;

                _isButtonTransitionLockedForSelection =
                    false;
            }

            if (_rootRectTransform != null)
            {
                RestoreRootLayout();
            }

            if (_buttonRect != null)
            {
                RestoreButtonLayout();
            }
        }

        private void TryCacheHotkeyDigitLabel()
        {
            if (_hotkeyDigitLabel != null ||
                _skillButton == null)
            {
                return;
            }

            if (_skillButton.transform.childCount > 0)
            {
                var firstChild =
                    _skillButton.transform.GetChild(0);

                _hotkeyDigitLabel =
                    firstChild.GetComponent<TextMeshProUGUI>();
            }

            if (_hotkeyDigitLabel == null)
            {
                _hotkeyDigitLabel =
                    _skillButton
                        .GetComponentInChildren<TextMeshProUGUI>(
                            true);
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
                _descriptionPanel =
                    FindDescendantByName(
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
                _descriptionPanelBaseScale =
                    _descriptionPanel.localScale.sqrMagnitude <
                    0.0001f
                        ? Vector3.one
                        : _descriptionPanel.localScale;
            }

            if (_descriptionText == null)
            {
                if (!string.IsNullOrEmpty(
                        descriptionTextName))
                {
                    var textTransform =
                        FindDescendantByName(
                            _descriptionPanel,
                            descriptionTextName);

                    if (textTransform != null)
                    {
                        _descriptionText =
                            textTransform
                                .GetComponent<TextMeshProUGUI>();
                    }
                }

                if (_descriptionText == null)
                {
                    _descriptionText =
                        _descriptionPanel
                            .GetComponentInChildren<TextMeshProUGUI>(
                                true);
                }
            }

            if (ensureCanvasGroupOnDescriptionPanel)
            {
                if (_descriptionCanvasGroup == null)
                {
                    _descriptionCanvasGroup =
                        _descriptionPanel
                            .GetComponent<CanvasGroup>();

                    if (_descriptionCanvasGroup == null)
                    {
                        _descriptionCanvasGroup =
                            _descriptionPanel.gameObject
                                .AddComponent<CanvasGroup>();
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

            _descriptionPanel.localScale =
                Vector3.zero;

            if (!_isDescriptionPanelVisible)
            {
                _descriptionPanel.gameObject
                    .SetActive(false);
            }
        }

        private static Transform FindDescendantByName(
            Transform searchRoot,
            string nameToMatch)
        {
            if (searchRoot == null ||
                string.IsNullOrEmpty(nameToMatch))
            {
                return null;
            }

            var all =
                searchRoot.GetComponentsInChildren<Transform>(
                    true);

            for (var index = 0;
                 index < all.Length;
                 index++)
            {
                var current =
                    all[index];

                if (current != null &&
                    string.Equals(
                        current.name,
                        nameToMatch,
                        StringComparison.Ordinal))
                {
                    return current;
                }
            }

            for (var index = 0;
                 index < all.Length;
                 index++)
            {
                var current =
                    all[index];

                if (current != null &&
                    string.Equals(
                        current.name,
                        nameToMatch,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            return null;
        }

        private void ShowFullDescription()
        {
            TryResolveDescriptionUi();

            if (string.IsNullOrEmpty(
                    _playerDescriptionLine) ||
                _descriptionPanel == null)
            {
                return;
            }

            UpdateDescriptionText();

            if (_descriptionCanvasGroup == null)
            {
                _descriptionCanvasGroup =
                    _descriptionPanel
                        .GetComponent<CanvasGroup>();
            }

            _descriptionPanel.DOKill(false);

            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.DOKill(false);
            }

            _descriptionPanel.gameObject.SetActive(true);

            if (_isDescriptionPanelVisible)
            {
                _descriptionPanel.localScale =
                    _descriptionPanelBaseScale;

                if (_descriptionCanvasGroup != null)
                {
                    _descriptionCanvasGroup.alpha =
                        1f;
                }

                return;
            }

            _isDescriptionPanelVisible = true;

            _descriptionPanel.localScale =
                Vector3.zero;

            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.alpha =
                    0f;

                _descriptionCanvasGroup
                    .DOFade(
                        1f,
                        DescriptionShowDuration * 0.9f)
                    .SetEase(
                        DescriptionShowEase)
                    .SetLink(
                        _descriptionPanel.gameObject);
            }

            _descriptionPanel
                .DOScale(
                    _descriptionPanelBaseScale,
                    DescriptionShowDuration)
                .SetEase(
                    DescriptionShowEase)
                .SetLink(
                    _descriptionPanel.gameObject);
        }

        private void UpdateDescriptionText()
        {
            if (_descriptionText == null)
            {
                return;
            }

            _descriptionText.text =
                _playerDescriptionLine;

            _descriptionText.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    1f);

            _descriptionText.ForceMeshUpdate(
                true);
        }

        private void CenterOnThisSkill()
        {
            if (_skillScroll == null)
            {
                _skillScroll =
                    FindFirstObjectByType<InfinitySkillScroll>();
            }

            if (_skillScroll == null)
            {
                return;
            }

            _skillScroll.ScrollToItem(
                _zeroBasedSlotIndex);
        }

        private void HideDescriptionPanel()
        {
            if (_descriptionPanel == null ||
                !_isDescriptionPanelVisible)
            {
                return;
            }

            _descriptionPanel.DOKill(false);

            if (_descriptionCanvasGroup != null)
            {
                _descriptionCanvasGroup.DOKill(false);

                _descriptionCanvasGroup
                    .DOFade(
                        0f,
                        DescriptionHideDuration)
                    .SetEase(
                        DescriptionHideEase)
                    .SetLink(
                        _descriptionPanel.gameObject);
            }

            _descriptionPanel
                .DOScale(
                    0f,
                    DescriptionHideDuration)
                .SetEase(
                    DescriptionHideEase)
                .SetLink(
                    _descriptionPanel.gameObject)
                .OnComplete(() =>
                {
                    if (_descriptionPanel != null)
                    {
                        if (_descriptionCanvasGroup != null)
                        {
                            _descriptionCanvasGroup.alpha =
                                0f;
                        }

                        _descriptionPanel.gameObject
                            .SetActive(false);
                    }

                    _isDescriptionPanelVisible =
                        false;
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

            _descriptionPanel.localScale =
                Vector3.zero;

            _descriptionPanel.gameObject
                .SetActive(false);

            _isDescriptionPanelVisible = false;
            _descriptionPinnedByMouseSelection = false;
            _isPointerInside = false;
        }
    }

    [DisallowMultipleComponent]
    public sealed class SkillButtonSlotPointerRelay :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private SkillButtonPanelView _host;

        public void Init(
            SkillButtonPanelView host)
        {
            _host = host;
        }

        public void OnPointerEnter(
            PointerEventData eventData)
        {
            _host?.HandlePointerEnter();
        }

        public void OnPointerExit(
            PointerEventData eventData)
        {
            _host?.HandlePointerExit();
        }
    }
}