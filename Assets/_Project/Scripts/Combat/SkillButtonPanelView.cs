using System;
using DG.Tweening;
using Erumperem.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>O que um botão da barra de skills deve mostrar neste frame.</summary>
    public readonly struct SkillSlotViewState
    {
        public SkillSlotViewState(
            int slotIndex,
            Sprite icon,
            Color iconColor,
            bool isUsable,
            bool isFocused,
            bool isSelected,
            string descriptionOrNull)
        {
            SlotIndex = slotIndex;
            Icon = icon;
            IconColor = iconColor;
            IsUsable = isUsable;
            IsFocused = isFocused;
            IsSelected = isSelected;
            DescriptionOrNull = descriptionOrNull;
        }

        public int SlotIndex { get; }
        public Sprite Icon { get; }
        public Color IconColor { get; }

        /// <summary>A skill pode ser usada agora (custos, cooldown, alvo válido).</summary>
        public bool IsUsable { get; }

        /// <summary>Cursor da barra (skill central, sob a moldura). Teclado e mouse movem o mesmo cursor.</summary>
        public bool IsFocused { get; }

        /// <summary>Skill escolhida, aguardando alvo.</summary>
        public bool IsSelected { get; }

        /// <summary>Texto do balão abaixo do botão; nulo esconde o balão.</summary>
        public string DescriptionOrNull { get; }
    }

    /// <summary>
    /// Botão de um slot do carrossel de skills. Só desenha o estado recebido em <see cref="Render"/> mais o hover do mouse;
    /// não decide nada sobre seleção. Cliques são repassados para quem montou a barra.
    /// Selecionada: maior, com borda dourada pulsando. Cursor: maior (a moldura do HUD marca o centro).
    /// Hover: cresce, clareia e ganha borda branca. Indisponível: apagada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillButtonPanelView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const string DescriptionPanelName = "SkillDescriptionPanel";
        private const string DescriptionTextName = "SkillDescriptionText";
        private const string StateBorderObjectName = "SlotStateBorder";

        private const float SelectedScale = 1.2f;
        private const float FocusedScale = 1.12f;
        private const float HoveredScale = 1.07f;
        private const float HoverScaleBonus = 0.05f;
        private const float ScaleTweenDuration = 0.1f;
        private const float SelectedPulseDuration = 0.45f;
        private const float BorderThickness = 2f;
        private const float BorderGap = 2f;

        private static readonly Color SelectedBorderColor = new(1f, 0.8f, 0.25f, 1f);
        private static readonly Color HoveredBorderColor = new(1f, 1f, 1f, 0.9f);
        private static readonly Color UnusableIconTint = new(0.45f, 0.45f, 0.45f, 0.55f);

        public event Action<SkillButtonPanelView> Clicked;

        private RectTransform _rect;
        private Button _button;
        private Image _iconImage;
        private TextMeshProUGUI _hotkeyLabel;

        private CanvasGroup _borderGroup;
        private Image[] _borderEdges;

        private GameObject _descriptionPanel;
        private TextMeshProUGUI _descriptionText;
        private string _shownDescription;

        private SkillSlotViewState _state;
        private bool _hasState;
        private bool _isHovered;

        private float _appliedScale = -1f;
        private Tweener _scaleTween;
        private Tweener _pulseTween;
        private Tweener _rejectTween;

        public int SlotIndex => _state.SlotIndex;

        public bool IsHovered => _isHovered && isActiveAndEnabled;

        public void Wire()
        {
            ResolveReferences();

            if (_button != null)
            {
                // A barra é dona do visual: sem tint do Button e sem navegação do EventSystem (A/D é do combate).
                _button.transition = Selectable.Transition.None;
                _button.navigation = new Navigation { mode = Navigation.Mode.None };
                _button.interactable = true;
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(HandleClicked);
            }

            if (_iconImage != null)
            {
                _iconImage.raycastTarget = true;
                _iconImage.preserveAspect = false;
            }

            ShowDescription(null);
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                ClearHover();
                _hasState = false;
            }

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void Render(SkillSlotViewState state)
        {
            ResolveReferences();

            _state = state;
            _hasState = true;

            if (_iconImage != null && state.Icon != null)
            {
                _iconImage.sprite = state.Icon;
            }

            if (_hotkeyLabel != null)
            {
                _hotkeyLabel.text = (state.SlotIndex + 1).ToString();
            }

            ShowDescription(state.DescriptionOrNull);
            ApplyVisualState();
        }

        /// <summary>Balança o botão quando o jogador tenta usar uma skill indisponível.</summary>
        public void PlayRejectFeedback()
        {
            if (_rect == null || !isActiveAndEnabled)
            {
                return;
            }

            _rejectTween?.Kill();
            _rect.localRotation = Quaternion.identity;
            _rejectTween = _rect
                .DOPunchRotation(new Vector3(0f, 0f, 12f), 0.3f, 10, 0.5f)
                .SetLink(gameObject)
                .OnKill(() => _rect.localRotation = Quaternion.identity);
        }

        public void ClearHover()
        {
            if (!_isHovered)
            {
                return;
            }

            _isHovered = false;

            if (_hasState)
            {
                ApplyVisualState();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isHovered)
            {
                return;
            }

            _isHovered = true;

            if (_hasState)
            {
                ApplyVisualState();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearHover();
        }

        private void HandleClicked()
        {
            // Evita que o EventSystem mantenha o botão "selecionado" e receba Submit/navegação.
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            Clicked?.Invoke(this);
        }

        private void ApplyVisualState()
        {
            var isUsable = _state.IsUsable;
            var showHover = _isHovered;

            if (_iconImage != null)
            {
                var iconColor = _state.IconColor;
                if (!isUsable)
                {
                    iconColor *= UnusableIconTint;
                }
                else if (showHover)
                {
                    iconColor = Color.Lerp(iconColor, Color.white, 0.25f);
                }

                _iconImage.color = iconColor;
            }

            ApplyBorder(isUsable, showHover);
            ApplyScale(ResolveTargetScale(isUsable, showHover));
        }

        private float ResolveTargetScale(bool isUsable, bool showHover)
        {
            var hoverBonus = showHover && isUsable ? HoverScaleBonus : 0f;
            if (_state.IsSelected)
            {
                return SelectedScale + hoverBonus;
            }

            if (_state.IsFocused)
            {
                return FocusedScale + hoverBonus;
            }

            return showHover && isUsable ? HoveredScale : 1f;
        }

        private void ApplyBorder(bool isUsable, bool showHover)
        {
            if (_borderGroup == null)
            {
                return;
            }

            if (_state.IsSelected)
            {
                SetBorderColor(SelectedBorderColor);
                StartSelectedPulse();
                return;
            }

            StopSelectedPulse();
            var showHoverBorder = showHover && isUsable;
            if (showHoverBorder)
            {
                SetBorderColor(HoveredBorderColor);
            }

            _borderGroup.alpha = showHoverBorder ? 1f : 0f;
        }

        private void SetBorderColor(Color color)
        {
            for (var edgeIndex = 0; edgeIndex < _borderEdges.Length; edgeIndex++)
            {
                _borderEdges[edgeIndex].color = color;
            }
        }

        private void StartSelectedPulse()
        {
            if (_pulseTween != null && _pulseTween.IsActive())
            {
                return;
            }

            _borderGroup.alpha = 1f;
            _pulseTween = _borderGroup
                .DOFade(0.35f, SelectedPulseDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }

        private void StopSelectedPulse()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
        }

        private void ApplyScale(float targetScale)
        {
            if (_rect == null || Mathf.Approximately(_appliedScale, targetScale))
            {
                return;
            }

            var isFirstApply = _appliedScale < 0f;
            _appliedScale = targetScale;
            _scaleTween?.Kill();

            if (isFirstApply)
            {
                _rect.localScale = Vector3.one * targetScale;
                return;
            }

            _scaleTween = _rect
                .DOScale(targetScale, ScaleTweenDuration)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject);
        }

        private void ShowDescription(string descriptionOrNull)
        {
            if (_descriptionPanel == null)
            {
                return;
            }

            var hasDescription = !string.IsNullOrEmpty(descriptionOrNull);
            if (_descriptionPanel.activeSelf != hasDescription)
            {
                _descriptionPanel.SetActive(hasDescription);
            }

            if (!hasDescription || _descriptionText == null ||
                string.Equals(_shownDescription, descriptionOrNull, StringComparison.Ordinal))
            {
                return;
            }

            _shownDescription = descriptionOrNull;
            _descriptionText.text = PlayerFacingText.PresentForUi(descriptionOrNull);
            _descriptionText.color = Color.white;
        }

        private void ResolveReferences()
        {
            if (_rect == null)
            {
                _rect = transform as RectTransform;
            }

            if (_button == null)
            {
                if (!TryGetComponent(out _button))
                {
                    _button = GetComponentInChildren<Button>(true);
                }
            }

            if (_iconImage == null)
            {
                _iconImage = _button != null ? _button.targetGraphic as Image : null;
                if (_iconImage == null)
                {
                    TryGetComponent(out _iconImage);
                }
            }

            if (_hotkeyLabel == null)
            {
                var hotkeyTransform = transform.Find("SkillButtonNumberText");
                _hotkeyLabel = hotkeyTransform != null ? hotkeyTransform.GetComponent<TextMeshProUGUI>() : null;
            }

            if (_descriptionPanel == null)
            {
                var panelTransform = transform.Find(DescriptionPanelName);
                if (panelTransform != null)
                {
                    _descriptionPanel = panelTransform.gameObject;
                    var textTransform = panelTransform.Find(DescriptionTextName);
                    _descriptionText = textTransform != null
                        ? textTransform.GetComponent<TextMeshProUGUI>()
                        : panelTransform.GetComponentInChildren<TextMeshProUGUI>(true);

                    // Versões antigas da barra encolhiam/apagavam o balão para animar; aqui ele só liga e desliga.
                    panelTransform.localScale = Vector3.one;
                    if (panelTransform.TryGetComponent<CanvasGroup>(out var panelGroup))
                    {
                        panelGroup.alpha = 1f;
                    }

                    // Texto centralizado embaixo da skill do centro.
                    if (panelTransform.TryGetComponent<HorizontalOrVerticalLayoutGroup>(out var panelLayout))
                    {
                        panelLayout.childAlignment = TextAnchor.UpperCenter;
                    }

                    if (_descriptionText != null)
                    {
                        _descriptionText.alignment = TextAlignmentOptions.Top;
                    }
                }
            }

            if (_borderGroup == null && _rect != null)
            {
                CreateStateBorder();
            }
        }

        /// <summary>
        /// Borda retangular fina por fora do ícone (quatro barras). Não usa <see cref="Outline"/>,
        /// que redesenha o sprite deslocado e duplica a imagem do ícone.
        /// </summary>
        private void CreateStateBorder()
        {
            var borderObject = new GameObject(StateBorderObjectName, typeof(RectTransform), typeof(CanvasGroup));
            var borderRect = (RectTransform)borderObject.transform;
            borderRect.SetParent(_rect, false);
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-(BorderGap + BorderThickness), -(BorderGap + BorderThickness));
            borderRect.offsetMax = new Vector2(BorderGap + BorderThickness, BorderGap + BorderThickness);

            _borderGroup = borderObject.GetComponent<CanvasGroup>();
            _borderGroup.alpha = 0f;
            _borderGroup.interactable = false;
            _borderGroup.blocksRaycasts = false;

            _borderEdges = new[]
            {
                CreateBorderEdge(borderRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -BorderThickness), Vector2.zero),
                CreateBorderEdge(borderRect, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, BorderThickness)),
                CreateBorderEdge(borderRect, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(BorderThickness, 0f)),
                CreateBorderEdge(borderRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-BorderThickness, 0f), Vector2.zero),
            };
        }

        private static Image CreateBorderEdge(
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var edgeObject = new GameObject("Edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var edgeRect = (RectTransform)edgeObject.transform;
            edgeRect.SetParent(parent, false);
            edgeRect.anchorMin = anchorMin;
            edgeRect.anchorMax = anchorMax;
            edgeRect.offsetMin = offsetMin;
            edgeRect.offsetMax = offsetMax;

            var edgeImage = edgeObject.GetComponent<Image>();
            edgeImage.raycastTarget = false;
            return edgeImage;
        }

        private void OnDisable()
        {
            _isHovered = false;
            _scaleTween?.Kill();
            _scaleTween = null;
            StopSelectedPulse();
            _rejectTween?.Kill();
            _rejectTween = null;

            if (_borderGroup != null)
            {
                _borderGroup.alpha = 0f;
            }

            if (_rect != null)
            {
                _appliedScale = -1f;
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
            }
        }
    }
}
