namespace Core.CharacterStats
{
    /// <summary>
    /// The one place that defines how a modifier changes a stat.
    ///
    /// Persistence model: the save file stores, per character and stat, a
    /// DELTA over the base value defined in AllyCharacterStatDefinition.
    ///   effective (current) value = base + savedDelta
    ///
    /// Relative modifiers are a percentage of the CURRENT effective value
    /// (base + savedDelta), so stacking is cumulative and consistent.
    /// </summary>
    public static class StatModifierMath
    {
        /// <summary>The amount to ADD to the saved delta for this modifier.</summary>
        public static float ComputeDeltaChange(float baseValue, float savedDelta, in ResolvedStatModifier modifier)
        {
            switch (modifier.Type)
            {
                case StatModifierType.Absolute:
                    return modifier.Value;

                case StatModifierType.Relative:
                    return (baseValue + savedDelta) * (modifier.Value / 100f);

                default:
                    return 0f;
            }
        }
    }
}
