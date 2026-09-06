using Game.Core.Abstractions;
using Game.Core.Diagnostics;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

/// <summary>Applies on-hit skill effects (tokens, DOTs, push/pull) after a successful hit.</summary>
internal sealed class BattleCombatEffectApplicator
{
    private readonly IRandomSource _random;
    private readonly BattleCombatEventEmitter _eventEmitter;

    public BattleCombatEffectApplicator(IRandomSource random, BattleCombatEventEmitter eventEmitter)
    {
        _random = random;
        _eventEmitter = eventEmitter;
    }

    public void ApplyEffects(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        bool includeDefaultScopedEffects,
        bool includeNonDefaultScopedEffects)
    {
        foreach (var effect in skill.EffectsOnHit)
        {
            var isDefaultScope = effect.EffectScope == EffectScope.Default;
            if (isDefaultScope && !includeDefaultScopedEffects)
            {
                continue;
            }

            if (!isDefaultScope && !includeNonDefaultScopedEffects)
            {
                continue;
            }

            if (_random.NextDouble() > effect.Chance)
            {
                continue;
            }

            var effectRecipients = SkillTargetResolver.ResolveEffectRecipients(
                state,
                actor,
                target,
                effect.EffectScope);

            ApplyEffectToRecipients(state, actor, skill, effect, effectRecipients);
        }
    }

    public void ApplyPassiveExtraDotsAfterEnemySkill(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var passiveExtraDotNotes = new List<PassiveCombatNote>();
        state.PassiveBus.ApplyPassiveExtraDotsAfterEnemySkill(
            state,
            actor,
            target,
            skill,
            CombatDamageCalculator.GetElementalMultiplier(state, actor, target, skill),
            (defender, dotType) => EffectPassesResistance(defender, dotType, state),
            passiveExtraDotNotes);
        foreach (var note in passiveExtraDotNotes)
        {
            _eventEmitter.Emit(
                state,
                BattleEventType.DotInflicted,
                actorId: actor.Identity.Id,
                targetId: target.Identity.Id,
                skillId: skill.Id,
                dotType: note.DotTypeName ?? string.Empty,
                dotAmount: (int)Math.Round(note.Magnitude),
                passiveId: note.PassiveId,
                passiveEffectKindName: note.EffectKind.ToString(),
                passiveRelatedSkillId: note.RelatedSkillId ?? string.Empty,
                dotDurationTurns: note.DotDurationTurns);
        }
    }

    public void ApplyPostSkillPassiveExtras(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var postSkillPassiveNotes = new List<PassiveCombatNote>();
        state.PassiveBus.ApplyPostSkillPassiveExtras(state, actor, target, skill, postSkillPassiveNotes);
        foreach (var note in postSkillPassiveNotes)
        {
            if (note.EffectKind == PassiveEffectKind.ExtraTokenOnSelfSkill &&
                !string.IsNullOrEmpty(note.TokenTypeName))
            {
                _eventEmitter.Emit(
                    state,
                    BattleEventType.TokenApplied,
                    actorId: actor.Identity.Id,
                    targetId: actor.Identity.Id,
                    skillId: skill.Id,
                    tokenType: note.TokenTypeName,
                    tokenDelta: note.TokenDelta);
            }
            else if (note.EffectKind == PassiveEffectKind.ExtraHealPercentOnSelfSkill)
            {
                _eventEmitter.EmitPassiveCombatNarrativeEvent(state, note, actor.Identity.Id, actor.Identity.Id, skill.Id);
            }
        }

        state.PassiveBus.RaiseAfterSkillResolved(state, actor, target, skill);
    }

