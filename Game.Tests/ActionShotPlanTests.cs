using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Presentation;

namespace Game.Tests;

public sealed class ActionShotPlanTests
{
    private static CombatEvent Event(BattleEventType type, string targetId, int damage = 0, bool isHit = true) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        BattleId = "b",
        Turn = 1,
        TimestampUtc = DateTime.UtcNow,
        EventType = type,
        ActorId = "hero",
        TargetId = targetId,
        DamageAmount = damage,
        IsHit = isHit,
    };

    [Fact]
    public void SingleTargetAttack_FramesTheTarget()
    {
        var plan = ActionShotPlan.Build("hero", "enemy_a", new[]
        {
            Event(BattleEventType.ActionUsed, "enemy_a"),
            Event(BattleEventType.HitResolved, "enemy_a"),
            Event(BattleEventType.DamageApplied, "enemy_a", damage: 5),
        });

        Assert.True(plan.HasTargetFocus);
        Assert.Equal(new[] { "enemy_a" }, plan.AffectedCombatantIds);
        Assert.Equal(new[] { "enemy_a" }, plan.HitTakenCombatantIds);
    }

    [Fact]
    public void SelfOnlySkill_HasNoTargetFocus()
    {
        var plan = ActionShotPlan.Build("hero", "hero", new[]
        {
            Event(BattleEventType.ActionUsed, "hero"),
            Event(BattleEventType.TokenApplied, "hero"),
        });

        Assert.False(plan.HasTargetFocus);
    }

    [Fact]
    public void AttackThatAlsoBuffsTheActor_FramesActorAndTargetTogether()
    {
        var plan = ActionShotPlan.Build("hero", "enemy_a", new[]
        {
            Event(BattleEventType.HitResolved, "enemy_a"),
            Event(BattleEventType.DamageApplied, "enemy_a", damage: 3),
            Event(BattleEventType.TokenApplied, "hero"),
        });

        Assert.True(plan.HasTargetFocus);
        Assert.Equal(new[] { "enemy_a", "hero" }, plan.AffectedCombatantIds);
        Assert.Equal(new[] { "enemy_a" }, plan.HitTakenCombatantIds);
    }

    [Fact]
    public void MissedAttack_StillFramesTargetButNoHitTaken()
    {
        var plan = ActionShotPlan.Build("hero", "enemy_a", new[]
        {
            Event(BattleEventType.HitResolved, "enemy_a", isHit: false),
        });

        Assert.Equal(new[] { "enemy_a" }, plan.AffectedCombatantIds);
        Assert.Empty(plan.HitTakenCombatantIds);
    }

    [Fact]
    public void KillingBlow_ListsTheDeadCombatant()
    {
        var plan = ActionShotPlan.Build("hero", "enemy_a", new[]
        {
            Event(BattleEventType.DamageApplied, "enemy_a", damage: 9),
            Event(BattleEventType.CombatantDied, "enemy_a"),
        });

        Assert.Equal(new[] { "enemy_a" }, plan.DiedCombatantIds);
    }

    [Fact]
    public void HealWithoutEvents_StillFramesThePrimaryTarget()
    {
        var plan = ActionShotPlan.Build("hero", "ally", new[]
        {
            Event(BattleEventType.ActionUsed, "ally"),
        });

        Assert.True(plan.HasTargetFocus);
        Assert.Equal(new[] { "ally" }, plan.AffectedCombatantIds);
    }
}
