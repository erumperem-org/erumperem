using Core.CharacterStats;

/// <summary>
/// PLACEHOLDER para o futuro ScriptableObject de stats base dos
/// personagens. Quando esse SO existir, é aqui (e só aqui) que a leitura
/// real deve entrar — troque o corpo deste método por uma consulta ao SO
/// pelo characterId, sem espalhar essa mudança por outros arquivos.
///
/// Não reaproveita BarConfigSO.MaxDefault de propósito: aquele campo é
/// configuração de exibição de UI da barra, conceitualmente diferente de
/// um stat base de gameplay, mesmo que hoje os dois sejam só um float.
/// </summary>
public static class CharacterBaseStatsSource
{
    // TODO: substituir esta tabela fixa por uma consulta real ao SO de
    // stats base, indexada por characterId, quando esse sistema existir.
    public static float GetBaseStat(string characterId, StatType statType)
    {
        return 100f;
    }
}