    private void ApplyEffectToRecipients(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        EffectSpec effect,
        IReadOnlyList<Combatant> effectRecipients)
    {
        switch (effect.Type)
        {
            case EffectType.ApplyToken:
                if (!effect.Token.HasValue)
                {
                    return;
                }

                var stacks = Math.Max(1, effect.Stacks);
                if (effect.ScaleFromToken.HasValue && effect.ScaleStacksPerSourceStack != 0)
                {
                    var sourceStacks = actor.Tokens.GetStacks(effect.ScaleFromToken.Value);
                    var divisor = Math.Max(1, effect.ScaleStacksSourceDivisor);
                    stacks += (sourceStacks * effect.ScaleStacksPerSourceStack) / divisor;
                }

                foreach (var recipient in effectRecipients)
                {
                    ApplyTokenToCombatant(state, actor, recipient, skill, effect.Token.Value, stacks);
                }

                break;
            case EffectType.ApplyDot:
                if (!effect.Dot.HasValue)
                {
                    return;
                }

                foreach (var recipient in effectRecipients)
                {
                    TryApplyDotToCombatant(state, actor, recipient, skill, effect.Dot.Value, effect.Potency, effect.Duration);
                }

                break;
            case EffectType.ApplyRandomDot:
            {
                var randomDotCandidates = new[] { DotType.Burn, DotType.Blight, DotType.Bleed };
                var chosenDotType = randomDotCandidates[_random.Next(0, randomDotCandidates.Length)];
                foreach (var recipient in effectRecipients)
                {
                    TryApplyDotToCombatant(state, actor, recipient, skill, chosenDotType, effect.Potency, effect.Duration);
                }

                break;
            }
            case EffectType.Push:
                foreach (var recipient in effectRecipients)
                {
                    MoveTarget(state, recipient, +Math.Abs(effect.Steps));
                }

                break;
            case EffectType.Pull:
                foreach (var recipient in effectRecipients)
                {
                    MoveTarget(state, recipient, -Math.Abs(effect.Steps));
                }

                break;
            case EffectType.HealHp:
                ApplyHealHpEffect(state, actor, skill, effect, effectRecipients);
                break;
            case EffectType.HealHpPercent:
                ApplyHealHpPercentEffect(state, actor, skill, effect, effectRecipients);
                break;
            case EffectType.ApplyStun:
                foreach (var recipient in effectRecipients)
                {
                    if (_random.NextDouble() >= recipient.Resistances.StunRes)
                    {
                        var stunStacks = Math.Max(1, effect.Stacks);
                        ApplyTokenToCombatant(state, actor, recipient, skill, TokenType.Stun, stunStacks);
                    }
                }

                break;
            case EffectType.RemoveAllDebuffTokens:
                foreach (var recipient in effectRecipients)
                {
                    recipient.Tokens.ClearDebuffTokens();
                    state.PassiveBus.RaiseTokenStacksChanged(
                        state,
                        actor,
                        recipient,
                        skill,
                        TokenType.Weaken,
                        delta: 0);
                }

                break;
            case EffectType.ConsumeAllTokenStacksDealDamagePerStack:
                ApplyConsumeAllTokenStacksDealDamage(state, actor, skill, effect, effectRecipients);
                break;
            case EffectType.ConsumeAllTokenStacksHealPerStack:
                ApplyConsumeAllTokenStacksHeal(state, actor, skill, effect, effectRecipients);
                break;
            case EffectType.SelfDamageFlat:
                ApplySelfDamageFlat(state, actor, skill, effect);
                break;
            case EffectType.TriggerDestabilizationOnTargets:
                foreach (var recipient in effectRecipients)
                {
                    BattleCombatStatusTicker.TriggerDestabilizationExplosion(
                        state,
                        recipient,
                        _eventEmitter,
                        skill.Id,
                        actor.Identity.Id);
                }

                break;
            case EffectType.ApplyBonusAction:
                foreach (var recipient in effectRecipients)
                {
                    var bonusActionStacks = Math.Max(1, effect.Stacks);
                    ApplyTokenToCombatant(state, actor, recipient, skill, TokenType.BonusAction, bonusActionStacks);
                }

                break;
        }
    }

    private void ApplyHealHpEffect(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        EffectSpec effect,
        IReadOnlyList<Combatant> effectRecipients)
    {
        var potencyMin = Math.Max(0, effect.Potency);
        if (potencyMin <= 0)
        {
            return;
        }

        if (!CombatHealUnlock.IsCombatHealingUnlocked)
        {
            LogForbiddenCombatHeal(actor, skill, effectRecipients, "HealHp", potencyMin);
            return;
        }

        var potencyMax = effect.AmountMax > potencyMin ? effect.AmountMax : potencyMin;
        foreach (var recipient in effectRecipients)
        {
            var healRoll = potencyMin == potencyMax
                ? potencyMin
                : _random.Next(potencyMin, potencyMax + 1);
            CombatHealUnlock.ApplyHealHpToRecipient(recipient, healRoll);
        }
    }

