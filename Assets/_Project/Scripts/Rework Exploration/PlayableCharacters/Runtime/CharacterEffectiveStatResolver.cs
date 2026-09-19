using Core.CharacterStats;

/// <summary>
/// Combina o stat base de um personagem (fonte placeholder por enquanto,
/// ver CharacterBaseStatsSource) com o modificador acumulado por itens
/// (armazenado via CharacterStatsRepository, pacote
/// CharacterStatsItemsSystem). Genérico por StatType - hoje só Health é
/// consumido (ver PlayableCharacterController), mas fica pronto para
/// Defense/Critical quando necessário.
///
/// Ignora CharacterIdentityResolver de propósito: aquele resolver hoje
/// sempre devolve o mesmo id placeholder para todo mundo, o que colapsaria
/// todos os PlayableCharacters no mesmo registro de stats. Usa
/// PlayableCharacters.CharacterId diretamente - hoje definido manualmente no
/// Inspector de cada personagem; no futuro, todos esses ids virão do SO de
/// identidade/stats base em vez de serem digitados à mão.
/// </summary>
public static class CharacterEffectiveStatResolver
{
    public static float GetEffectiveStat(string characterId, StatType statType)
    {
        float baseValue = CharacterBaseStatsSource.GetBaseStat(characterId, statType);

        CharacterStatsData stats = CharacterStatsRepository.GetStatsForCharacter(characterId);

        // Get() já retorna 0 para stat ausente/personagem sem registro
        // ainda - não precisa de checagem de nulo/existência separada.
        float storedModifier = stats?.Get(statType) ?? 0f;

        return baseValue + storedModifier;
    }
}