using System;
using Core.CharacterStats;
using Erumperem.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Inventory.UI.ItemUse
{
    /// <summary>
    /// One "use it on THIS character" button. Shows the character and, when
    /// the item can't be applied to it, a disabled state with the reason.
    /// </summary>
    public sealed class CharacterTargetButtonView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _reasonText;
        [SerializeField] private Button _button;

        private AllyCharacterStatDefinition _definition;

        public event Action<AllyCharacterStatDefinition> OnClicked;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(HandleClicked);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(HandleClicked);
        }

        public void Bind(AllyCharacterStatDefinition definition, ItemApplicability applicability)
        {
            _definition = definition;

            if (_icon != null)
            {
                _icon.sprite = definition != null ? definition.Icon : null;
                _icon.enabled = _icon.sprite != null;
            }

            if (_nameText != null)
                _nameText.text = definition != null ? definition.DisplayName : string.Empty;

            if (_reasonText != null)
                _reasonText.text = applicability.CanApply ? string.Empty : applicability.Reason;

            if (_button != null)
                _button.interactable = applicability.CanApply;
        }

        private void HandleClicked() => OnClicked?.Invoke(_definition);
    }
}
