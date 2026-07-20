using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

/// <summary>Collects combat telemetry events for a single battle session.</summary>
internal sealed class BattleCombatEventEmitter
{
    private readonly CombatEventCollector _eventCollector;

    public BattleCombatEventEmitter(CombatEventCollector eventCollector)
    {
        _eventCollector = eventCollector;
    }

    public void EmitBattleStarted(BattleState state)
    {
        Emit(
            state,
            BattleEventType.BattleStarted,
            battleResult: string.Empty,
            passiveLoadoutCsv: state.GetPassiveLoadoutCsv());
    }

    public void EmitBattleEnded(BattleState state)
    {
        var winner = state.Winner?.ToString() ?? "None";
        Emit(state, BattleEventType.BattleEnded, battleResult: winner);
    }

    public void EmitCombatantDied(BattleState state, string targetCombatantId) =>
        Emit(state, BattleEventType.CombatantDied, targetId: targetCombatantId);

    public void EmitPassiveCombatNarrativeEvent(
        BattleState state,
        PassiveCombatNote note,
        string narrativeActorId,
        string narrativeTargetId,
        string contextSkillId)
    {
        Emit(
            state,
            BattleEventType.PassiveCombatNarrative,
            actorId: narrativeActorId,
            targetId: narrativeTargetId,
            skillId: contextSkillId,
            dotType: note.DotTypeName ?? string.Empty,
            tokenType: note.TokenTypeName ?? string.Empty,
            tokenDelta: note.TokenDelta,
            passiveId: note.PassiveId,
            passiveEffectKindName: note.EffectKind.ToString(),
            passiveMagnitude: note.Magnitude,
            passiveRelatedSkillId: note.RelatedSkillId ?? string.Empty,
            dotDurationTurns: note.DotDurationTurns,
            passiveAuxInt: note.HealAmount);
    }

    public void Emit(
        BattleState state,
        BattleEventType eventType,
        string actorId = "",
        string targetId = "",
        string skillId = "",
        ElementType element = ElementType.None,
        bool isHit = false,
        bool isCrit = false,
        int damageAmount = 0,
        string dotType = "",
        int dotAmount = 0,
        string tokenType = "",
        int tokenDelta = 0,
        string battleResult = "",
        string passiveLoadoutCsv = "",
        double corruptionDelta = 0,
        int? previousCorruptionTier = null,
        string passiveId = "",
        string passiveEffectKindName = "",
        double passiveMagnitude = 0,
        string passiveRelatedSkillId = "",
        int dotDurationTurns = 0,
        int passiveAuxInt = 0)
    {
        _eventCollector.Add(new CombatEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            BattleId = state.BattleId.ToString("N"),
            Turn = state.TurnNumber,
            TimestampUtc = DateTime.UtcNow,
            EventType = eventType,
            ActorId = actorId,
            TargetId = targetId,
            SkillId = skillId,
            Element = element,
            IsHit = isHit,
            IsCrit = isCrit,
            DamageAmount = damageAmount,
            DotType = dotType,
            DotAmount = dotAmount,
            TokenType = tokenType,
            TokenDelta = tokenDelta,
            CorruptionValue = state.CorruptionValue,
            CorruptionTier = state.CorruptionTier,
            CorruptionDelta = corruptionDelta,
            PreviousCorruptionTier = previousCorruptionTier,
            PassiveLoadoutCsv = passiveLoadoutCsv,
            BattleResult = battleResult,
            PassiveId = passiveId,
            PassiveEffectKindName = passiveEffectKindName,
            PassiveMagnitude = passiveMagnitude,
            PassiveRelatedSkillId = passiveRelatedSkillId,
            DotDurationTurns = dotDurationTurns,
            PassiveAuxInt = passiveAuxInt,
        });
    }
}
