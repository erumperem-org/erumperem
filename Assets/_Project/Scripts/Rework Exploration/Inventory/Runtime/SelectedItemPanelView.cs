using Services.DebugUtilities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Core.Exploration.Items;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Shows details for the currently selected inventory item, with
    /// Use/Discard/Cancel actions. Holds a reference to the selected item
    /// and the inventory it came from. Discarding removes exactly one unit
    /// at a time (multi-item discard is a planned future addition, not yet
    /// implemented). All three buttons hide together after any of them is
    /// clicked.
    /// </summary>
    public sealed class SelectedItemPanelView : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private GameObject _root;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;

        [Header("Actions")]
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _discardButton;
        [SerializeField] private Button _cancelButton;

        private IIITem _selectedItem;
        private InventorySystem _sourceInventory;

        public IIITem SelectedItem => _selectedItem;

        private void Awake()
        {
            _useButton?.onClick.AddListener(HandleUse);
            _discardButton?.onClick.AddListener(HandleDiscard);
            _cancelButton?.onClick.AddListener(HandleCancel);
        }

        private void OnDestroy()
        {
            _useButton?.onClick.RemoveListener(HandleUse);
            _discardButton?.onClick.RemoveListener(HandleDiscard);
            _cancelButton?.onClick.RemoveListener(HandleCancel);
        }

        private void Start() => Hide();

        /// <summary>Displays the given item's details and reveals the action buttons.</summary>
        public void Show(IIITem item, InventorySystem sourceInventory)
        {
            _selectedItem = item;
            _sourceInventory = sourceInventory;

            if (_icon != null) _icon.sprite = item?.Sprite;
            if (_titleText != null) _titleText.text = item?.DisplayName ?? string.Empty;
            if (_descriptionText != null) _descriptionText.text = item?.Description ?? string.Empty;

            SetVisible(true);
        }

        /// <summary>Hides the panel and clears the current selection without acting on the inventory.</summary>
        public void Hide() => ClearSelection();

        private void HandleUse()
        {
            if (_selectedItem == null || _sourceInventory == null)
            {
                Log(LogLevel.Warning, "Use clicked with no valid selection — ignored.");
                ClearSelection();
                return;
            }

            _selectedItem.ExecuteItemEffect();
            _sourceInventory.TryRemoveItem(_selectedItem, 1);

            ClearSelection();
        }

        private void HandleDiscard()
        {
            if (_selectedItem == null || _sourceInventory == null)
            {
                Log(LogLevel.Warning, "Discard clicked with no valid selection — ignored.");
                ClearSelection();
                return;
            }

            // Only one unit at a time by design — multi-item discard is a planned future addition.
            _sourceInventory.TryRemoveItem(_selectedItem, 1);

            ClearSelection();
        }

        private void HandleCancel() => ClearSelection();

        private void ClearSelection()
        {
            _selectedItem = null;
            _sourceInventory = null;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);

            if (_useButton != null) _useButton.gameObject.SetActive(visible);
            if (_discardButton != null) _discardButton.gameObject.SetActive(visible);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(visible);
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[SelectedItemPanelView:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}