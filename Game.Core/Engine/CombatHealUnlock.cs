using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>
/// Hook for HealHp / HealHpPercent. Combat healing is unlocked for hero kits (Maria).
/// </summary>
public static class CombatHealUnlock
{
    /// <summary>When true, HealHp / HealHpPercent in combat apply HP. Default unlocked for hero kits.</summary>
    public static bool IsCombatHealingUnlocked { get; set; } = true;

    /// <summary>HP restored by a flat HealHp effect. Used by tests and by the applicator.</summary>
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
    /// Revives dead recipients when heal amount &gt; 0 (Resurrection Hymn MVP).
    /// </summary>
    public static int ApplyHealHpToRecipient(Combatant recipient, int potency)
    {
        var healAmount = ComputeHealHpAmount(potency);
        if (recipient?.Health == null || healAmount <= 0)
        {
            return 0;
        }

        if (recipient.Health.IsDead)
        {
            recipient.Health.IsDead = false;
            recipient.Health.CurrentHp = Math.Min(recipient.Health.MaxHp, healAmount);
            return recipient.Health.CurrentHp;
        }

        var hpBeforeHeal = recipient.Health.CurrentHp;
        recipient.Health.CurrentHp = Math.Min(recipient.Health.MaxHp, recipient.Health.CurrentHp + healAmount);
        return recipient.Health.CurrentHp - hpBeforeHeal;
    }

    public static int ApplyHealHpPercentToRecipient(Combatant recipient, int percentOfMaxHp)
    {
        var healAmount = ComputeHealHpPercentAmount(recipient, percentOfMaxHp);
        return ApplyHealHpToRecipient(recipient, healAmount);
    }
}
