using Game.Core.Domain;

namespace Game.Core.Models;

/// <summary>
/// Estado volátil de passivas durante uma batalha (flags, não persiste fora do combate).
/// </summary>
public sealed class PassiveRuntimeState
{
    /// <summary>Ímpeto: após a skill pré-requisito acertar, a skill seguinte ganha bónus de dano causado.</summary>
    public bool ImpetoCleaveBonusPending { get; set; }

    /// <summary>Bits 1=75%, 2=50%, 4=25% — tiers de invocação já consumidos nesta batalha.</summary>
    public int HpTierSummonFlagsConsumed { get; set; }

    /// <summary>
    /// True when this combatant started the turn with Confusion stacks.
    /// Per-skill swaps live in <see cref="ConfusedSkillIdsThisTurn"/>.
    /// </summary>
    public bool ConfusionActiveThisTurn { get; set; }

    /// <summary>Skill ids whose targeting is swapped for the rest of this turn (Confusion spec).</summary>
    public HashSet<string> ConfusedSkillIdsThisTurn { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// After resolving a skill with ChanceToNotEndTurn / BonusAction, the actor may act again
    /// without advancing initiative (Unity turn driver + headless Simulate).
    /// </summary>
    public bool ShouldRetainTurnForBonusAction { get; set; }

    /// <summary>Last skill this combatant fully resolved; used by Hypnosis lock.</summary>
    public string? LastResolvedSkillId { get; set; }

    /// <summary>When Hypnosis is present, only this skill id is usable.</summary>
    public string? HypnosisLockedSkillId { get; set; }

    public Dictionary<string, int> TriggersThisBattleByPassiveId { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> TriggersThisTurnByPassiveId { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> EffectTriggersThisBattleByKey { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> EffectTriggersThisTurnByKey { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, int> HitPointsLostAccumulatorByPassiveId { get; } = new(StringComparer.Ordinal);

    public HashSet<string> FiredStatThresholdKeysThisBattle { get; } = new(StringComparer.Ordinal);

    public double BattleDamageCausedAdditive { get; set; }

    public double BattleAccuracyAdditive { get; set; }

    public double BattleCritChanceAdditive { get; set; }

    public double BattleCritDamageAdditive { get; set; }

    public double BattleDefenseChanceAdditive { get; set; }

    /// <summary>
    /// Defense chance granted until this combatant's next turn start (corruption on-hit defense).
    /// Cleared in <see cref="BeginTurn"/>.
    /// </summary>
    public double UntilOpposingSideTurnEndDefenseChanceAdditive { get; set; }

    public Dictionary<string, double> SkillDamageFlatBonusBySkillId { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, double> SkillAccuracyAdditiveBySkillId { get; } = new(StringComparer.Ordinal);

    public Dictionary<TokenType, double> TokenEfficiencyAdditiveByToken { get; } = [];

    /// <summary>Enemies this combatant has slain during the current battle (Pistoleer pistol damage per kill).</summary>
    public int EnemiesDefeatedThisBattle { get; set; }

    /// <summary>
    /// Skills queued by CastSkill passives. Preferred target is the token recipient when set;
    /// otherwise the original selected target is used. Follow-up invocation (no turn spend / BonusAction).
    /// </summary>
    public Queue<PendingCastSkillRequest> PendingCastSkills { get; } = new();

    public bool WasHpTierSummonConsumed(int tierFlag) => (HpTierSummonFlagsConsumed & tierFlag) != 0;

    public void MarkHpTierSummonConsumed(int tierFlag) => HpTierSummonFlagsConsumed |= tierFlag;

    public void BeginTurn()
    {
        TriggersThisTurnByPassiveId.Clear();
        EffectTriggersThisTurnByKey.Clear();
        ConfusedSkillIdsThisTurn.Clear();
        ConfusionActiveThisTurn = false;
        ShouldRetainTurnForBonusAction = false;
        UntilOpposingSideTurnEndDefenseChanceAdditive = 0;
    }

    public bool IsSkillConfusedThisTurn(string? skillId) =>
        !string.IsNullOrEmpty(skillId) && ConfusedSkillIdsThisTurn.Contains(skillId);
}

/// <summary>One CastSkill proc: resolve <see cref="SkillId"/> as a follow-up against <see cref="PreferredTargetCombatantId"/>.</summary>
public readonly record struct PendingCastSkillRequest(string SkillId, string? PreferredTargetCombatantId);
