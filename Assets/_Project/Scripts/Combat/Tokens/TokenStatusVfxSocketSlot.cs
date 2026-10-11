namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Body attachment point for token status VFX. Maps 1:1 to empty GameObjects on battle prefabs
    /// (e.g. <c>Head_1_VFX_Container</c>).
    /// </summary>
    public enum TokenStatusVfxSocketSlot
    {
        Head1 = 0,
        Chest1 = 1,
        Feet1 = 2,
        Root = 3,
    }

    public static class TokenStatusVfxSocketNames
    {
        public static string Resolve(TokenStatusVfxSocketSlot socketSlot)
        {
            return socketSlot switch
            {
                TokenStatusVfxSocketSlot.Head1 => "Head_1_VFX_Container",
                TokenStatusVfxSocketSlot.Chest1 => "Chest_1_VFX_Container",
                TokenStatusVfxSocketSlot.Feet1 => "Feet_1_VFX_Container",
                TokenStatusVfxSocketSlot.Root => "Root_VFX_Container",
                _ => string.Empty,
            };
        }
    }
}
