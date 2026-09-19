using System;
using System.Collections.Generic;

/// <summary>
/// Raiz do arquivo de save. InGameCharacterId/CompanionCharacterId são a
/// fonte da verdade de quem está em cada papel - o campo State de cada
/// CharacterSaveRecord é salvo por completude/legibilidade do arquivo, mas
/// não deve ser usado para decidir papel (evita inconsistência caso dois
/// registros digam o mesmo estado por engano).
/// </summary>
[Serializable]
public class CharacterSaveData
{
    public string InGameCharacterId;
    public string CompanionCharacterId;
    public List<CharacterSaveRecord> Records = new List<CharacterSaveRecord>();
}
