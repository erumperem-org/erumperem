namespace Game.Core.Models;

/// <summary>
/// Estado volátil de passivas durante uma batalha (flags, não persiste fora do combate).
/// </summary>
public sealed class PassiveRuntimeState
{
    /// <summary>Ímpeto (ex. f_t3_p2): após Empurrão acertar, próximo Talho direto ganha bónus.</summary>
    public bool ImpetoCleaveBonusPending { get; set; }

    /// <summary>Bits 1=75%, 2=50%, 4=25% — tiers de invocação já consumidos nesta batalha.</summary>
    public int HpTierSummonFlagsConsumed { get; set; }

    /// <summary>
    /// Confusion MVP: when true for this turn, enemy-targeting skills may retarget randomly (33% per resolve).
    /// Set at turn start if Confusion stacks &gt; 0; cleared on EOT decay.
    /// </summary>
    public bool ConfusionActiveThisTurn { get; set; }

    /// <summary>
    /// After resolving a skill with ChanceToNotEndTurn / BonusAction, the actor may act again
    /// without advancing initiative (Unity turn driver + headless Simulate).
    /// </summary>
    public bool ShouldRetainTurnForBonusAction { get; set; }

    public bool WasHpTierSummonConsumed(int tierFlag) => (HpTierSummonFlagsConsumed & tierFlag) != 0;

    public void MarkHpTierSummonConsumed(int tierFlag) => HpTierSummonFlagsConsumed |= tierFlag;
}
