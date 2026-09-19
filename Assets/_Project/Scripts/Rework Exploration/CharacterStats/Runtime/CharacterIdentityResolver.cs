namespace Core.CharacterStats
{
    /// <summary>
    /// Resolves "which character is currently active" for status-modifying
    /// items. The real lookup is not implemented yet — commented out below,
    /// ready to be swapped in once the character identity system exists.
    /// Until then, every status item applies to the same placeholder id.
    /// </summary>
    public static class CharacterIdentityResolver
    {
        private const string PlaceholderCharacterId = "PLACEHOLDER_CHARACTER_ID";

        public static string GetCurrentCharacterId()
        {
            // TODO: replace this placeholder with the real lookup once the
            // character identity system is available, e.g.:
            // return CharacterIdentity.Instance.CurrentCharacterId;

            return PlaceholderCharacterId;
        }
    }
}
