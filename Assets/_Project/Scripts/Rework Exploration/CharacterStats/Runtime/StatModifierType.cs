namespace Core.CharacterStats
{
    /// <summary>
    /// Matches the "Absolute / Relative" legend from the item design doc.
    /// </summary>
    public enum StatModifierType
    {
        /// <summary>Adds a fixed delta to the current value: current + magnitude.</summary>
        Absolute,

        /// <summary>Applies a percentage of the current value: current + current * (magnitude / 100). A negative magnitude decreases the value.</summary>
        Relative
    }
}
