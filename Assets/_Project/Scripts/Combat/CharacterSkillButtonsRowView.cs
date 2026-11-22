using System;
using System.Linq;
using DG.Tweening;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Carrossel de skills do herói no turno do jogador, dentro do <c>SkillsPanel</c> da HUD.
    /// Uma janela fixa de botões: o do meio mostra a skill do cursor e cada vizinho a skill seguinte/anterior,
    /// dando a volta na lista (depois da última vem a primeira). Com poucas skills a mesma skill pode aparecer
    /// dos dois lados; nunca a central.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSkillButtonsRowView : MonoBehaviour
    {
        /// <summary>
        /// 4 innates + up to 3 unlocked tree actives (one fully unlocked tree) = 7.
        /// Must stay aligned with PlayerActionBuilder / keys 1–7.
        /// </summary>
        public const int MaxVisibleSlots = 7;

        private const int MaxSideSlots = 3;
        private const int WindowSize = MaxSideSlots * 2 + 1;
        private const float RowIntroScaleFrom = 0.94f;
        private const float RowIntroDuration = 0.2f;

        private readonly SkillButtonPanelView[] _windowViews = new SkillButtonPanelView[WindowSize];
        private readonly bool[] _slotUsableScratch = new bool[MaxVisibleSlots];
        private string _combatantId = string.Empty;
        private int? _cursorSlot;
        private bool _isShowing;
        private InfinitySkillScroll _carousel;
        private SkillVisualCatalog _skillVisualCatalog;
        private RectTransform _rowRect;

        /// <summary>Herói cujas skills estão na barra; vazio fora do turno do jogador.</summary>
        public string CombatantId => _isShowing ? _combatantId : string.Empty;

        public bool IsShowing => _isShowing;

        /// <summary>Slot sob o mouse (só em modo mouse).</summary>
        public int? HoveredZeroBasedSlotIndex
        {
            get
            {
                if (!_isShowing || global::InputDeviceModeManager.IsKeyboardMode)
                {
                    return null;
                }

                for (var viewIndex = 0; viewIndex < WindowSize; viewIndex++)
                {
                    var view = _windowViews[viewIndex];
                    if (view != null && view.IsHovered)
                    {
                        return view.SlotIndex;
                    }
                }

                return null;
            }
        }

        /// <summary>Skill que a HUD descreve agora: a do mouse, senão a do cursor.</summary>
        public int? PreviewedZeroBasedSlotIndex => _isShowing ? HoveredZeroBasedSlotIndex ?? _cursorSlot : null;

        public void Build(
            GameObject skillButtonCombatPrefab,
            SkillVisualCatalog skillVisualCatalog,
            InfinitySkillScroll carouselOrNull,
            Action<int, int> onSlotClickedAtOffset)
        {
            _skillVisualCatalog = skillVisualCatalog;
            _carousel = carouselOrNull;
            _rowRect = (RectTransform)transform;
            _carousel?.Configure(_rowRect);

            for (var childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = transform.GetChild(childIndex);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            for (var viewIndex = 0; viewIndex < WindowSize; viewIndex++)
            {
                var instance = Instantiate(skillButtonCombatPrefab, transform, false);
                if (!instance.TryGetComponent<SkillButtonPanelView>(out var view))
                {
                    view = instance.AddComponent<SkillButtonPanelView>();
                }

                var offsetFromCenter = viewIndex - MaxSideSlots;
                view.Wire();
                view.Clicked += clickedView => onSlotClickedAtOffset?.Invoke(clickedView.SlotIndex, offsetFromCenter);
                _carousel?.PlaceSlot((RectTransform)instance.transform, offsetFromCenter);
                view.SetVisible(false);
                _windowViews[viewIndex] = view;
            }
        }

        public void HideAllSlots()
        {
            _isShowing = false;
            _cursorSlot = null;
            for (var viewIndex = 0; viewIndex < WindowSize; viewIndex++)
            {
                _windowViews[viewIndex]?.SetVisible(false);
            }
        }

        public void ClearHover()
        {
            for (var viewIndex = 0; viewIndex < WindowSize; viewIndex++)
            {
                _windowViews[viewIndex]?.ClearHover();
            }
        }

        /// <summary>Balança todas as cópias visíveis da skill recusada.</summary>
        public void PlayRejectFeedback(int zeroBasedSlot)
        {
            for (var viewIndex = 0; viewIndex < WindowSize; viewIndex++)
            {
                var view = _windowViews[viewIndex];
                if (view != null && view.isActiveAndEnabled && view.SlotIndex == zeroBasedSlot)
                {
                    view.PlayRejectFeedback();
                }
            }
        }

        private void OnDisable()
        {
            if (_rowRect != null)
            {
                _rowRect.DOKill(false);
                _rowRect.localScale = Vector3.one;
            }
        }

        private void PlayIntro()
        {
            if (_rowRect == null)
            {
                _rowRect = (RectTransform)transform;
            }

            _rowRect.DOKill(false);
            _rowRect.localScale = new Vector3(RowIntroScaleFrom, RowIntroScaleFrom, 1f);
            _rowRect
                .DOScale(Vector3.one, RowIntroDuration)
                .SetEase(Ease.OutBack)
                .SetLink(_rowRect.gameObject);
        }

        public void Refresh(
            BattleState battleState,
            BattleSimulator battleSimulator,
            Combatant subject,
            int cursorSlot,
            int? selectedSlot,
            Combatant selectedEnemyOrNull)
        {
            if (subject == null || string.IsNullOrEmpty(subject.Identity.Id))
            {
                HideAllSlots();
                return;
            }

            var skillIds = subject.SkillLoadout.Skills
                .Where(skillId => battleState.SkillsById.ContainsKey(skillId))
                .Take(MaxVisibleSlots)
                .ToList();
            var slotCount = skillIds.Count;
            if (slotCount == 0)
            {
                HideAllSlots();
                return;
            }

            if (!_isShowing)
            {
                // A barra reaparece no começo do turno: nenhum hover antigo sobrevive.
                ClearHover();
                PlayIntro();
            }

            _isShowing = true;
            _combatantId = subject.Identity.Id;
            _cursorSlot = Mathf.Clamp(cursorSlot, 0, slotCount - 1);

            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                _slotUsableScratch[slotIndex] = CombatSkillSlotUiEligibility.IsSlotUiInteractable(
                    battleState,
                    battleSimulator,
                    subject,
                    slotIndex,
                    selectedEnemyOrNull);
            }

            // O balão de descrição fica sempre no botão do centro: mostra a skill sob o mouse, senão a do cursor.
            var describedSlot = Mathf.Clamp(PreviewedZeroBasedSlotIndex ?? _cursorSlot.Value, 0, slotCount - 1);
            var centerDescription = battleState.SkillsById.TryGetValue(skillIds[describedSlot], out var describedSkill)
                ? CombatSkillPlayerDescriptionFormatter.BuildHudEssentialsLine(
                    describedSkill,
                    battleState,
                    subject,
                    selectedEnemyOrNull)
                : null;

            var sideSlots = Mathf.Min(
                _carousel != null ? _carousel.SideSlotCount : MaxSideSlots,
                slotCount - 1);

            for (var viewIndex = 0; viewIndex < WindowSize; viewIndex++)
            {
                var view = _windowViews[viewIndex];
                if (view == null)
                {
                    continue;
                }

                var offsetFromCenter = viewIndex - MaxSideSlots;
                if (Mathf.Abs(offsetFromCenter) > sideSlots)
                {
                    view.SetVisible(false);
                    continue;
                }

                var slotIndex = ((_cursorSlot.Value + offsetFromCenter) % slotCount + slotCount) % slotCount;
                var isCenter = offsetFromCenter == 0;
                ResolveSkillIconVisuals(skillIds[slotIndex], out var skillIcon, out var skillIconColor);

                view.SetVisible(true);
                view.Render(new SkillSlotViewState(
                    slotIndex,
                    skillIcon,
                    skillIconColor,
                    _slotUsableScratch[slotIndex],
                    isFocused: isCenter,
                    isSelected: isCenter && selectedSlot == slotIndex,
                    descriptionOrNull: isCenter ? centerDescription : null));
            }
        }

        private void ResolveSkillIconVisuals(string skillId, out Sprite skillIcon, out Color skillIconColor)
        {
            skillIcon = null;
            skillIconColor = Color.white;
            if (_skillVisualCatalog == null || string.IsNullOrEmpty(skillId))
            {
                return;
            }

            if (!_skillVisualCatalog.TryGet(skillId, out var skillVisualDefinition))
            {
                return;
            }

            skillIcon = skillVisualDefinition.icon;
            skillIconColor = skillVisualDefinition.iconColor;
        }
    }
}
