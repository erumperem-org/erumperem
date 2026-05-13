using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Passives;

/// <summary>
/// Payload for <see cref="CombatPassiveEventBus"/> dispatches; only fields relevant to the trigger are set.
/// </summary>
public sealed class CombatPassiveEventContext
{
    public Combatant? Self { get; init; }
    public Combatant? Other { get; init; }
    public Combatant? Killer { get; init; }
    public Combatant? Victim { get; init; }

    public SkillDefinition? Skill { get; init; }

    public int DamageAmount { get; init; }
    public bool WasCrit { get; init; }

    public TokenType? TokenType { get; init; }
    public int TokenDelta { get; init; }

    public DotInstance? Dot { get; init; }

    /// <summary>HP ratio before damage (0–1); set on <see cref="PassiveTrigger.DamageTaken"/> from direct hits and DOT ticks.</summary>
    public double? HpPercentBefore { get; init; }

    /// <summary>HP ratio after damage (0–1).</summary>
    public double? HpPercentAfter { get; init; }

    /// <summary>Limiar (0–1) em <see cref="PassiveTrigger.HpPercentThresholdCrossed"/>.</summary>
    public double? CrossedHpPercentBarrier { get; init; }
}
