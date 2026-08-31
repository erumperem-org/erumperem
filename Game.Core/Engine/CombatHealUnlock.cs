using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>
/// Hook for HealHp / HealHpPercent. Combat application stays locked this phase
/// (<see cref="BattleCombatEffectApplicator"/> logs FORBIDDEN and does not call these methods).
/// Intended unlock: village Main after 3 seconds — set <see cref="IsCombatHealingUnlocked"/> when that ships.
/// </summary>
public static class CombatHealUnlock
{
    public static bool IsCombatHealingUnlocked => false;

    /// <summary>HP restored by a flat HealHp effect. Used by tests and by the future applicator hook.</summary>
    public static int ComputeHealHpAmount(int potency) => Math.Max(0, potency);

    /// <summary>HP restored by HealHpPercent (percent of MaxHp).</summary>
    public static int ComputeHealHpPercentAmount(Combatant recipient, int percentOfMaxHp)
    {
        if (recipient?.Health == null || percentOfMaxHp <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(recipient.Health.MaxHp * (percentOfMaxHp / 100.0));
    }

    /// <summary>
    /// Applies a flat heal to <paramref name="recipient"/>. Call only when <see cref="IsCombatHealingUnlocked"/> is true.
    /// </summary>
    public static int ApplyHealHpToRecipient(Combatant recipient, int potency)
    {
        var healAmount = ComputeHealHpAmount(potency);
        if (recipient?.Health == null || healAmount <= 0 || recipient.Health.IsDead)
        {
            return 0;
        }

        var hpBeforeHeal = recipient.Health.CurrentHp;
        recipient.Health.CurrentHp = Math.Min(recipient.Health.MaxHp, recipient.Health.CurrentHp + healAmount);
        return recipient.Health.CurrentHp - hpBeforeHeal;
    }
}
