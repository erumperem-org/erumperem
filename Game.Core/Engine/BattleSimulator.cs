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
    private readonly CombatEventCollector _eventCollector;

    public BattleSimulator(IRandomSource random, CombatEventCollector eventCollector)
    {
        _random = random;
        _eventCollector = eventCollector;
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
        Emit(
            state,
            BattleEventType.BattleStarted,
            battleResult: string.Empty,
            passiveLoadoutCsv: state.GetPassiveLoadoutCsv());
    }

    /// <summary>Evento de fim com vencedor atual.</summary>
    public void EmitBattleEnded(BattleState state)
    {
        var winner = state.Winner?.ToString() ?? "None";
        Emit(state, BattleEventType.BattleEnded, battleResult: winner);
    }

    /// <summary>TurnStarted, passivas de início de turno, DOTs e stun. Devolve false se o actor não age (morto, stun, etc.).</summary>
    public bool TryPrepareActorTurn(BattleState state, Combatant actor)
    {
        if (actor.Health.IsDead || state.IsFinished)
        {
            return false;
        }

        Emit(state, BattleEventType.TurnStarted, actorId: actor.Identity.Id, battleResult: string.Empty);
        state.PassiveBus.RaiseTurnStarted(
            state,
            actor,
            (tokenType, stackDelta) => Emit(
                state,
                BattleEventType.TokenApplied,
                actorId: actor.Identity.Id,
                targetId: actor.Identity.Id,
                tokenType: tokenType.ToString(),
                tokenDelta: stackDelta,
                battleResult: string.Empty));
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
            if (damage > 0)
            {
                var hpPercentBeforeDot =
                    actor.Health.MaxHp <= 0 ? 0 : (double)actor.Health.CurrentHp / actor.Health.MaxHp;
                actor.Health.CurrentHp = Math.Max(0, actor.Health.CurrentHp - damage);
                var hpPercentAfterDot =
                    actor.Health.MaxHp <= 0 ? 0 : (double)actor.Health.CurrentHp / actor.Health.MaxHp;
                Emit(
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

            dot.RemainingTurns--;
            if (dot.RemainingTurns > 0)
            {
                actor.Dots.ActiveDots.Add(dot);
            }

            if (actor.Health.CurrentHp <= 0 && !actor.Health.IsDead)
            {
                actor.Health.IsDead = true;
                Emit(state, BattleEventType.CombatantDied, targetId: actor.Identity.Id);
                state.PassiveBus.RaiseCombatantSlain(state, dotSourceCombatant, actor);
                HandleCompaction(state, actor.Position.Side);
                break;
            }
        }
    }

    /// <summary>Escolha automática (AI / simulação headless).</summary>
    public ChosenAction? ChooseAiAction(BattleState state, Combatant actor)
    {
        var enemies = actor.Position.Side == Side.Allies ? state.Enemies : state.Allies;
        var availableTargets = enemies.Where(enemy => !enemy.Health.IsDead).ToList();
        if (availableTargets.Count == 0) return null;

        var availableSkills = actor.SkillLoadout.Skills
            .Where(id => state.SkillsById.ContainsKey(id))
            .Select(id => state.SkillsById[id])
            .Where(skill => IsSkillUsable(actor, skill))
            .Where(skill => actor.AI is null || skill.TargetKind == SkillTargetKind.Enemy)
            .ToList();

        if (availableSkills.Count == 0) return null;

        SkillDefinition selectedSkill;
        if (actor.AI?.DecisionPolicyId == "KillThenWeighted")
        {
            selectedSkill = ChooseEnemySkillForAi(state, actor, availableTargets, availableSkills);
        }
        else
        {
            selectedSkill = availableSkills[_random.Next(0, availableSkills.Count)];
        }

        Combatant? target;
        if (selectedSkill.TargetKind == SkillTargetKind.Self)
        {
            target = actor;
        }
        else if (selectedSkill.TargetKind == SkillTargetKind.Ally)
        {
            var roster = actor.Position.Side == Side.Allies ? state.Allies : state.Enemies;
            var allies = roster.Where(combatant => !combatant.Health.IsDead).ToList();
            target = SelectAllyTarget(actor, allies, selectedSkill);
            if (target is null) return null;
        }
        else
        {
            target = SelectTarget(actor, availableTargets, selectedSkill);
            if (target is null) return null;
        }

        return new ChosenAction
        {
            Actor = actor,
            Target = target,
            Skill = selectedSkill,
            ActionType = ActionType.Skill,
        };
    }

    private SkillDefinition ChooseEnemySkillForAi(
        BattleState state,
        Combatant actor,
        IReadOnlyList<Combatant> targets,
        IReadOnlyList<SkillDefinition> skills)
    {
        var lethalSkills = new List<SkillDefinition>();
        foreach (var skill in skills)
        {
            foreach (var target in targets)
            {
                var estimate = EstimateDamage(state, actor, target, skill);
                if (estimate >= target.Health.CurrentHp)
                {
                    lethalSkills.Add(skill);
                    break;
                }
            }
        }

        var skillPool = lethalSkills.Count > 0 ? lethalSkills : skills.ToList();

        // Roll per-skill chanceToUse so "especiais" (ex.: 0.20) entram no draw com baixa frequência.
        // Se ninguém passar, o pool inteiro vira fallback para a IA nunca ficar sem opção.
        var rolledCandidates = new List<SkillDefinition>(skillPool.Count);
        foreach (var skill in skillPool)
        {
            var chance = Math.Clamp(skill.ChanceToUse, 0.0, 1.0);
            if (_random.NextDouble() < chance)
            {
                rolledCandidates.Add(skill);
            }
        }

        var finalPool = rolledCandidates.Count > 0 ? rolledCandidates : skillPool;
        var pickedIndex = _random.Next(0, finalPool.Count);
        return finalPool[pickedIndex];
    }

    private Combatant? SelectAllyTarget(
        Combatant _actor,
        IReadOnlyList<Combatant> allies,
        SkillDefinition _skill)
    {
        var visible = allies
            .Where(ally => ally.Tokens.GetStacks(TokenType.Stealth) == 0)
            .ToList();
        if (visible.Count == 0) return null;

        return visible[_random.Next(0, visible.Count)];
    }

    private Combatant? SelectTarget(
        Combatant _actor,
        IReadOnlyList<Combatant> availableTargets,
        SkillDefinition _skill)
    {
        var tauntTargets = availableTargets.Where(enemy => enemy.Tokens.GetStacks(TokenType.Taunt) > 0).ToList();
        var candidateTargets = tauntTargets.Count > 0 ? tauntTargets : availableTargets.ToList();

        var visibleTargets = candidateTargets
            .Where(enemy => enemy.Tokens.GetStacks(TokenType.Stealth) == 0)
            .ToList();
        if (visibleTargets.Count == 0)
        {
            return null;
        }

        return visibleTargets[_random.Next(0, visibleTargets.Count)];
    }

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
        Emit(
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
            Emit(
                state,
                BattleEventType.HitResolved,
                actorId: actor.Identity.Id,
                targetId: target.Identity.Id,
                skillId: skill.Id,
                element: skill.Element,
                isHit: result.IsHit,
                isCrit: result.IsCrit,
                damageAmount: result.DamageApplied);

            if (result.IsHit)
            {
                ApplyEffects(state, actor, target, skill, result);
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

            Emit(
                state,
                BattleEventType.HitResolved,
                actorId: actor.Identity.Id,
                targetId: target.Identity.Id,
                skillId: skill.Id,
                element: skill.Element,
                isHit: result.IsHit,
                isCrit: result.IsCrit,
                damageAmount: result.DamageApplied);

            if (result.IsHit)
            {
                ApplyEffects(state, actor, target, skill, result);
            }
        }
        else
        {
            if (skill.BaseDamage.Max == 0 && skill.BaseDamage.Min == 0)
            {
                result = new ResolveActionResult { IsHit = true, IsCrit = false, DamageApplied = 0 };
                Emit(
                    state,
                    BattleEventType.HitResolved,
                    actorId: actor.Identity.Id,
                    targetId: target.Identity.Id,
                    skillId: skill.Id,
                    element: skill.Element,
                    isHit: true,
                    isCrit: false,
                    damageAmount: 0);
                ApplyEffects(state, actor, target, skill, result);
            }
            else
            {
                result = ResolveHitAndDamage(state, actor, target, skill);
                Emit(
                    state,
                    BattleEventType.HitResolved,
                    actorId: actor.Identity.Id,
                    targetId: target.Identity.Id,
                    skillId: skill.Id,
                    element: skill.Element,
                    isHit: result.IsHit,
                    isCrit: result.IsCrit,
                    damageAmount: result.DamageApplied);
                if (result.IsHit)
                {
                    ApplyEffects(state, actor, target, skill, result);
                }
            }
        }

        if (action.ActionType == ActionType.Skill && actor.Identity.Faction == Faction.Player)
        {
            ApplyBattleCorruptionDelta(state, skill.CorruptionCost, actor.Identity.Id, skill.Id);
        }

        state.PassiveBus.RaiseTurnEnded(state, actor);
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

        var isCrit = _random.NextDouble() < EffectiveCritChance(state, actor, target, skill);
        var damage = _random.Next(skill.BaseDamage.Min, skill.BaseDamage.Max + 1);
        var elementalMultiplier = GetElementalMultiplier(state, actor, target, skill);
        damage = (int)Math.Round(damage * elementalMultiplier);
        if (isCrit)
        {
            damage = (int)Math.Round(damage * CorruptionRules.BaseCriticalStrikeDamageMultiplier);
        }

        if (isCrit &&
            actor.Identity.Faction == Faction.Enemy &&
            target.Identity.Faction == Faction.Player)
        {
            var enemyCritTierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
            damage = (int)Math.Round(damage * enemyCritTierModifiers.EnemyCritDamageMultiplierAgainstPlayer);
        }

        damage = (int)Math.Round(damage * CorruptionDamageMultiplier(state, actor, target));
        var outgoingPassiveNotes = new List<PassiveCombatNote>();
        if (damage > 0 && target.Identity.Id != actor.Identity.Id)
        {
            var (outAcc, consumeImpeto, _) =
                state.PassiveBus.AccumulateOutgoingDamageModifiers(state, actor, target, skill, noteSink: outgoingPassiveNotes);
            damage = (int)Math.Round(damage * (1.0 + outAcc.OutgoingDamageAdditiveSum) * outAcc.OutgoingDamageMultiplicativeProduct);
            damage = Math.Max(0, damage);
            if (consumeImpeto)
            {
                actor.PassiveRuntime.ImpetoCleaveBonusPending = false;
            }
        }

        var incomingPassiveNotes = new List<PassiveCombatNote>();
        if (damage > 0)
        {
            var (incomingMult, _) =
                state.PassiveBus.AccumulateIncomingDamageMultiplier(state, target, noteSink: incomingPassiveNotes);
            damage = (int)Math.Round(damage * incomingMult);
            damage = Math.Max(0, damage);
        }

        damage = ApplyMitigation(state, target, damage);
        var targetHpBeforeHit = target.Health.CurrentHp;
        target.Health.CurrentHp = Math.Max(0, target.Health.CurrentHp - damage);

        Emit(
            state,
            BattleEventType.DamageApplied,
            actorId: actor.Identity.Id,
            targetId: target.Identity.Id,
            skillId: skill.Id,
            element: skill.Element,
            isHit: true,
            isCrit: isCrit,
            damageAmount: damage);

        foreach (var note in outgoingPassiveNotes)
        {
            EmitPassiveCombatNarrativeEvent(state, note, actor.Identity.Id, target.Identity.Id, skill.Id);
        }

        foreach (var note in incomingPassiveNotes)
        {
            EmitPassiveCombatNarrativeEvent(state, note, target.Identity.Id, target.Identity.Id, skill.Id);
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

        if (target.Health.CurrentHp <= 0 && !target.Health.IsDead)
        {
            target.Health.IsDead = true;
            Emit(state, BattleEventType.CombatantDied, targetId: target.Identity.Id);
            state.PassiveBus.RaiseCombatantSlain(state, actor, target);
            HandleCompaction(state, target.Position.Side);
        }

        state.PassiveBus.RaiseOutgoingHitSuccess(state, actor, target, skill, hit: true);

        return new ResolveActionResult
        {
            IsHit = true,
            IsCrit = isCrit,
            DamageApplied = damage,
        };
    }

    private void ApplyEffects(
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
                        if (string.Equals(effect.EffectScope, "AllAllies", StringComparison.OrdinalIgnoreCase))
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
                                Emit(
                                    state,
                                    BattleEventType.TokenApplied,
                                    actorId: actor.Identity.Id,
                                    targetId: ally.Identity.Id,
                                    skillId: skill.Id,
                                    tokenType: effect.Token.Value.ToString(),
                                    tokenDelta: stacks);
                            }
                        }
                        else if (string.Equals(effect.EffectScope, "Self", StringComparison.OrdinalIgnoreCase))
                        {
                            actor.Tokens.Add(effect.Token.Value, stacks);
                            state.PassiveBus.RaiseTokenStacksChanged(
                                state,
                                actor,
                                actor,
                                skill,
                                effect.Token.Value,
                                stacks);
                            Emit(
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
                            Emit(
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
                        var elementalMultiplier = GetElementalMultiplier(state, actor, target, skill);
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
                        Emit(
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
                        Emit(
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
                Emit(
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
                EmitPassiveCombatNarrativeEvent(state, note, actor.Identity.Id, actor.Identity.Id, skill.Id);
            }
        }

        var passiveExtraDotNotes = new List<PassiveCombatNote>();
        state.PassiveBus.ApplyPassiveExtraDotsAfterEnemySkill(
            state,
            actor,
            target,
            skill,
            GetElementalMultiplier(state, actor, target, skill),
            (defender, dotType) => EffectPassesResistance(defender, dotType, state),
            passiveExtraDotNotes);
        foreach (var note in passiveExtraDotNotes)
        {
            Emit(
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

    private static IEnumerable<Combatant> LivingSameSide(BattleState state, Combatant actor)
    {
        var roster = actor.Position.Side == Side.Allies ? state.Allies : state.Enemies;
        return roster.Where(combatant => !combatant.Health.IsDead);
    }

    private int ApplyMitigation(BattleState state, Combatant target, int damage)
    {
        if (target.Tokens.GetStacks(TokenType.BlockPlus) > 0)
        {
            target.Tokens.ConsumeOne(TokenType.BlockPlus);
            damage = (int)Math.Round(damage * state.BalanceConfig.BlockPlusDamageMultiplier);
        }
        else if (target.Tokens.GetStacks(TokenType.Block) > 0)
        {
            target.Tokens.ConsumeOne(TokenType.Block);
            damage = (int)Math.Round(damage * state.BalanceConfig.BlockDamageMultiplier);
        }

        return Math.Max(0, damage);
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

    private void HandleCompaction(BattleState state, Side side)
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

    private int EstimateDamage(BattleState state, Combatant actor, Combatant target, SkillDefinition skill)
    {
        var average = (skill.BaseDamage.Min + skill.BaseDamage.Max) / 2.0;
        var elementalMultiplier = GetElementalMultiplier(state, actor, target, skill);
        var damage = average * elementalMultiplier * CorruptionDamageMultiplier(state, actor, target);
        if (target.Identity.Id != actor.Identity.Id)
        {
            var (outAcc, _, _) = state.PassiveBus.AccumulateOutgoingDamageModifiers(
                state,
                actor,
                target,
                skill,
                notifyObservers: false);
            damage *= (1.0 + outAcc.OutgoingDamageAdditiveSum) * outAcc.OutgoingDamageMultiplicativeProduct;
        }

        damage *= state.PassiveBus.AccumulateIncomingDamageMultiplier(state, target, notifyObservers: false).Mult;
        return Math.Max(0, (int)Math.Round(damage));
    }

    private double CorruptionDamageMultiplier(BattleState state, Combatant actor, Combatant target)
    {
        var tierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
        if (actor.Identity.Faction == Faction.Player)
        {
            return tierModifiers.PlayerDamageDealtMultiplier;
        }

        if (target.Identity.Faction == Faction.Player)
        {
            return tierModifiers.PlayerDamageTakenMultiplier;
        }

        return 1.0;
    }

    private double EffectiveCritChance(BattleState state, Combatant actor, Combatant target, SkillDefinition skill)
    {
        var baseChance = skill.BaseCritChance + actor.Stats.CritChance;
        var tierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);

        if (actor.Identity.Faction == Faction.Player)
        {
            baseChance += tierModifiers.PlayerCritBonus;
        }

        if (target.Identity.Faction == Faction.Player)
        {
            baseChance += tierModifiers.EnemyCritBonusAgainstPlayer;
        }

        return Math.Clamp(baseChance, 0, 1);
    }

    private double GetElementalMultiplier(BattleState state, Combatant actor, Combatant target, SkillDefinition skill)
    {
        var attackElement = skill.Element == ElementType.None
            ? actor.ElementAffinity.Element
            : skill.Element;
        var defenseElement = target.ElementAffinity.Element;
        if (ElementTriangle.HasAdvantage(attackElement, defenseElement))
        {
            return state.BalanceConfig.ElementAdvantageMultiplier;
        }

        if (ElementTriangle.HasAdvantage(defenseElement, attackElement))
        {
            return state.BalanceConfig.ElementDisadvantageMultiplier;
        }

        return 1.0;
    }

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

        Emit(
            state,
            BattleEventType.CorruptionAdjusted,
            actorId: actorId,
            skillId: skillId,
            corruptionDelta: delta,
            previousCorruptionTier: tierBeforeAdjustment);
    }

    private void EmitPassiveCombatNarrativeEvent(
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

    private void Emit(
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
