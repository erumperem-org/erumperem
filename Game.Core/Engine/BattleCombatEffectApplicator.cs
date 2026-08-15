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
        ResolveActionResult result)
    {
        var effects = skill.EffectsOnHit.ToList();
        var comboBonusWasIncluded =
            target.Tokens.GetStacks(TokenType.Combo) > 0 && skill.ComboBonus.Count > 0;
        if (target.Tokens.GetStacks(TokenType.Combo) > 0)
        {
            effects.AddRange(skill.ComboBonus);
        }

        if (comboBonusWasIncluded)
        {
            state.PassiveBus.RaiseComboBonusEffectsIncluded(state, actor, target, skill);
        }

        foreach (var effect in effects)
        {
            if (_random.NextDouble() > effect.Chance) continue;
            switch (effect.Type)
            {
                case EffectType.ApplyToken:
                    if (effect.Token.HasValue)
                    {
                        var stacks = Math.Max(1, effect.Stacks);
                        if (string.Equals(effect.EffectScope, EffectScopes.AllAllies, StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var ally in LivingSameSide(state, actor))
                            {
                                ally.Tokens.Add(effect.Token.Value, stacks);
                                state.PassiveBus.RaiseTokenStacksChanged(
                                    state,
                                    actor,
                                    ally,
                                    skill,
                                    effect.Token.Value,
                                    stacks);
                                _eventEmitter.Emit(
                                    state,
                                    BattleEventType.TokenApplied,
                                    actorId: actor.Identity.Id,
                                    targetId: ally.Identity.Id,
                                    skillId: skill.Id,
                                    tokenType: effect.Token.Value.ToString(),
                                    tokenDelta: stacks);
                            }
                        }
                        else if (string.Equals(effect.EffectScope, EffectScopes.Self, StringComparison.OrdinalIgnoreCase))
                        {
                            actor.Tokens.Add(effect.Token.Value, stacks);
                            state.PassiveBus.RaiseTokenStacksChanged(
                                state,
                                actor,
                                actor,
                                skill,
                                effect.Token.Value,
                                stacks);
                            _eventEmitter.Emit(
                                state,
                                BattleEventType.TokenApplied,
                                actorId: actor.Identity.Id,
                                targetId: actor.Identity.Id,
                                skillId: skill.Id,
                                tokenType: effect.Token.Value.ToString(),
                                tokenDelta: stacks);
                        }
                        else
                        {
                            target.Tokens.Add(effect.Token.Value, stacks);
                            state.PassiveBus.RaiseTokenStacksChanged(
                                state,
                                actor,
                                target,
                                skill,
                                effect.Token.Value,
                                stacks);
                            _eventEmitter.Emit(
                                state,
                                BattleEventType.TokenApplied,
                                actorId: actor.Identity.Id,
                                targetId: target.Identity.Id,
                                skillId: skill.Id,
                                tokenType: effect.Token.Value.ToString(),
                                tokenDelta: stacks);
                        }
                    }

                    break;
                case EffectType.ApplyDot:
                    if (effect.Dot.HasValue && EffectPassesResistance(target, effect.Dot.Value, state))
                    {
                        var elementalMultiplier = CombatDamageCalculator.GetElementalMultiplier(state, actor, target, skill);
                        var potency = (int)Math.Round(Math.Max(1, effect.Potency) * elementalMultiplier);
                        var baseDuration = Math.Max(1, effect.Duration);
                        var duration = state.PassiveBus.AdjustDotDuration(state, actor, effect.Dot.Value, baseDuration);
                        target.Dots.ActiveDots.Add(new DotInstance
                        {
                            Type = effect.Dot.Value,
                            Potency = potency,
                            RemainingTurns = duration,
                            AppliedById = actor.Identity.Id,
                        });
                        _eventEmitter.Emit(
                            state,
                            BattleEventType.DotInflicted,
                            actorId: actor.Identity.Id,
                            targetId: target.Identity.Id,
                            skillId: skill.Id,
                            dotType: effect.Dot.Value.ToString(),
                            dotAmount: potency,
                            dotDurationTurns: duration);
                    }

                    break;
                case EffectType.ApplyRandomDot:
                {
                    var randomDotCandidates = new[] { DotType.Burn, DotType.Blight, DotType.Bleed };
                    var chosenDotType = randomDotCandidates[_random.Next(0, randomDotCandidates.Length)];
                    if (EffectPassesResistance(target, chosenDotType, state))
                    {
                        var elementalMultiplier = CombatDamageCalculator.GetElementalMultiplier(state, actor, target, skill);
                        var potency = (int)Math.Round(Math.Max(1, effect.Potency) * elementalMultiplier);
                        var baseDuration = Math.Max(1, effect.Duration);
                        var duration = state.PassiveBus.AdjustDotDuration(state, actor, chosenDotType, baseDuration);
                        target.Dots.ActiveDots.Add(new DotInstance
                        {
                            Type = chosenDotType,
                            Potency = potency,
                            RemainingTurns = duration,
                            AppliedById = actor.Identity.Id,
                        });
                        _eventEmitter.Emit(
                            state,
                            BattleEventType.DotInflicted,
                            actorId: actor.Identity.Id,
                            targetId: target.Identity.Id,
                            skillId: skill.Id,
                            dotType: chosenDotType.ToString(),
                            dotAmount: potency,
                            dotDurationTurns: duration);
                    }

                    break;
                }
                case EffectType.Push:
                    MoveTarget(state, target, +Math.Abs(effect.Steps));
                    break;
                case EffectType.Pull:
                    MoveTarget(state, target, -Math.Abs(effect.Steps));
                    break;
                case EffectType.HealHp:
                {
                    var blockedHealTarget = skill.TargetKind == SkillTargetKind.Enemy ? actor : target;
                    var healPotency = Math.Max(0, effect.Potency);
                    if (healPotency > 0)
                    {
                        HealDebugTrace.Log(
                            $"[FORBIDDEN] [COMBAT] HealHp ignorado skill='{skill.Id}' actor='{actor.Identity.Id}' " +
                            $"target='{blockedHealTarget.Identity.Id}' potency={healPotency}. " +
                            "Cura de HP só é permitida pelo Main após 3s na vila.");
                    }
                    break;
                }
                case EffectType.HealHpPercent:
                {
                    var blockedHealTarget = skill.TargetKind == SkillTargetKind.Enemy ? actor : target;
                    var healPercent = Math.Max(0, effect.Potency);
                    if (healPercent > 0)
                    {
                        HealDebugTrace.Log(
                            $"[FORBIDDEN] [COMBAT] HealHpPercent ignorado skill='{skill.Id}' actor='{actor.Identity.Id}' " +
                            $"target='{blockedHealTarget.Identity.Id}' percent={healPercent}. " +
                            "Cura de HP só é permitida pelo Main após 3s na vila.");
                    }
                    break;
                }
                case EffectType.ApplyStun:
                    if (_random.NextDouble() >= target.Resistances.StunRes)
                    {
                        var stunStacks = Math.Max(1, effect.Stacks);
                        target.Tokens.Add(TokenType.Stun, stunStacks);
                        state.PassiveBus.RaiseTokenStacksChanged(
                            state,
                            actor,
                            target,
                            skill,
                            TokenType.Stun,
                            stunStacks);
                        _eventEmitter.Emit(
                            state,
                            BattleEventType.TokenApplied,
                            actorId: actor.Identity.Id,
                            targetId: target.Identity.Id,
                            skillId: skill.Id,
                            tokenType: TokenType.Stun.ToString(),
                            tokenDelta: stunStacks);
                    }
                    break;
            }
        }

        if (comboBonusWasIncluded && target.Tokens.ConsumeOne(TokenType.Combo))
        {
            state.PassiveBus.RaiseComboConsumed(state, actor, target, skill);
        }

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
        state.PassiveBus.RaiseAfterSkillResolved(state, actor, target, skill);
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

    private static IEnumerable<Combatant> LivingSameSide(BattleState state, Combatant actor)
    {
        var roster = actor.Position.Side == Side.Allies ? state.Allies : state.Enemies;
        return roster.Where(combatant => !combatant.Health.IsDead);
    }
}
