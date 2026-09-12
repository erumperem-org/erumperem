using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

/// <summary>
/// Status ticks, Destabilization explosions, ControlledInstability reflect, and flat HP loss helpers.
/// Destabilization "nearby" is every other living combatant (no radius). Unleash consumes all stacks
/// and does not damage the carrier.
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
        CombatHypnosisRules.ClearLockIfExpired(actor);
    }

    public static void ApplyEndOfTurnStatusEffects(
        BattleState state,
        Combatant actor,
        BattleCombatEventEmitter? eventEmitter)
    {
        if (actor.Health.IsDead)
        {
            return;
        }

        var regenerationStacks = actor.Tokens.GetStacks(TokenType.Regeneration);
        if (regenerationStacks > 0 && CombatHealUnlock.IsCombatHealingUnlocked)
        {
            var healed = CombatHealUnlock.ApplyHealHpToRecipient(actor, regenerationStacks);
            if (healed > 0)
            {
                state.PassiveBus.RaiseHealingDealt(state, actor, actor, skill: null, healed);
            }
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

        var burnStacks = actor.Tokens.GetStacks(TokenType.Burn);
        if (burnStacks > 0)
        {
            ApplyDirectHpLoss(
                state,
                actor,
                burnStacks,
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
            if (actor.Tokens.GetStacks(decayTokenType) <= 0)
            {
                continue;
            }

            var skipDecayChance = PassiveDataDrivenEngine.GetTokenEndOfTurnDecaySkipChance(
                state,
                actor,
                decayTokenType);
            if (skipDecayChance > 0 && state.PassiveTriggerRandom.NextDouble() < skipDecayChance)
            {
                continue;
            }

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
        actor.PassiveRuntime.ConfusedSkillIdsThisTurn.Clear();
        CombatHypnosisRules.ClearLockIfExpired(actor);
    }

    public static void ApplyControlledInstabilityReflect(
        BattleState state,
        Combatant attacker,
        Combatant defender,
        BattleCombatEventEmitter? eventEmitter,
        string skillId)
    {
        if (attacker.Identity.Faction != Faction.Enemy)
        {
            return;
        }

        var instabilityStacks = defender.Tokens.GetStacks(TokenType.ControlledInstability);
        if (instabilityStacks <= 0 || attacker.Health.IsDead)
        {
            return;
        }

        var reflectEfficiency = PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(
            state,
            defender,
            TokenType.ControlledInstability,
            attacker);
        var reflectDamage = (int)Math.Round(
            CombatStatusRules.ControlledInstabilityReflectDamagePerStack * instabilityStacks * reflectEfficiency);
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
        if (attacker.Identity.Faction != Faction.Enemy)
        {
            return;
        }

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
        BattleCombatEventEmitter? eventEmitter,
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

        var applierCombatant = state.GetAllCombatants().FirstOrDefault(combatant =>
            string.Equals(combatant.Identity.Id, actorId, StringComparison.Ordinal));
        var destabEfficiency = applierCombatant == null
            ? 1.0
            : PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(
                state,
                applierCombatant,
                TokenType.Destabilization,
                explodingCombatant);
        var explosionDamage = (int)Math.Round(
            CombatStatusRules.DestabilizationDamagePerStack * destabilizationStacks * destabEfficiency);
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
        BattleCombatEventEmitter? eventEmitter,
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
            eventEmitter?.Emit(
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
        eventEmitter?.Emit(
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
            eventEmitter?.Emit(state, BattleEventType.CombatantDied, targetId: target.Identity.Id);
            TriggerDestabilizationExplosion(state, target, eventEmitter, skillId, actorId);
        }
    }
}
