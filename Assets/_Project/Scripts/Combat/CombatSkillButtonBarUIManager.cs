using System;
using System.Collections.Generic;
using Erumperem.Combat.Runtime;
using Game.Core.Models;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Erumperem.Combat
{
    /// <summary>
    /// Monta o carrossel de skills no <c>SkillsPanel</c> da HUD e decide quando ele aparece:
    /// só enquanto o jogador comanda um herói. No turno inimigo (e durante a apresentação de ações)
    /// a barra, as dicas de tecla A/D e o ícone de Espaço ficam escondidos.
    /// </summary>
    public sealed class CombatSkillButtonBarUIManager : MonoBehaviour
    {
        private const string SkillsSectionObjectName = "Skills";
        private const string BottomPanelObjectName = "BottonPanel";
        private const string FrontGroundObjectName = "FrontGround";
        private const string CenterFrameObjectName = "Image (2)";
        private static readonly string[] SkillsSectionHintNames = { "Key", "Key (1)" };
        private static readonly string[] BottomPanelHintNames = { "ConfirmIcon" };

        [FormerlySerializedAs("rowsParent")]
        [SerializeField] private Transform skillsPanelParent;
        [FormerlySerializedAs("skillButtonPanelPrefab")]
        [SerializeField] private GameObject skillButtonCombatPrefab;
        [SerializeField] private SkillVisualCatalog skillVisualCatalog;
        [SerializeField] private float worldRaycastDistance = 200f;
        [SerializeField] private CombatSkillBarSelectionController skillBarSelectionController;
        [SerializeField] private InfinitySkillScroll skillCarousel;

        [Tooltip("Dicas de input (A/D, Espaço) visíveis só no turno do jogador. Se vazio, procura por nome na HUD.")]
        [SerializeField] private GameObject[] playerTurnInputHints = Array.Empty<GameObject>();

        private CombatPrototypeController _controller;
        private CharacterSkillButtonsRowView _skillsRowView;
        private readonly CombatPointerRaycastService _pointerRaycast = new();
        private bool? _inputHintsVisible;

        public CharacterSkillButtonsRowView SkillsRowView => _skillsRowView;

        public CombatSkillBarSelectionController SelectionController => skillBarSelectionController;

        private void Awake()
        {
            TryResolveSkillsPanelParentIfMissing();
        }

        private void OnEnable()
        {
            if (global::InputDeviceModeManager.Instance != null)
            {
                global::InputDeviceModeManager.Instance.ModeChanged -= HandleInputModeChanged;
                global::InputDeviceModeManager.Instance.ModeChanged += HandleInputModeChanged;
            }
        }

        private void OnDisable()
        {
            if (global::InputDeviceModeManager.Instance != null)
            {
                global::InputDeviceModeManager.Instance.ModeChanged -= HandleInputModeChanged;
            }
        }

        private void OnDestroy()
        {
            if (skillBarSelectionController != null)
            {
                skillBarSelectionController.CursorMoved -= HandleCursorMoved;
                skillBarSelectionController.SlotRejected -= HandleSlotRejected;
            }
        }

        public void Initialize(CombatPrototypeController controller)
        {
            _controller = controller;
            _pointerRaycast.Configure(Camera.main, worldRaycastDistance);
            TryResolveSkillsPanelParentIfMissing();
            if (skillsPanelParent == null)
            {
                skillsPanelParent = transform;
            }

            if (skillBarSelectionController == null && !TryGetComponent(out skillBarSelectionController))
            {
                skillBarSelectionController = gameObject.AddComponent<CombatSkillBarSelectionController>();
            }

            skillBarSelectionController.Bind(controller);
            skillBarSelectionController.CursorMoved -= HandleCursorMoved;
            skillBarSelectionController.CursorMoved += HandleCursorMoved;
            skillBarSelectionController.SlotRejected -= HandleSlotRejected;
            skillBarSelectionController.SlotRejected += HandleSlotRejected;

            if (_controller == null || skillButtonCombatPrefab == null)
            {
                return;
            }

            if (skillCarousel == null)
            {
                skillCarousel = skillsPanelParent.GetComponentInParent<InfinitySkillScroll>();
            }

            if (!skillsPanelParent.TryGetComponent(out _skillsRowView))
            {
                _skillsRowView = skillsPanelParent.gameObject.AddComponent<CharacterSkillButtonsRowView>();
            }

            _skillsRowView.Build(
                skillButtonCombatPrefab,
                skillVisualCatalog,
                skillCarousel,
                skillBarSelectionController.ActivateSlotAtCarouselOffset);
            ResolveHudChrome();
            HideBar();
        }

        public void OnBattleEnded()
        {
            HideBar();
        }

        public void OnSkillBarSelectionCleared()
        {
            Tick();
        }

        public void SyncVisibleRowWithBattle()
        {
            Tick();
        }

        public bool TryGetHoveredLivingCombatant(out Combatant hoveredCombatant)
        {
            hoveredCombatant = TryRaycastHoveredLivingCombatant();
            return hoveredCombatant != null;
        }

        private Combatant TryRaycastHoveredLivingCombatant()
        {
            if (_controller == null)
            {
                return null;
            }

            if (!_pointerRaycast.TryRaycastCombatCapsuleTagFromInputManager(out var capsuleTag))
            {
                return null;
            }

            var hovered = _controller.FindCombatantById(capsuleTag.combatantId);
            if (hovered == null || hovered.Health.IsDead)
            {
                return null;
            }

            return hovered;
        }

        public void Tick()
        {
            if (_controller == null || _controller.BattleState == null || _skillsRowView == null)
            {
                return;
            }

            skillBarSelectionController.SyncWithTurn();
            RenderBar();
        }

        private void RenderBar()
        {
            var ownerId = skillBarSelectionController.CursorOwnerId;
            var cursorSlot = skillBarSelectionController.CursorSlot;
            var owner = string.IsNullOrEmpty(ownerId) ? null : _controller.FindCombatantById(ownerId);
            if (owner == null || !cursorSlot.HasValue)
            {
                HideBar();
                return;
            }

            _controller.GetSkillBarSelection(out var selectedSlot, out var selectedOwnerId);
            var ownerSelectedSlot = string.Equals(selectedOwnerId, ownerId, StringComparison.Ordinal)
                ? selectedSlot
                : null;

            _skillsRowView.Refresh(
                _controller.BattleState,
                _controller.BattleSimulator,
                owner,
                cursorSlot.Value,
                ownerSelectedSlot,
                _controller.CurrentSelectedEnemy);
            SetInputHintsVisible(true);
        }

        private void HideBar()
        {
            _skillsRowView?.HideAllSlots();
            SetInputHintsVisible(false);
        }

        private void HandleCursorMoved(int step)
        {
            if (_controller == null || _controller.BattleState == null || _skillsRowView == null)
            {
                return;
            }

            // Redesenha já com o cursor novo para o deslize começar do lugar certo.
            RenderBar();
            skillCarousel?.PlaySlide(step);
        }

        private void HandleSlotRejected(int zeroBasedSlot)
        {
            _skillsRowView?.PlayRejectFeedback(zeroBasedSlot);
        }

        private void HandleInputModeChanged(InputControlMode mode)
        {
            if (mode == InputControlMode.Keyboard)
            {
                // Em modo teclado o EventSystem é desligado e não manda PointerExit: o hover antigo não pode ficar preso.
                _skillsRowView?.ClearHover();
            }
        }

        private void SetInputHintsVisible(bool visible)
        {
            if (_inputHintsVisible == visible)
            {
                return;
            }

            _inputHintsVisible = visible;
            for (var hintIndex = 0; hintIndex < playerTurnInputHints.Length; hintIndex++)
            {
                var hint = playerTurnInputHints[hintIndex];
                if (hint != null)
                {
                    hint.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// Acha as dicas de tecla por nome quando não foram ligadas no Inspector e tira o raycast das molduras
        /// decorativas por cima do carrossel (a moldura central bloqueava o clique na skill do meio).
        /// </summary>
        private void ResolveHudChrome()
        {
            var skillsSection = FindAncestorNamed(skillsPanelParent, SkillsSectionObjectName);
            var bottomPanel = FindAncestorNamed(skillsPanelParent, BottomPanelObjectName);

            if (playerTurnInputHints == null || playerTurnInputHints.Length == 0)
            {
                var hints = new List<GameObject>();
                CollectDirectChildren(skillsSection, SkillsSectionHintNames, hints);
                CollectDirectChildren(bottomPanel, BottomPanelHintNames, hints);
                playerTurnInputHints = hints.ToArray();
            }

            if (skillsSection == null)
            {
                return;
            }

            var frontGround = skillsSection.Find(FrontGroundObjectName);
            DisableRaycasts(frontGround);
            DisableRaycasts(skillsSection.Find(CenterFrameObjectName));
        }

        private static void DisableRaycasts(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static void CollectDirectChildren(Transform parent, string[] childNames, List<GameObject> results)
        {
            if (parent == null)
            {
                return;
            }

            for (var nameIndex = 0; nameIndex < childNames.Length; nameIndex++)
            {
                var child = parent.Find(childNames[nameIndex]);
                if (child != null)
                {
                    results.Add(child.gameObject);
                }
            }
        }

        private static Transform FindAncestorNamed(Transform start, string ancestorName)
        {
            for (var cursor = start; cursor != null; cursor = cursor.parent)
            {
                if (string.Equals(cursor.name, ancestorName, StringComparison.Ordinal))
                {
                    return cursor;
                }
            }

            return null;
        }

        private void TryResolveSkillsPanelParentIfMissing()
        {
            if (skillsPanelParent != null)
            {
                return;
            }

            var skillsPanelAnchor = FindFirstObjectByType<CombatSkillsPanelAnchor>();
            if (skillsPanelAnchor != null)
            {
                skillsPanelParent = skillsPanelAnchor.transform;
            }
        }
    }
}
