using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Diagnostics;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

public sealed class BattleSimulator
{
    private readonly IRandomSource _random;
    private readonly BattleCombatEventEmitter _eventEmitter;
    private readonly BattleCombatEffectApplicator _effectApplicator;
    private readonly BattleAiActionChooser _aiActionChooser;

    public BattleSimulator(IRandomSource random, CombatEventCollector eventCollector)
    {
        _random = random;
        _eventEmitter = new BattleCombatEventEmitter(eventCollector);
        _effectApplicator = new BattleCombatEffectApplicator(random, _eventEmitter);
        _aiActionChooser = new BattleAiActionChooser(random);
    }

    public BattleState Simulate(BattleState state, int maxTurns = 100)
    {
        EmitBattleStarted(state);

        while (!state.IsFinished && state.TurnNumber < maxTurns)
        {
            state.TurnNumber++;
            var turnOrder = BuildInitiativeOrder(state);
            foreach (var actor in turnOrder)
            {
                if (state.IsFinished) break;
                if (actor.Health.IsDead) continue;

                if (!TryPrepareActorTurn(state, actor))
                {
                    continue;
                }

                var action = ChooseAiAction(state, actor);
                if (action is null) continue;
                ResolveChosenAction(state, action);
            }
        }

        EmitBattleEnded(state);
        return state;
    }

    /// <summary>Evento inicial de batalha (telemetria / UI).</summary>
    public void EmitBattleStarted(BattleState state)
    {
        EnsureInitiativeResolved(state);
        _eventEmitter.EmitBattleStarted(state);
    }

    /// <summary>Evento de fim com vencedor atual.</summary>
    public void EmitBattleEnded(BattleState state)
    {
        _eventEmitter.EmitBattleEnded(state);
    }

    /// <summary>Emite morte de combatente (cheats QA, testes ou fluxos fora de <see cref="ResolveChosenAction"/>).</summary>
    public void EmitCombatantDied(BattleState state, string targetCombatantId) =>
        _eventEmitter.EmitCombatantDied(state, targetCombatantId);

    /// <summary>TurnStarted, passivas de início de turno, DOTs e stun. Devolve false se o actor não age (morto, stun, etc.).</summary>
    public bool TryPrepareActorTurn(BattleState state, Combatant actor)
    {
        if (actor.Health.IsDead || state.IsFinished)
        {
            return false;
        }

        _eventEmitter.Emit(state, BattleEventType.TurnStarted, actorId: actor.Identity.Id, battleResult: string.Empty);
        state.PassiveBus.RaiseTurnStarted(
            state,
            actor,
            (tokenType, stackDelta) => _eventEmitter.Emit(
                state,
                BattleEventType.TokenApplied,
                actorId: actor.Identity.Id,
                targetId: actor.Identity.Id,
                tokenType: tokenType.ToString(),
                tokenDelta: stackDelta,
                battleResult: string.Empty));
        if (PassiveRuleApplier.TryApplyTurnStartSummonPassives(
                state,
                actor,
                _random,
                out var spawnedCombatant,
                out var spawnRankUsed,
                out var summonPassiveDefinition))
        {
            _eventEmitter.Emit(
                state,
                BattleEventType.CombatantSpawned,
                actorId: actor.Identity.Id,
                targetId: spawnedCombatant.Identity.Id,
                skillId: summonPassiveDefinition.SkillId ?? string.Empty,
                passiveId: summonPassiveDefinition.Id,
                passiveEffectKindName: summonPassiveDefinition.EffectKind.ToString(),
                passiveAuxInt: spawnRankUsed);
        }

        ResolveDotTick(state, actor);
        if (actor.Health.IsDead || state.IsFinished)
        {
            return false;
        }

        if (actor.Tokens.ConsumeOne(TokenType.Stun))
        {
            state.PassiveBus.RaiseTokenStacksChanged(state, actor, actor, skill: null, TokenType.Stun, delta: -1);
            return false;
        }

        return true;
    }

