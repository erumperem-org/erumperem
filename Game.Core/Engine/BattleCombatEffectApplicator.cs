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
                LogForbiddenCombatHeal(actor, skill, effectRecipients, "HealHp", Math.Max(0, effect.Potency));
                break;
            case EffectType.HealHpPercent:
                LogForbiddenCombatHeal(actor, skill, effectRecipients, "HealHpPercent", Math.Max(0, effect.Potency));
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
        }
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
