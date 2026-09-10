using System.Collections.Generic;

namespace Core.CharacterStats
{
    /// <summary>
    /// In-memory view of a single character's stats, unpacked from the
    /// shared character_stats.json file by CharacterStatsRepository.
    /// Missing stats default to 0.
    /// </summary>
    public sealed class CharacterStatsData
    {
        private readonly Dictionary<StatType, float> _values;

        public CharacterStatsData(IReadOnlyDictionary<StatType, float> initialValues = null)
        {
            _values = initialValues != null
                ? new Dictionary<StatType, float>(initialValues)
                : new Dictionary<StatType, float>();
        }

        public float Get(StatType type) => _values.TryGetValue(type, out var value) ? value : 0f;

        public void Set(StatType type, float value) => _values[type] = value;

        public IReadOnlyDictionary<StatType, float> AsReadOnlyDictionary() => _values;
    }
}
