namespace Core.CharacterStats
{
    /// <summary>
    /// Base combat stats that items can modify. Extending this list also
    /// requires no change elsewhere — CharacterStatsData is a dictionary
    /// keyed by this enum, not a fixed set of fields.
    /// </summary>
    public enum StatType
    {
        Health,
        Defense,
        Critical
    }
}
