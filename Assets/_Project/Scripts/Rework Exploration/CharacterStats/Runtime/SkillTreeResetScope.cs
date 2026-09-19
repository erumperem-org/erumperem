namespace Core.CharacterStats
{
    /// <summary>
    /// Distinguishes a general skill tree reset from a character-specific
    /// one, matching "Reset de Skill Tree Geral" vs. "Reset de Skill Tree —
    /// BuckWyatt/Wulfric/Matsuda" in the item design doc. This is
    /// authored data only — the effect itself is not implemented yet.
    /// </summary>
    public enum SkillTreeResetScope
    {
        General,
        CharacterSpecific
    }
}
