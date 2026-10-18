namespace Core.CharacterStats
{
    /// <summary>
    /// A <see cref="StatModifierEntry"/> after its intensity symbol has been
    /// turned into a concrete number by the <see cref="IntensityLibrary"/>.
    /// This is what the math, the applicability rule and the repository consume.
    /// </summary>
    public readonly struct ResolvedStatModifier
    {
        public StatType Stat { get; }
        public StatModifierType Type { get; }

        /// <summary>Absolute: flat signed delta. Relative: signed percentage n (applied as n/100).</summary>
        public float Value { get; }

        public ResolvedStatModifier(StatType stat, StatModifierType type, float value)
        {
            Stat = stat;
            Type = type;
            Value = value;
        }
    }
}
