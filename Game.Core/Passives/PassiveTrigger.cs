namespace Game.Core.Passives;

/// <summary>
/// Observers subscribe to these; the simulator raises them via <see cref="CombatPassiveEventBus"/>.
/// Data-driven passives (<see cref="PassiveEffectKind"/>) run first through <see cref="PassiveRuleApplier"/> inside the bus, then external listeners.
/// </summary>
public enum PassiveTrigger
{
    TurnStarted = 0,
    TurnEnded = 1,

    BeforeOutgoingDamage = 2,
    BeforeIncomingDamage = 3,
    AfterOutgoingHitResolved = 4,

    /// <summary>Defendeu e recebeu dano (hit directo ou DOT tick). Inclui <see cref="CombatPassiveEventContext.HpPercentBefore"/> / <see cref="CombatPassiveEventContext.HpPercentAfter"/> quando aplicável.</summary>
    DamageTaken = 5,

    AfterSkillEffectsResolved = 6,

    BeforeDotTickDamage = 7,

    TokenStacksChanged = 8,

    /// <summary>Combatente eliminado (dano directo ou DOT); <see cref="CombatPassiveEventContext.Killer"/> pode ser null (DOT sem applier).</summary>
    CombatantSlain = 10,

    /// <summary>Destinatário do token é o mesmo combatente que <see cref="CombatPassiveEventContext.Other"/> (actor da skill); apenas quando <c>delta &gt; 0</c>.</summary>
    TokenAppliedToSelf = 11,

    /// <summary>Destinatário distinto do actor da skill; apenas quando <c>delta &gt; 0</c>.</summary>
    TokenAppliedToOther = 12,

    /// <summary>HP atual atravessou um limiar em <see cref="CombatPassiveEventBus.MonitoredHpPercentBarriers"/> (subida ou descida).</summary>
    HpPercentThresholdCrossed = 14,
}