    /// <summary>Ordem de turno da ronda actual (vivo), com base na iniciativa e posição/roster.</summary>
    public List<Combatant> BuildInitiativeOrder(BattleState state)
    {
        EnsureInitiativeResolved(state);
        return InitiativeResolver.BuildTurnOrder(state);
    }

    private void EnsureInitiativeResolved(BattleState state)
    {
        if (state.Initiative is not null)
        {
            return;
        }

        state.Initiative = InitiativeResolver.RollInitiative(state, _random);
    }

    private void ResolveDotTick(BattleState state, Combatant actor)
    {
        if (actor.Dots.ActiveDots.Count == 0) return;
        var active = actor.Dots.ActiveDots.ToList();
        actor.Dots.ActiveDots.Clear();
        foreach (var dot in active)
        {
            var tickMult = state.PassiveBus.GetDotTickDamageMultiplier(state, actor, dot);
            var damage = Math.Max(0, (int)Math.Round(dot.Potency * tickMult));
            var dotSourceCombatant = string.IsNullOrEmpty(dot.AppliedById)
                ? null
                : state.GetAllCombatants().FirstOrDefault(combatant => combatant.Identity.Id == dot.AppliedById);
            if (damage > 0 && !IsAllyInfiniteHealthProtected(state, actor))
            {
                var hpPercentBeforeDot =
                    actor.Health.MaxHp <= 0 ? 0 : (double)actor.Health.CurrentHp / actor.Health.MaxHp;
                actor.Health.CurrentHp = Math.Max(0, actor.Health.CurrentHp - damage);
                var hpPercentAfterDot =
                    actor.Health.MaxHp <= 0 ? 0 : (double)actor.Health.CurrentHp / actor.Health.MaxHp;
                _eventEmitter.Emit(
                    state,
                    BattleEventType.DotTick,
                    actorId: dot.AppliedById,
                    targetId: actor.Identity.Id,
                    dotType: dot.Type.ToString(),
                    dotAmount: damage,
                    damageAmount: damage);
                state.PassiveBus.RaiseDamageTaken(
                    state,
                    dotSourceCombatant,
                    actor,
                    skill: null,
                    damage,
                    wasCrit: false,
                    hpPercentBeforeDot,
                    hpPercentAfterDot);
            }
            else if (damage > 0 && IsAllyInfiniteHealthProtected(state, actor))
            {
                _eventEmitter.Emit(
                    state,
                    BattleEventType.DotTick,
                    actorId: dot.AppliedById,
                    targetId: actor.Identity.Id,
                    dotType: dot.Type.ToString(),
                    dotAmount: damage,
                    damageAmount: 0);
            }

            dot.RemainingTurns--;
            if (dot.RemainingTurns > 0)
            {
                actor.Dots.ActiveDots.Add(dot);
            }

            if (actor.Health.CurrentHp <= 0 && !actor.Health.IsDead && !IsAllyInfiniteHealthProtected(state, actor))
            {
                actor.Health.IsDead = true;
                _eventEmitter.Emit(state, BattleEventType.CombatantDied, targetId: actor.Identity.Id);
                state.PassiveBus.RaiseCombatantSlain(state, dotSourceCombatant, actor);
                _effectApplicator.HandleCompaction(state, actor.Position.Side);
                break;
            }
        }
    }

    /// <summary>Escolha automática (AI / simulação headless).</summary>
    public ChosenAction? ChooseAiAction(BattleState state, Combatant actor) =>
        _aiActionChooser.ChooseAiAction(state, actor, IsSkillUsable);

