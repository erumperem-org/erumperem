using System.Collections.Generic;
using Core.Exploration.Interactables.Chest;
using Services.DebugUtilities;
using Services.Loot;
using UnityEngine;

public sealed class ChestInteractable : Interactable
{
    [Header("Conteúdo")]
    [SerializeField] private LootTable _lootTable;

    [Header("Estado")]
    [SerializeField] private bool _startOpened;
    [SerializeField] private Animator _animator;

    private static readonly int OpenTrigger  = Animator.StringToHash("OpeningChest");
    private static readonly int ResetTrigger = Animator.StringToHash("Reset");

    private ILootService _lootService = new LootService();
    private IReadOnlyDictionary<IStorageable, int> _lastLoot = new Dictionary<IStorageable, int>();

    public IReadOnlyDictionary<IStorageable, int> LastLoot => _lastLoot;
    public bool IsOpened { get; private set; }

    public override bool CanInteract => !IsOpened;

    protected override void Awake()
    {
        base.Awake();
        IsOpened = _startOpened;
    }

    // ── Injeção de serviço (testes / variantes de dificuldade) ─────────────

    public void InjectLootService(ILootService service) =>
        _lootService = service ?? throw new System.ArgumentNullException(nameof(service));

    // ── Injeção de LootTable (usada pela ChestPool ao realocar) ────────────

    /// <summary>
    /// Troca a LootTable ativa em runtime. O ChestBuilder chama este método
    /// ao extrair o baú da pool, garantindo um conjunto de loot diferente
    /// a cada nova leva gerada pela ChestAreaSpawner.
    /// </summary>
    public void InjectLootTable(LootTable lootTable)
    {
        _lootTable = lootTable;
    }

    // ── Interação ───────────────────────────────────────────────────────────

    public override void ExecuteInteraction(InteractionContext context)
    {
        if (!CanInteract) return;

        IsOpened = true;
        _lastLoot = _lootService.GenerateLoot(
            _lootTable,
            new LootRequestContext(gameObject.name, transform.position));

        if (_animator != null)
        {
            _animator.ResetTrigger(ResetTrigger);
            _animator.SetTrigger(OpenTrigger);
            context.SetInputBlocked(true);
            StartCoroutine(ReenableAfterAnimation(context));
        }

        TransferToInventory(context.Inventory);
    }

    // ── Reset ────────────────────────────────────────────────────────────────

    public void ResetChest()
    {
        IsOpened  = false;
        _lastLoot = new Dictionary<IStorageable, int>();

        if (_animator != null)
        {
            _animator.ResetTrigger(OpenTrigger);
            _animator.SetTrigger(ResetTrigger);
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[{gameObject.name.ToUpper()}] resetado.", LogCategory.Interaction);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void TransferToInventory(PlayerInventorySystem inventory)
    {
        if (_lastLoot.Count == 0) return;

        if (inventory == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[{gameObject.name.ToUpper()}] Inventário não fornecido no contexto. Itens não transferidos.",
                LogCategory.Interaction);
            return;
        }

        inventory.AddItems(new Dictionary<IStorageable, int>(_lastLoot));
        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[{gameObject.name.ToUpper()}] {_lastLoot.Count} tipo(s) transferido(s).",
            LogCategory.Interaction);
    }
}


