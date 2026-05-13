namespace Game.Core.Passives;

/// <summary>
/// Short structured note when a passive changes combat (for telemetry / UI narration). Filled in <see cref="PassiveRuleApplier"/>.
/// </summary>
public readonly record struct PassiveCombatNote(
    string PassiveId,
    PassiveEffectKind EffectKind,
    double Magnitude,
    string? RelatedSkillId,
    string? DotTypeName,
    string? TokenTypeName,
    int TokenDelta,
    int HealAmount,
    int DotDurationTurns);
