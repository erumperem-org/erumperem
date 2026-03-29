namespace Game.Core.Models;

/// <summary>
/// Estado volátil de passivas durante uma batalha (flags, não persiste fora do combate).
/// </summary>
public sealed class PassiveRuntimeState
{
    /// <summary>Ímpeto (ex. f_t3_p2): após Empurrão acertar, próximo Talho direto ganha bónus.</summary>
    public bool ImpetoCleaveBonusPending { get; set; }
}
