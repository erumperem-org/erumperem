using System.Collections.Generic;
using Erumperem.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Inventory.UI.ItemUse
{
    /// <summary>
    /// Visual layer of the item-use flow. Listens to ItemUseFlowController
    /// and shows/hides the matching sections; it holds no flow logic.
    ///
    /// Layout expected:
    ///  - Root panel (hidden while Idle) with item icon/name and a step label.
    ///  - Target panel: a container where one CharacterTargetButtonView per ally is spawned.
    ///  - Confirmation panel: summary text, Confirm button, "Change character" button.
    ///  - Cancel button, always available while the flow is active.
    /// </summary>
    public sealed class ItemUseFlowView : MonoBehaviour
    {
        [SerializeField] private ItemUseFlowController _controller;
        [SerializeField] private GameObject _root;

        [Header("Item")]
        [SerializeField] private Image _itemIcon;
        [SerializeField] private TMP_Text _itemNameText;
        [SerializeField] private TMP_Text _stepText;

        [Header("Target selection")]
        [SerializeField] private GameObject _targetPanel;
        [SerializeField] private Transform _targetButtonContainer;
        [SerializeField] private CharacterTargetButtonView _targetButtonPrefab;
        [Tooltip("Characters offered as targets, in display order.")]
        [SerializeField] private List<AllyCharacterStatDefinition> _allies = new();

        [Header("Confirmation")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private TMP_Text _confirmText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _changeTargetButton;

        [Header("Common")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _feedbackText;

        private void OnEnable()
        {
            if (_controller == null) return;

            _controller.OnStepChanged += HandleStepChanged;
            _controller.OnRejected += HandleRejected;
            _controller.OnCompleted += HandleCompleted;

            if (_confirmButton != null) _confirmButton.onClick.AddListener(_controller.Confirm);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(_controller.Cancel);
            if (_changeTargetButton != null) _changeTargetButton.onClick.AddListener(_controller.ChangeTarget);

            HandleStepChanged(_controller.Step, _controller.Context);
        }

        private void OnDisable()
        {
            if (_controller == null) return;

            _controller.OnStepChanged -= HandleStepChanged;
            _controller.OnRejected -= HandleRejected;
            _controller.OnCompleted -= HandleCompleted;

            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(_controller.Confirm);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(_controller.Cancel);
            if (_changeTargetButton != null) _changeTargetButton.onClick.RemoveListener(_controller.ChangeTarget);
        }

        private void HandleStepChanged(ItemUseStep step, ItemUseContext context)
        {
            bool active = step != ItemUseStep.Idle;

            if (_root != null) _root.SetActive(active);
            if (_targetPanel != null) _targetPanel.SetActive(step == ItemUseStep.SelectingTarget);
            if (_confirmPanel != null) _confirmPanel.SetActive(step == ItemUseStep.AwaitingConfirmation);
            if (_cancelButton != null) _cancelButton.interactable = active;
            if (_confirmButton != null) _confirmButton.interactable = step == ItemUseStep.AwaitingConfirmation;
            if (_changeTargetButton != null) _changeTargetButton.gameObject.SetActive(active && context.RequiresTarget);

            if (!active)
            {
                ClearTargetButtons();
                return;
            }

            SetFeedback(string.Empty);
            RefreshItemInfo(context);

            switch (step)
            {
                case ItemUseStep.SelectingTarget:
                    SetStepText("Choose a character");
                    RebuildTargetButtons(context);
                    break;

                case ItemUseStep.AwaitingConfirmation:
                    SetStepText("Confirm");
                    RefreshConfirmText(context);
                    break;
            }
        }

        private void RefreshItemInfo(ItemUseContext context)
        {
            if (_itemIcon != null)
            {
                _itemIcon.sprite = context.Item?.Sprite;
                _itemIcon.enabled = _itemIcon.sprite != null;
            }

            if (_itemNameText != null)
                _itemNameText.text = context.Item != null ? context.Item.DisplayName : string.Empty;
        }

        private void RefreshConfirmText(ItemUseContext context)
        {
            if (_confirmText == null) return;

            string itemName = context.Item != null ? context.Item.DisplayName : "?";
            _confirmText.text = context.HasTarget
                ? $"Use {itemName} on {context.Target.DisplayName}?"
                : $"Use {itemName}?";
        }

        private void RebuildTargetButtons(ItemUseContext context)
        {
            ClearTargetButtons();
            if (_targetButtonPrefab == null || _targetButtonContainer == null) return;

            var targeted = context.TargetedItem;

            foreach (var ally in _allies)
            {
                if (ally == null) continue;

                var button = Instantiate(_targetButtonPrefab, _targetButtonContainer);
                button.Bind(ally, targeted.CanApplyTo(ally));
                button.OnClicked += HandleTargetClicked;
            }
        }

        private void ClearTargetButtons()
        {
            if (_targetButtonContainer == null) return;

            for (int i = _targetButtonContainer.childCount - 1; i >= 0; i--)
                Destroy(_targetButtonContainer.GetChild(i).gameObject);
        }

        private void HandleTargetClicked(AllyCharacterStatDefinition ally) => _controller.SelectTarget(ally);

        private void HandleRejected(string reason) => SetFeedback(reason);

        private void HandleCompleted(ItemUseResult result) => SetFeedback(string.Empty);

        private void SetStepText(string text)
        {
            if (_stepText != null) _stepText.text = text;
        }

        private void SetFeedback(string text)
        {
            if (_feedbackText != null) _feedbackText.text = text;
        }
    }
}
