using System.Text.Json.Serialization;
using Game.Core.Domain;

namespace Game.Core.Items;

/// <summary>
/// Persistent list of consumed status/thematic items, keyed by combat characterId (wulfric, buck, maria).
/// Replay onto SO baselines so combat and exploration stay aligned.
/// </summary>
public sealed class CombatItemBonusLedger
{
    public int Version { get; set; } = 1;

    public Dictionary<string, List<string>> AppliedItemIdsByCharacterId { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public void RecordAppliedItem(string characterId, string itemId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        if (!AppliedItemIdsByCharacterId.TryGetValue(characterId, out var itemIds))
        {
            itemIds = [];
            AppliedItemIdsByCharacterId[characterId] = itemIds;
        }

        itemIds.Add(itemId);
    }

    public IReadOnlyList<string> GetAppliedItemIds(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) ||
            !AppliedItemIdsByCharacterId.TryGetValue(characterId, out var itemIds))
        {
            return [];
        }

        return itemIds;
    }

    public CombatantBaseStatSnapshot ComputeModifiedStats(
        string characterId,
        CombatantBaseStatSnapshot baseline,
        IReadOnlyDictionary<string, CombatItemDefinition> itemsById)
    {
        var appliedItemIds = GetAppliedItemIds(characterId);
        if (appliedItemIds.Count == 0)
        {
            return baseline;
        }

        var itemsToReplay = new List<CombatItemDefinition>();
        foreach (var itemId in appliedItemIds)
        {
            if (itemsById.TryGetValue(itemId, out var item) && item.Kind != CombatItemKind.Utility)
            {
                itemsToReplay.Add(item);
            }
        }

        return CombatItemStatApplier.ApplyItems(baseline, itemsToReplay);
    }
}

public sealed class CombatItemBonusLedgerSaveDto
{
    public int Version { get; set; } = 1;

    [JsonPropertyName("appliedItemIdsByCharacterId")]
    public Dictionary<string, List<string>> AppliedItemIdsByCharacterId { get; set; } = new();

    public static CombatItemBonusLedgerSaveDto FromLedger(CombatItemBonusLedger ledger) =>
        new()
        {
            Version = ledger.Version,
            AppliedItemIdsByCharacterId = new Dictionary<string, List<string>>(
                ledger.AppliedItemIdsByCharacterId,
                StringComparer.OrdinalIgnoreCase),
        };

    public CombatItemBonusLedger ToLedger()
    {
        return new CombatItemBonusLedger
        {
            Version = Version <= 0 ? 1 : Version,
            AppliedItemIdsByCharacterId = AppliedItemIdsByCharacterId ??
                                          new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
        };
    }
}
