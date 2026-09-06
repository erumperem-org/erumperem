using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

/// <summary>
/// Status ticks, Destabilization explosions, ControlledInstability reflect, and flat HP loss helpers.
/// </summary>
internal static class BattleCombatStatusTicker
{
    public static void ApplyTurnStartStatusEffects(
        BattleState state,
        Combatant actor,
        BattleCombatEventEmitter eventEmitter)
    {
        actor.PassiveRuntime.ConfusionActiveThisTurn = actor.Tokens.GetStacks(TokenType.Confusion) > 0;
        actor.PassiveRuntime.ShouldRetainTurnForBonusAction = false;
    }

    public static void ApplyEndOfTurnStatusEffects(
        BattleState state,
        Combatant actor,
        BattleCombatEventEmitter eventEmitter)
    {
        if (actor.Health.IsDead)
        {
            return;
        }

        var regenerationStacks = actor.Tokens.GetStacks(TokenType.Regeneration);
        if (regenerationStacks > 0 && CombatHealUnlock.IsCombatHealingUnlocked)
        {
            CombatHealUnlock.ApplyHealHpToRecipient(actor, regenerationStacks);
        }

        var bleedingStacks = actor.Tokens.GetStacks(TokenType.Bleeding);
        if (bleedingStacks > 0)
        {
            var bleedingDamage = (int)Math.Floor(
                actor.Health.MaxHp * CombatStatusRules.BleedingMaxHpDamageFractionPerStack * bleedingStacks);
            if (bleedingDamage > 0)
            {
                ApplyDirectHpLoss(
                    state,
                    actor,
                    bleedingDamage,
                    eventEmitter,
                    actor.Identity.Id,
                    skillId: string.Empty,
                    markDeath: true);
            }
        }

        if (actor.Health.IsDead)
        {
            return;
        }

        var corrosionStacks = actor.Tokens.GetStacks(TokenType.Corrosion);
        if (corrosionStacks > 0)
        {
            ApplyDirectHpLoss(
                state,
                actor,
                CombatStatusRules.CorrosionEndOfTurnDamage,
                eventEmitter,
                actor.Identity.Id,
                skillId: string.Empty,
                markDeath: true);
        }

        if (actor.Health.IsDead)
        {
            return;
        }

        foreach (var decayTokenType in CombatStatusRules.EndOfTurnDecayTokens)
        {
            if (actor.Tokens.ConsumeOne(decayTokenType))
            {
                state.PassiveBus.RaiseTokenStacksChanged(
                    state,
                    actor,
                    actor,
                    skill: null,
                    decayTokenType,
                    delta: -1);
            }
        }

        actor.PassiveRuntime.ConfusionActiveThisTurn = false;
    }

    public static void ApplyControlledInstabilityReflect(
        BattleState state,
        Combatant attacker,
        Combatant defender,
        BattleCombatEventEmitter eventEmitter,
        string skillId)
    {
        var instabilityStacks = defender.Tokens.GetStacks(TokenType.ControlledInstability);
        if (instabilityStacks <= 0 || attacker.Health.IsDead)
        {
            return;
        }

        var reflectDamage = CombatStatusRules.ControlledInstabilityReflectDamagePerStack * instabilityStacks;
        ApplyDirectHpLoss(
            state,
            attacker,
            reflectDamage,
            eventEmitter,
            defender.Identity.Id,
            skillId,
            markDeath: true);
    }

    public static void ConsumeTauntOnBeingHit(BattleState state, Combatant defender, Combatant attacker)
    {
        if (defender.Tokens.GetStacks(TokenType.Taunt) <= 0)
        {
            return;
        }

        if (defender.Tokens.ConsumeOne(TokenType.Taunt))
        {
            state.PassiveBus.RaiseTokenStacksChanged(
                state,
                attacker,
                defender,
                skill: null,
                TokenType.Taunt,
                delta: -1);
        }
    }

    public static void TriggerDestabilizationExplosion(
        BattleState state,
        Combatant explodingCombatant,
        BattleCombatEventEmitter eventEmitter,
        string skillId,
        string actorId)
    {
        var destabilizationStacks = explodingCombatant.Tokens.ConsumeAllStacks(TokenType.Destabilization);
        if (destabilizationStacks <= 0)
        {
            return;
        }

        state.PassiveBus.RaiseTokenStacksChanged(
            state,
            explodingCombatant,
            explodingCombatant,
            skill: null,
            TokenType.Destabilization,
            delta: -destabilizationStacks);

        var explosionDamage = CombatStatusRules.DestabilizationDamagePerStack * destabilizationStacks;
        foreach (var otherCombatant in state.GetAllCombatants())
        {
            if (otherCombatant.Health.IsDead)
            {
                continue;
            }

            if (string.Equals(
                    otherCombatant.Identity.Id,
                    explodingCombatant.Identity.Id,
                    StringComparison.Ordinal))
            {
                continue;
            }

            ApplyDirectHpLoss(
                state,
                otherCombatant,
                explosionDamage,
                eventEmitter,
                actorId,
                skillId,
                markDeath: true);
        }
    }

    public static void ApplyDirectHpLoss(
        BattleState state,
        Combatant target,
        int damage,
        BattleCombatEventEmitter eventEmitter,
        string actorId,
        string skillId,
        bool markDeath)
    {
        if (damage <= 0 || target.Health.IsDead)
        {
            return;
        }

        if (state.AlliesHaveInfiniteHealth && target.Identity.Faction == Faction.Player)
        {
            eventEmitter.Emit(
                state,
                BattleEventType.DamageApplied,
                actorId: actorId,
                targetId: target.Identity.Id,
                skillId: skillId,
                isHit: true,
                damageAmount: 0);
            return;
        }

        target.Health.CurrentHp = Math.Max(0, target.Health.CurrentHp - damage);
        eventEmitter.Emit(
            state,
            BattleEventType.DamageApplied,
            actorId: actorId,
            targetId: target.Identity.Id,
            skillId: skillId,
            isHit: true,
            damageAmount: damage);

        if (markDeath &&
            target.Health.CurrentHp <= 0 &&
            !target.Health.IsDead)
        {
            target.Health.IsDead = true;
            eventEmitter.Emit(state, BattleEventType.CombatantDied, targetId: target.Identity.Id);
            TriggerDestabilizationExplosion(state, target, eventEmitter, skillId, actorId);
        }
    }
}