    private void ApplyHealHpPercentEffect(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        EffectSpec effect,
        IReadOnlyList<Combatant> effectRecipients)
    {
        var percentOfMaxHp = Math.Max(0, effect.Potency);
        if (percentOfMaxHp <= 0)
        {
            return;
        }

        if (!CombatHealUnlock.IsCombatHealingUnlocked)
        {
            LogForbiddenCombatHeal(actor, skill, effectRecipients, "HealHpPercent", percentOfMaxHp);
            return;
        }

        foreach (var recipient in effectRecipients)
        {
            CombatHealUnlock.ApplyHealHpPercentToRecipient(recipient, percentOfMaxHp);
        }
    }

    private void ApplyConsumeAllTokenStacksDealDamage(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        EffectSpec effect,
        IReadOnlyList<Combatant> effectRecipients)
    {
        if (!effect.Token.HasValue)
        {
            return;
        }

        var removedStacks = actor.Tokens.ConsumeAllStacks(effect.Token.Value);
        if (removedStacks <= 0)
        {
            return;
        }

        state.PassiveBus.RaiseTokenStacksChanged(
            state,
            actor,
            actor,
            skill,
            effect.Token.Value,
            delta: -removedStacks);

        var damagePerTarget = Math.Max(0, effect.Potency) * removedStacks;
        if (damagePerTarget <= 0)
        {
            return;
        }

        var damageTargets = effectRecipients.Count > 0
            ? effectRecipients
            : SkillTargetResolver.ResolveEffectRecipients(state, actor, actor, EffectScope.AllEnemies);

        foreach (var damageTarget in damageTargets)
        {
            if (damageTarget.Health.IsDead)
            {
                continue;
            }

            BattleCombatStatusTicker.ApplyDirectHpLoss(
                state,
                damageTarget,
                damagePerTarget,
                _eventEmitter,
                actor.Identity.Id,
                skill.Id,
                markDeath: true);
        }

        // Loss of control also deals 1 self damage per token when Potency of a paired SelfDamageFlat is used;
        // optional: if Steps > 0, treat Steps as self-damage-per-stack.
        if (effect.Steps > 0)
        {
            var selfDamage = effect.Steps * removedStacks;
            BattleCombatStatusTicker.ApplyDirectHpLoss(
                state,
                actor,
                selfDamage,
                _eventEmitter,
                actor.Identity.Id,
                skill.Id,
                markDeath: true);
        }
    }

    private void ApplyConsumeAllTokenStacksHeal(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        EffectSpec effect,
        IReadOnlyList<Combatant> effectRecipients)
    {
        if (!effect.Token.HasValue)
        {
            return;
        }

        var removedStacks = actor.Tokens.ConsumeAllStacks(effect.Token.Value);
        if (removedStacks <= 0)
        {
            return;
        }

        state.PassiveBus.RaiseTokenStacksChanged(
            state,
            actor,
            actor,
            skill,
            effect.Token.Value,
            delta: -removedStacks);

        if (!CombatHealUnlock.IsCombatHealingUnlocked)
        {
            LogForbiddenCombatHeal(actor, skill, effectRecipients, "ConsumeAllTokenStacksHealPerStack", removedStacks);
            return;
        }

        var healPerStack = Math.Max(1, effect.Potency);
        var healAmount = healPerStack * removedStacks;
        var healRecipients = effectRecipients.Count > 0 ? effectRecipients : new[] { actor };
        foreach (var recipient in healRecipients)
        {
            CombatHealUnlock.ApplyHealHpToRecipient(recipient, healAmount);
        }
    }

    private void ApplySelfDamageFlat(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        EffectSpec effect)
    {
        var selfDamage = Math.Max(0, effect.Potency);
        if (selfDamage <= 0)
        {
            return;
        }

        BattleCombatStatusTicker.ApplyDirectHpLoss(
            state,
            actor,
            selfDamage,
            _eventEmitter,
            actor.Identity.Id,
            skill.Id,
            markDeath: true);
    }

