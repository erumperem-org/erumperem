using Services.DebugUtilities;
using UnityEngine;
using UnityEngine.UI;
using Core.Exploration.Items;
using Core.Inventory.UI.ItemUse;

namespace Core.Inventory.UI
{
    /// <summary>
    /// Painel de ações do item selecionado para a view de dois inventários:
    /// Usar / Descartar / Transferir / Cancelar. Não exibe detalhes do item
    /// (isso é responsabilidade do tooltip de hover).
    ///
    /// Usar não executa o item aqui: entrega a seleção ao ItemUseFlowController,
    /// que cuida da escolha de personagem, da confirmação, do efeito e da
    /// remoção de UMA unidade do slot/inventário exatos.
    /// Descartar remove exatamente UMA unidade do slot selecionado.
    /// </summary>
    public sealed class TransferItemPanelView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;

        [Header("Actions")]
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _discardButton;
        [SerializeField] private Button _transferButton;
        [SerializeField] private Button _cancelButton;

        [Header("Transfer")]
        [Tooltip("Ligado: transfere a pilha inteira do slot. Desligado: uma unidade por clique.")]
        [SerializeField] private bool _transferEntireStack = true;

        [Header("Item use flow")]
        [SerializeField] private ItemUseFlowController _useFlow;

        private IIITem _selectedItem;
        private InventorySystem _source;
        private InventorySystem _target;
        private int _slotIndex = -1;

        public IIITem SelectedItem => _selectedItem;

        private void Awake()
        {
            if (_useButton != null) _useButton.onClick.AddListener(HandleUse);
            if (_discardButton != null) _discardButton.onClick.AddListener(HandleDiscard);
            if (_transferButton != null) _transferButton.onClick.AddListener(HandleTransfer);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Hide);
        }

        private void OnDestroy()
        {
            if (_useButton != null) _useButton.onClick.RemoveListener(HandleUse);
            if (_discardButton != null) _discardButton.onClick.RemoveListener(HandleDiscard);
            if (_transferButton != null) _transferButton.onClick.RemoveListener(HandleTransfer);
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Hide);
        }

        private void Start() => Hide();

        public void Show(IIITem item, InventorySystem source, int slotIndex, InventorySystem target)
        {
            _selectedItem = item;
            _source = source;
            _slotIndex = slotIndex;
            _target = target;

            RefreshTransferAvailability();
            SetVisible(true);
        }

        public void Hide() => ClearSelection();

        private void RefreshTransferAvailability()
        {
            if (_transferButton == null) return;

            _transferButton.interactable =
                _selectedItem != null
                && _target != null
                && !ReferenceEquals(_target, _source)
                && _target.CanFit(_selectedItem, 1);
        }

        private bool HasValidSelection() => _selectedItem != null && _source != null && _slotIndex >= 0;

        private void HandleUse()
        {
            if (!HasValidSelection())
            {
                Log(LogLevel.Warning, "Use clicked with no valid selection — ignored.");
                ClearSelection();
                return;
            }

            if (_useFlow == null)
            {
                Log(LogLevel.Error, "ItemUseFlowController not assigned — item NOT used.");
                return; // mantém o painel aberto; o item não é consumido
            }

            // O fluxo escolhe o personagem (se precisar), confirma, executa o efeito
            // e remove 1 unidade deste slot/inventário exatos.
            _useFlow.BeginWithItem(_source, _slotIndex, _selectedItem);
            ClearSelection();
        }

        private void HandleDiscard()
        {
            if (!HasValidSelection())
            {
                Log(LogLevel.Warning, "Discard clicked with no valid selection — ignored.");
                ClearSelection();
                return;
            }

            _source.TryRemoveFromSlot(_slotIndex, _selectedItem, 1);
            ClearSelection();
        }

        private void HandleTransfer()
        {
            if (!HasValidSelection() || _target == null || ReferenceEquals(_target, _source))
            {
                Log(LogLevel.Warning, "Transfer clicked with no valid selection/target — ignored.");
                ClearSelection();
                return;
            }

            if (!_source.TryGetSlot(_slotIndex, out var slotItem, out int slotQuantity)
                || !Equals(slotItem.StorageableId, _selectedItem.StorageableId))
            {
                Log(LogLevel.Warning, "Selected slot changed before transfer — ignored.");
                ClearSelection();
                return;
            }

            int amount = _transferEntireStack ? slotQuantity : 1;
            int moved = _source.TransferTo(_slotIndex, _target, amount);

            if (moved <= 0)
            {
                Log(LogLevel.Warning, $"Target inventory could not accept '{slotItem.DisplayName}'.");
                RefreshTransferAvailability();
                return; // mantém o painel aberto
            }

            if (moved < amount)
                Log(LogLevel.Warning, $"Partial transfer of '{slotItem.DisplayName}': {moved}/{amount} (target full).");

            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedItem = null;
            _source = null;
            _target = null;
            _slotIndex = -1;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);

            if (_useButton != null) _useButton.gameObject.SetActive(visible);
            if (_discardButton != null) _discardButton.gameObject.SetActive(visible);
            if (_transferButton != null) _transferButton.gameObject.SetActive(visible);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(visible);
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[TransferItemPanelView:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}