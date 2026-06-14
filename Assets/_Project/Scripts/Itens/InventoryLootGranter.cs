using System.Collections.Generic;
using System;
using Core.Exploration.Interactables.Chest;
using Services.DebugUtilities;
using Services.Loot;
using UnityEngine;

/// <summary>
/// Realiza o sorteio via <see cref="LootTable"/>, adiciona os itens ao
/// <see cref="PlayerInventorySystem"/> e persiste imediatamente em disco
/// via <see cref="PlayerInventorySaveSystem"/>.
///
/// Expõe apenas <see cref="RollLoot"/> como ponto de entrada.
/// Dispara <see cref="OnLootGranted"/> com os itens sorteados antes de salvar.
///
/// Uso:
/// <code>
///     granter.RollLoot();
/// </code>
/// </summary>
public sealed class InventoryLootGranter : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayerInventorySystem     _inventory;
    [SerializeField] private PlayerInventorySaveSystem _saveSystem;

    [Tooltip("LootTable que define os itens, pesos e capacidade do sorteio.")]
    [SerializeField] private LootTable _lootTable;

    // ── Eventos ───────────────────────────────────────────────────────────

    /// <summary>
    /// Disparado após o sorteio, antes de salvar.
    /// Recebe o dicionário de IStorageable → quantidade sorteada.
    /// </summary>
    public event Action<IReadOnlyDictionary<IStorageable, int>> OnLootGranted;

    // ── Serviço de loot ───────────────────────────────────────────────────

    private readonly ILootService _lootService = new LootService();

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Sorteia itens da <see cref="LootTable"/>, adiciona ao inventário,
    /// dispara <see cref="OnLootGranted"/> e salva em disco.
    /// </summary>
    public void RollLoot()
    {
        if (!Validate()) return;

        var context = new LootRequestContext(gameObject.name, transform.position);
        IReadOnlyDictionary<IStorageable, int> loot = _lootService.GenerateLoot(_lootTable, context);

        if (loot == null || loot.Count == 0)
        {
            Log(LogLevel.Warning, "Sorteio não gerou nenhum item.");
            return;
        }

        // Converte para Dictionary mutável que AddItems espera
        var toAdd = new Dictionary<IStorageable, int>(loot);
        _inventory.AddItems(toAdd);

        // Notifica ouvintes com o resultado antes de persistir
        OnLootGranted?.Invoke(loot);

        // Persiste imediatamente
        _saveSystem.SaveAsync();

        Log(LogLevel.Debug, $"Sorteio concluído: {loot.Count} tipo(s) adicionado(s) e save gravado.");
    }

    // ── Validação ─────────────────────────────────────────────────────────

    private bool Validate()
    {
        if (_inventory == null)
        {
            Log(LogLevel.Error, "PlayerInventorySystem não atribuído.");
            return false;
        }

        if (_saveSystem == null)
        {
            Log(LogLevel.Error, "PlayerInventorySaveSystem não atribuído.");
            return false;
        }

        if (_lootTable == null)
        {
            Log(LogLevel.Error, "LootTable não atribuída.");
            return false;
        }

        return true;
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private void Log(LogLevel level, string msg) =>
        LoggerService.PrintLogMessage(level, $"[InventoryLootGranter:{gameObject.name}] {msg}", LogCategory.Inventory);
}