    private void ApplyTokenToCombatant(
        BattleState state,
        Combatant actor,
        Combatant recipient,
        SkillDefinition skill,
        TokenType tokenType,
        int stacks)
    {
        recipient.Tokens.Add(tokenType, stacks);
        state.PassiveBus.RaiseTokenStacksChanged(
            state,
            actor,
            recipient,
            skill,
            tokenType,
            stacks);
        _eventEmitter.Emit(
            state,
            BattleEventType.TokenApplied,
            actorId: actor.Identity.Id,
            targetId: recipient.Identity.Id,
            skillId: skill.Id,
            tokenType: tokenType.ToString(),
            tokenDelta: stacks);
    }

    private void TryApplyDotToCombatant(
        BattleState state,
        Combatant actor,
        Combatant recipient,
        SkillDefinition skill,
        DotType dotType,
        int potency,
        int durationTurns)
    {
        if (!EffectPassesResistance(recipient, dotType, state))
        {
            return;
        }

        var elementalMultiplier = CombatDamageCalculator.GetElementalMultiplier(state, actor, recipient, skill);
        var resolvedPotency = (int)Math.Round(Math.Max(1, potency) * elementalMultiplier);
        var baseDuration = Math.Max(1, durationTurns);
        var duration = state.PassiveBus.AdjustDotDuration(state, actor, dotType, baseDuration);
        recipient.Dots.ActiveDots.Add(new DotInstance
        {
            Type = dotType,
            Potency = resolvedPotency,
            RemainingTurns = duration,
            AppliedById = actor.Identity.Id,
        });
        _eventEmitter.Emit(
            state,
            BattleEventType.DotInflicted,
            actorId: actor.Identity.Id,
            targetId: recipient.Identity.Id,
            skillId: skill.Id,
            dotType: dotType.ToString(),
            dotAmount: resolvedPotency,
            dotDurationTurns: duration);
    }

    private static void LogForbiddenCombatHeal(
        Combatant actor,
        SkillDefinition skill,
        IReadOnlyList<Combatant> effectRecipients,
        string healEffectTypeName,
        int potencyOrPercent)
    {
        if (potencyOrPercent <= 0)
        {
            return;
        }

        var blockedHealTarget = effectRecipients.Count > 0 ? effectRecipients[0] : actor;
        HealDebugTrace.Log(
            $"[FORBIDDEN] [COMBAT] {healEffectTypeName} ignorado skill='{skill.Id}' actor='{actor.Identity.Id}' " +
            $"target='{blockedHealTarget.Identity.Id}' potency={potencyOrPercent}. " +
            "Cura de HP só é permitida pelo Main após 3s na vila. " +
            "Gancho: CombatHealUnlock.ApplyHealHpToRecipient quando IsCombatHealingUnlocked.");
    }

    private bool EffectPassesResistance(Combatant target, DotType dotType, BattleState state)
    {
        var resistance = dotType switch
        {
            DotType.Burn => target.Resistances.BurnRes,
            DotType.Blight => target.Resistances.BlightRes,
            DotType.Bleed => target.Resistances.BlightRes,
            _ => 0,
        };
        return _random.NextDouble() >= resistance;
    }

    private void MoveTarget(BattleState state, Combatant target, int steps)
    {
        if (steps == 0) return;
        var newFront = Math.Clamp(target.Position.FrontRank + steps, 1, 5 - target.Position.Size);
        target.Position.FrontRank = newFront;
        HandleCompaction(state, target.Position.Side);
    }

    public void HandleCompaction(BattleState state, Side side)
    {
        var roster = side == Side.Allies ? state.Allies : state.Enemies;
        var alive = roster.Where(combatant => !combatant.Health.IsDead).OrderBy(combatant => combatant.Position.FrontRank).ToList();
        var nextRank = 1;
        foreach (var unit in alive)
        {
            unit.Position.FrontRank = nextRank;
            nextRank += unit.Position.Size;
        }
    }
}
