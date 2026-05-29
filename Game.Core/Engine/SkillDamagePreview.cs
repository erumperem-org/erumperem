namespace Game.Core.Engine;

/// <summary>
/// Estimativa determinística de dano directo de uma skill contra um alvo (sem RNG de combate).
/// </summary>
public readonly struct SkillDamagePreview
{
    public int MinDamageOnHit { get; init; }
    public int MaxDamageOnHit { get; init; }
    public int MinHpAfterHit { get; init; }
    public int MaxHpAfterHit { get; init; }
    public bool IsGuaranteedKillOnHit { get; init; }
    public double HitChanceFraction { get; init; }
}