    public bool IsSkillUsable(Combatant actor, SkillDefinition skill)
    {
        if (skill.SelfHpPercentBelow < 1.0)
        {
            if (actor.Health.MaxHp <= 0)
            {
                return false;
            }

            var currentHpPercent = (double)actor.Health.CurrentHp / actor.Health.MaxHp;
            if (currentHpPercent >= skill.SelfHpPercentBelow)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Resolve uma ação já escolhida (player ou AI).</summary>
    public void ResolveChosenAction(BattleState state, ChosenAction action)
    {
        var actor = action.Actor;
        var target = action.Target;
        var skill = action.Skill;
        _eventEmitter.Emit(
            state,
            BattleEventType.ActionUsed,
            actorId: actor.Identity.Id,
            targetId: target.Identity.Id,
            skillId: skill.Id,
            element: skill.Element);

        ResolveActionResult result;
        if (skill.TargetKind == SkillTargetKind.Enemy)
        {
            result = ResolveHitAndDamage(state, actor, target, skill);
            EmitHitResolved(state, actor, target, skill, result);
            if (result.IsHit)
            {
                _effectApplicator.ApplyEffects(state, actor, target, skill, result);
            }
        }
        else if (skill.TargetKind == SkillTargetKind.Ally)
        {
            if (skill.BaseDamage.Max > 0)
            {
                result = ResolveHitAndDamage(state, actor, target, skill);
            }
            else
            {
                result = new ResolveActionResult { IsHit = true, IsCrit = false, DamageApplied = 0 };
            }

            EmitHitResolved(state, actor, target, skill, result);
            if (result.IsHit)
            {
                _effectApplicator.ApplyEffects(state, actor, target, skill, result);
            }
        }
        else
        {
            if (skill.BaseDamage.Max == 0 && skill.BaseDamage.Min == 0)
            {
                result = new ResolveActionResult { IsHit = true, IsCrit = false, DamageApplied = 0 };
                EmitHitResolved(state, actor, target, skill, result);
                _effectApplicator.ApplyEffects(state, actor, target, skill, result);
            }
            else
            {
                result = ResolveHitAndDamage(state, actor, target, skill);
                EmitHitResolved(state, actor, target, skill, result);
                if (result.IsHit)
                {
                    _effectApplicator.ApplyEffects(state, actor, target, skill, result);
                }
            }
        }

        if (action.ActionType == ActionType.Skill && actor.Identity.Faction == Faction.Player)
        {
            ApplyBattleCorruptionDelta(state, skill.CorruptionCost, actor.Identity.Id, skill.Id);
        }

        state.PassiveBus.RaiseTurnEnded(state, actor);
    }

    private void EmitHitResolved(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        ResolveActionResult result)
    {
        _eventEmitter.Emit(
            state,
            BattleEventType.HitResolved,
            actorId: actor.Identity.Id,
            targetId: target.Identity.Id,
            skillId: skill.Id,
            element: skill.Element,
            isHit: result.IsHit,
            isCrit: result.IsCrit,
            damageAmount: result.DamageApplied);
    }

    private ResolveActionResult ResolveHitAndDamage(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        if (actor.Tokens.GetStacks(TokenType.Blind) > 0)
        {
            actor.Tokens.ConsumeOne(TokenType.Blind);
            var blindMiss = _random.NextDouble() < state.BalanceConfig.BlindMissChance;
            if (blindMiss)
            {
                return new ResolveActionResult { IsHit = false, IsCrit = false, DamageApplied = 0 };
            }
        }

        if (_random.NextDouble() > skill.Accuracy * actor.Stats.Accuracy)
        {
            return new ResolveActionResult { IsHit = false, IsCrit = false, DamageApplied = 0 };
        }

        if (target.Tokens.GetStacks(TokenType.Dodge) > 0)
        {
            var dodged = _random.NextDouble() < state.BalanceConfig.DodgeNegateChance;
            target.Tokens.ConsumeOne(TokenType.Dodge);
            if (dodged)
            {
                return new ResolveActionResult { IsHit = false, IsCrit = false, DamageApplied = 0 };
            }
        }

        var isCrit = _random.NextDouble() < CombatDamageCalculator.EffectiveCritChanceFraction(state, actor, target, skill);
        var baseRollDamage = _random.Next(skill.BaseDamage.Min, skill.BaseDamage.Max + 1);
        var damageComputation = CombatDamageCalculator.ComputeDirectDamageBeforeMitigation(
            state,
            actor,
            target,
            skill,
            baseRollDamage,
            isCrit,
            capturePassiveNotes: true);

        if (damageComputation.ShouldClearImpetoCleaveBonus)
        {
            actor.PassiveRuntime.ImpetoCleaveBonusPending = false;
        }

        var damage = CombatDamageCalculator.ApplyMitigation(
            state,
            target,
            damageComputation.DamageBeforeMitigation,
            consumeMitigationTokens: true);
        var targetHpBeforeHit = target.Health.CurrentHp;
        if (damage > 0 && !IsAllyInfiniteHealthProtected(state, target))
        {
            target.Health.CurrentHp = Math.Max(0, target.Health.CurrentHp - damage);
        }

        _eventEmitter.Emit(
            state,
            BattleEventType.DamageApplied,
            actorId: actor.Identity.Id,
            targetId: target.Identity.Id,
            skillId: skill.Id,
            element: skill.Element,
            isHit: true,
            isCrit: isCrit,
            damageAmount: damage);

        foreach (var note in damageComputation.OutgoingPassiveNotes)
        {
            _eventEmitter.EmitPassiveCombatNarrativeEvent(state, note, actor.Identity.Id, target.Identity.Id, skill.Id);
        }

        foreach (var note in damageComputation.IncomingPassiveNotes)
        {
            _eventEmitter.EmitPassiveCombatNarrativeEvent(state, note, target.Identity.Id, target.Identity.Id, skill.Id);
        }

        if (damage > 0)
        {
            var hpPercentBeforeDamage =
                target.Health.MaxHp <= 0 ? 0 : (double)targetHpBeforeHit / target.Health.MaxHp;
            var hpPercentAfterDamage =
                target.Health.MaxHp <= 0 ? 0 : (double)target.Health.CurrentHp / target.Health.MaxHp;
            state.PassiveBus.RaiseDamageTaken(
                state,
                actor,
                target,
                skill,
                damage,
                isCrit,
                hpPercentBeforeDamage,
                hpPercentAfterDamage);
        }

        if (target.Health.CurrentHp <= 0 && !target.Health.IsDead && !IsAllyInfiniteHealthProtected(state, target))
        {
            target.Health.IsDead = true;
            _eventEmitter.Emit(state, BattleEventType.CombatantDied, targetId: target.Identity.Id);
            state.PassiveBus.RaiseCombatantSlain(state, actor, target);
            _effectApplicator.HandleCompaction(state, target.Position.Side);
        }

        state.PassiveBus.RaiseOutgoingHitSuccess(state, actor, target, skill, hit: true);

        return new ResolveActionResult
        {
            IsHit = true,
            IsCrit = isCrit,
            DamageApplied = damage,
        };
    }

    private static bool IsAllyInfiniteHealthProtected(BattleState state, Combatant combatant) =>
        state.AlliesHaveInfiniteHealth && combatant.Identity.Faction == Faction.Player;

    private void ApplyBattleCorruptionDelta(BattleState state, double delta, string actorId, string skillId)
    {
        if (double.IsNaN(delta) || double.IsInfinity(delta) || Math.Abs(delta) < 1e-12)
        {
            return;
        }

        if (delta < 0)
        {
            HealDebugTrace.Log(
                $"[FORBIDDEN] [COMBAT] Redução de corrupção ignorada actor='{actorId}' skill='{skillId}' " +
                $"Δ{delta:F2}. Corrupção só zera pelo Main após 3s na vila.");
            return;
        }

        var corruptionBeforeDelta = state.CorruptionValue;
        var tierBeforeAdjustment = CorruptionTierCalculator.GetTier(corruptionBeforeDelta);
        state.CorruptionValue = Math.Max(CorruptionRules.MinCorruptionValue, corruptionBeforeDelta + delta);

        _eventEmitter.Emit(
            state,
            BattleEventType.CorruptionAdjusted,
            actorId: actorId,
            skillId: skillId,
            corruptionDelta: delta,
            previousCorruptionTier: tierBeforeAdjustment);
    }
}
