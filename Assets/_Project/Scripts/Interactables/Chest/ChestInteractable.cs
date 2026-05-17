using System.Collections.Generic;
using Core.Exploration.Interactables.Chest;
using Player;
using Services.DebugUtilities;
using Services.Loot;
using UnityEngine;

public sealed class ChestInteractable : Interactable
{
    [Header("Conteúdo")]
    [Tooltip("Tabela de loot que define os itens e chances deste baú.")]
    [SerializeField] private LootTable lootTable;

    [Header("Estado")]
    [SerializeField] private bool opened;
    [SerializeField] private Animator animator;

    [Header("Referência ao inventário")]
    public PlayerInventorySystem inventory;

    private static readonly int OpenTrigger = Animator.StringToHash("Open");
    private static readonly int ResetTrigger = Animator.StringToHash("Reset");

    private ILootService _lootService = new LootService();
    private IReadOnlyDictionary<IStorageable, int> _items = new Dictionary<IStorageable, int>();

    public IReadOnlyDictionary<IStorageable, int> Items => _items;
    public override bool CanInteract => !opened;

    public void InjectLootService(ILootService service) =>
        _lootService = service ?? throw new System.ArgumentNullException(nameof(service));

    public override void ExecuteInteraction(PlayerMovementController controller)
    {
        if (!CanInteract) return;

        opened = true;
        _items = _lootService.GenerateLoot(lootTable, new LootRequestContext(gameObject.name, transform.position));

        if (animator != null)
        {
            animator.ResetTrigger(ResetTrigger);
            animator.SetTrigger(OpenTrigger);
            controller._inputReader.IsPlayerInteracting = true;
            StartCoroutine(ReenableAfterAnimation(controller));
        }

        TransferToInventory();
    }

    public void ResetChest()
    {
        opened = false;
        _items = new Dictionary<IStorageable, int>();

        if (animator != null)
        {
            animator.ResetTrigger(OpenTrigger);
            animator.SetTrigger(ResetTrigger);
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[{gameObject.name.ToUpper()}] resetado.", LogCategory.Interaction);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void TransferToInventory()
    {
        if (_items.Count == 0) return;
        if (inventory == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[{gameObject.name.ToUpper()}] PlayerInventorySystem não encontrado. Itens não transferidos.",
                LogCategory.Interaction);
            return;
        }

        inventory.AddItems(new Dictionary<IStorageable, int>(_items));

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[{gameObject.name.ToUpper()}] {_items.Count} tipo(s) transferido(s) ao inventário.",
            LogCategory.Interaction);
    }
}
