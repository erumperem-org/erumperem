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

                while (!actor.Health.IsDead &&
                       !state.IsFinished &&
                       actor.PassiveRuntime.ShouldRetainTurnForBonusAction)
                {
                    actor.PassiveRuntime.ShouldRetainTurnForBonusAction = false;
                    if (actor.Tokens.ConsumeOne(TokenType.BonusAction))
                    {
                        state.PassiveBus.RaiseTokenStacksChanged(
                            state,
                            actor,
                            actor,
                            skill: null,
                            TokenType.BonusAction,
                            delta: -1);
                    }

                    var bonusAction = ChooseAiAction(state, actor);
                    if (bonusAction is null)
                    {
                        break;
                    }

                    ResolveChosenAction(state, bonusAction);
                }
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

        BattleCombatStatusTicker.ApplyTurnStartStatusEffects(state, actor, _eventEmitter);

        if (actor.Tokens.ConsumeOne(TokenType.Stun))
        {
            state.PassiveBus.RaiseTokenStacksChanged(state, actor, actor, skill: null, TokenType.Stun, delta: -1);
            BattleCombatStatusTicker.ApplyEndOfTurnStatusEffects(state, actor, _eventEmitter);
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
                BattleCombatStatusTicker.TriggerDestabilizationExplosion(
                    state,
                    actor,
                    _eventEmitter,
                    skillId: string.Empty,
                    actorId: dotSourceCombatant?.Identity.Id ?? string.Empty);
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
        ResolveSkillActionCore(state, action, isFollowUpInvocation: false);
    }

    /// <summary>
    /// True when the actor should keep acting (ChanceToNotEndTurn / BonusAction) without advancing initiative.
    /// Unity turn drivers should check this before incrementing ActorIndex.
    /// </summary>
    public static bool ShouldActorRetainTurn(Combatant actor) =>
        actor?.PassiveRuntime.ShouldRetainTurnForBonusAction == true;

    private void ResolveSkillActionCore(BattleState state, ChosenAction action, bool isFollowUpInvocation)
    {
        var actor = action.Actor;
        var skill = action.Skill;
        var selectedTarget = action.Target;
        var primaryTargets = SkillTargetResolver.ResolvePrimaryTargets(state, actor, skill, selectedTarget);
        primaryTargets = ApplyConfusionRetargetIfNeeded(state, actor, skill, primaryTargets);

        var actionUsedTargetId = primaryTargets.Count > 0
            ? primaryTargets[0].Identity.Id
            : action.Target.Identity.Id;

        _eventEmitter.Emit(
            state,
            BattleEventType.ActionUsed,
            actorId: actor.Identity.Id,
            targetId: actionUsedTargetId,
            skillId: skill.Id,
            element: skill.Element);

        var hasDirectDamage = skill.BaseDamage.Min != 0 ||
                              skill.BaseDamage.Max != 0 ||
                              skill.ComputeFromDebuffTypesOnTarget ||
                              skill.BonusDamagePerOwnToken.HasValue;
        var hasAppliedSkillWideEffects = false;
        Combatant? lastSuccessfulHitTarget = null;
        var hitCount = Math.Max(1, skill.HitCount);

        for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            foreach (var primaryTarget in primaryTargets)
            {
                if (primaryTarget.Health.IsDead && !skill.CanTargetDeadAllies)
                {
                    continue;
                }

                var result = hasDirectDamage
                    ? ResolveHitAndDamage(state, actor, primaryTarget, skill)
                    : new ResolveActionResult { IsHit = true, IsCrit = false, DamageApplied = 0 };

                EmitHitResolved(state, actor, primaryTarget, skill, result);
                if (!result.IsHit)
                {
                    continue;
                }

                _effectApplicator.ApplyEffects(
                    state,
                    actor,
                    primaryTarget,
                    skill,
                    includeDefaultScopedEffects: true,
                    includeNonDefaultScopedEffects: !hasAppliedSkillWideEffects);
                hasAppliedSkillWideEffects = true;
                _effectApplicator.ApplyPassiveExtraDotsAfterEnemySkill(state, actor, primaryTarget, skill);
                lastSuccessfulHitTarget = primaryTarget;
            }
        }

        if (lastSuccessfulHitTarget != null)
        {
            _effectApplicator.ApplyPostSkillPassiveExtras(state, actor, lastSuccessfulHitTarget, skill);
        }

        if (skill.GrantsBonusActionsToAllies)
        {
            foreach (var ally in state.GetAllCombatants()
                         .Where(combatant =>
                             !combatant.Health.IsDead &&
                             combatant.Position.Side == actor.Position.Side))
            {
                ally.Tokens.Add(TokenType.BonusAction, 1);
                ally.PassiveRuntime.ShouldRetainTurnForBonusAction = true;
                _eventEmitter.Emit(
                    state,
                    BattleEventType.TokenApplied,
                    actorId: actor.Identity.Id,
                    targetId: ally.Identity.Id,
                    skillId: skill.Id,
                    tokenType: TokenType.BonusAction.ToString(),
                    tokenDelta: 1);
            }
        }

        if (!isFollowUpInvocation &&
            skill.FollowUpSkillIds is { Count: > 0 })
        {
            foreach (var followUpSkillId in skill.FollowUpSkillIds)
            {
                if (!state.SkillsById.TryGetValue(followUpSkillId, out var followUpSkill))
                {
                    continue;
                }

                ResolveSkillActionCore(
                    state,
                    new ChosenAction
                    {
                        Actor = actor,
                        Target = selectedTarget,
                        Skill = followUpSkill,
                        ActionType = ActionType.Skill,
                    },
                    isFollowUpInvocation: true);
            }
        }

        if (action.ActionType == ActionType.Skill &&
            actor.Identity.Faction == Faction.Player &&
            !isFollowUpInvocation)
        {
            ApplyBattleCorruptionDelta(state, skill.CorruptionCost, actor.Identity.Id, skill.Id);
        }

        if (!isFollowUpInvocation)
        {
            var shouldRetainTurn = false;
            if (skill.ChanceToNotEndTurn > 0 && _random.NextDouble() < skill.ChanceToNotEndTurn)
            {
                actor.Tokens.Add(TokenType.BonusAction, 1);
                shouldRetainTurn = true;
            }

            if (actor.Tokens.GetStacks(TokenType.BonusAction) > 0)
            {
                shouldRetainTurn = true;
            }

            actor.PassiveRuntime.ShouldRetainTurnForBonusAction = shouldRetainTurn;

            if (!shouldRetainTurn)
            {
                BattleCombatStatusTicker.ApplyEndOfTurnStatusEffects(state, actor, _eventEmitter);
                state.PassiveBus.RaiseTurnEnded(state, actor);
            }
        }
    }

    private IReadOnlyList<Combatant> ApplyConfusionRetargetIfNeeded(
        BattleState state,
        Combatant actor,
        SkillDefinition skill,
        IReadOnlyList<Combatant> primaryTargets)
    {
        if (!actor.PassiveRuntime.ConfusionActiveThisTurn)
        {
            return primaryTargets;
        }

        if (!SkillTargetKindRules.DirectsPrimaryDamageAtEnemies(skill.TargetKind))
        {
            return primaryTargets;
        }

        if (_random.NextDouble() >= CombatStatusRules.ConfusionRetargetChance)
        {
            return primaryTargets;
        }

        var validEnemies = SkillTargetResolver.GetValidEnemyPool(state, actor);
        if (validEnemies.Count == 0)
        {
            return primaryTargets;
        }

        var randomEnemy = validEnemies[_random.Next(0, validEnemies.Count)];
        return SkillTargetResolver.ResolvePrimaryTargets(state, actor, skill, randomEnemy);
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

        var effectiveHitChance = CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            state,
            actor,
            target,
            skill);
        if (_random.NextDouble() > effectiveHitChance)
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
        var baseRollMin = skill.BaseDamage.Min;
        var baseRollMax = skill.BaseDamage.Max;
        var baseRollDamage = baseRollMax >= baseRollMin
            ? _random.Next(baseRollMin, baseRollMax + 1)
            : 0;
        baseRollDamage += CombatDamageCalculator.ComputeBonusDamageFromSkillTokens(actor, target, skill);

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

            BattleCombatStatusTicker.ConsumeTauntOnBeingHit(state, target, actor);
            BattleCombatStatusTicker.ApplyControlledInstabilityReflect(
                state,
                actor,
                target,
                _eventEmitter,
                skill.Id);
        }

        if (target.Health.CurrentHp <= 0 && !target.Health.IsDead && !IsAllyInfiniteHealthProtected(state, target))
        {
            target.Health.IsDead = true;
            _eventEmitter.Emit(state, BattleEventType.CombatantDied, targetId: target.Identity.Id);
            state.PassiveBus.RaiseCombatantSlain(state, actor, target);
            BattleCombatStatusTicker.TriggerDestabilizationExplosion(
                state,
                target,
                _eventEmitter,
                skill.Id,
                actor.Identity.Id);
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
