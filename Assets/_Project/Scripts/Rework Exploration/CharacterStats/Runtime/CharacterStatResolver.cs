using Erumperem.Characters;

namespace Core.CharacterStats
{
    /// <summary>
    /// Bridges <see cref="StatType"/> to the base values authored in an
    /// <see cref="AllyCharacterStatDefinition"/>. Add a case here when
    /// StatType gets a new member.
    /// </summary>
    public static class CharacterStatResolver
    {
        public static float GetBase(AllyCharacterStatDefinition definition, StatType stat)
        {
            if (definition == null) return 0f;

            switch (stat)
            {
                case StatType.Health:   return definition.MaxHitPoints;
                case StatType.Defense:  return (float)definition.DefenseChance;
                case StatType.Critical: return (float)definition.CritChance;
                default:                return 0f;
            }
        }

        /// <summary>Current value = base + saved modification.</summary>
        public static float GetEffective(AllyCharacterStatDefinition definition, CharacterStatsData saved, StatType stat)
            => GetBase(definition, stat) + (saved != null ? saved.Get(stat) : 0f);
    }
}
