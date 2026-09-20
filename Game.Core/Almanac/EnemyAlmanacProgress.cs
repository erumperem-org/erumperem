using System.Text.Json.Serialization;

namespace Game.Core.Almanac;

public sealed class EnemyAlmanacSaveDto
{
    public int Version { get; set; } = 1;

    [JsonPropertyName("revealedActiveSkillIdsByEnemyCatalogId")]
    public Dictionary<string, List<string>> RevealedActiveSkillIdsByEnemyCatalogId { get; set; } = new();
}

/// <summary>
/// Tracks which active skills each enemy type has used at least once.
/// Persist via Unity save (<c>enemy_almanac.json</c>) when available; otherwise session-only.
/// </summary>
public sealed class EnemyAlmanacProgress
{
    private readonly Dictionary<string, HashSet<string>> _revealedActiveSkillIdsByEnemyCatalogId =
        new(StringComparer.OrdinalIgnoreCase);

    public bool RecordActiveSkillUsed(string enemyCatalogId, string skillId)
    {
        if (string.IsNullOrWhiteSpace(enemyCatalogId) || string.IsNullOrWhiteSpace(skillId))
        {
            return false;
        }

        if (!_revealedActiveSkillIdsByEnemyCatalogId.TryGetValue(enemyCatalogId, out var revealedSkillIds))
        {
            revealedSkillIds = new HashSet<string>(StringComparer.Ordinal);
            _revealedActiveSkillIdsByEnemyCatalogId[enemyCatalogId] = revealedSkillIds;
        }

        return revealedSkillIds.Add(skillId);
    }

    public bool HasRevealedActiveSkill(string enemyCatalogId, string skillId)
    {
        if (string.IsNullOrWhiteSpace(enemyCatalogId) || string.IsNullOrWhiteSpace(skillId))
        {
            return false;
        }

        return _revealedActiveSkillIdsByEnemyCatalogId.TryGetValue(enemyCatalogId, out var revealedSkillIds) &&
               revealedSkillIds.Contains(skillId);
    }

    public EnemyAlmanacSaveDto ToSaveDto()
    {
        var revealedByEnemy = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var enemyEntry in _revealedActiveSkillIdsByEnemyCatalogId)
        {
            revealedByEnemy[enemyEntry.Key] = enemyEntry.Value.OrderBy(skillId => skillId, StringComparer.Ordinal).ToList();
        }

        return new EnemyAlmanacSaveDto
        {
            Version = 1,
            RevealedActiveSkillIdsByEnemyCatalogId = revealedByEnemy,
        };
    }

    public static EnemyAlmanacProgress FromSaveDto(EnemyAlmanacSaveDto? saveDto)
    {
        var progress = new EnemyAlmanacProgress();
        if (saveDto?.RevealedActiveSkillIdsByEnemyCatalogId == null)
        {
            return progress;
        }

        foreach (var enemyEntry in saveDto.RevealedActiveSkillIdsByEnemyCatalogId)
        {
            if (string.IsNullOrWhiteSpace(enemyEntry.Key) || enemyEntry.Value == null)
            {
                continue;
            }

            foreach (var skillId in enemyEntry.Value)
            {
                progress.RecordActiveSkillUsed(enemyEntry.Key, skillId);
            }
        }

        return progress;
    }
}
