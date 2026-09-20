using Game.Core.Abstractions;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;

namespace Game.Core.Passives;

/// <summary>
/// Evaluates authored <see cref="PassiveConditionDefinition"/> / <see cref="PassiveEffectDefinition"/> lists.
/// Legacy <see cref="PassiveEffectKind"/> passives stay on <see cref="PassiveRuleApplier"/> (Horse Boss summon and synthetic tests).
/// </summary>
public static class PassiveDataDrivenEngine
{
    private const int MaximumNestedEvaluations = 8;
    private static int EvaluationDepth;

    public readonly record struct PermanentStatModifiers(
        double DamageCausedAdditive,
        double DefenseChanceAdditive,
        double AccuracyAdditive,
        double CritChanceAdditive,
        double CritDamageAdditive,
        double SkillDamageFlat,
        double SkillAccuracyAdditive,
        int SkillHitCountAdditive,
        double SkillChanceToNotEndTurnAdditive,
        double SkillHealEffectivenessAdditive,
        double SkillHealDoubleChanceAdditive);

    public static PermanentStatModifiers GetPermanentStatModifiers(
        BattleState state,
        Combatant owner,
        Combatant? oppositeCombatant,
        SkillDefinition? skill)
    {
        var damageCausedAdditive = owner.PassiveRuntime.BattleDamageCausedAdditive;
        var defenseChanceAdditive = owner.PassiveRuntime.BattleDefenseChanceAdditive +
                                    owner.PassiveRuntime.UntilOpposingSideTurnEndDefenseChanceAdditive;
        var accuracyAdditive = owner.PassiveRuntime.BattleAccuracyAdditive;
        var critChanceAdditive = owner.PassiveRuntime.BattleCritChanceAdditive;
        var critDamageAdditive = owner.PassiveRuntime.BattleCritDamageAdditive;
        var skillDamageFlat = 0.0;
        var skillAccuracyAdditive = 0.0;
        var skillHitCountAdditive = 0;
        var skillChanceToNotEndTurnAdditive = 0.0;
        var skillHealEffectivenessAdditive = 0.0;
        var skillHealDoubleChanceAdditive = 0.0;

        if (skill != null)
        {
            if (owner.PassiveRuntime.SkillDamageFlatBonusBySkillId.TryGetValue(skill.Id, out var storedDamage))
            {
                skillDamageFlat += storedDamage;
            }

            if (owner.PassiveRuntime.SkillAccuracyAdditiveBySkillId.TryGetValue(skill.Id, out var storedAccuracy))
            {
                skillAccuracyAdditive += storedAccuracy;
            }
        }

        foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(owner, state))
        {
            if (!passiveDefinition.HasDataDrivenEffects)
            {
                continue;
            }

            foreach (var condition in GetMatchingConditions(
                         passiveDefinition,
                         PassiveActivationKind.Permanent,
                         owner,
                         oppositeCombatant,
                         skill,
                         damageAmount: 0,
                         tokenType: null,
                         tokenDelta: 0,
                         hpPercentBefore: null,
                         hpPercentAfter: null))
            {
                AccumulateContinuousEffects(
                    state,
                    owner,
                    oppositeCombatant,
                    skill,
                    passiveDefinition,
                    ref damageCausedAdditive,
                    ref defenseChanceAdditive,
                    ref accuracyAdditive,
                    ref critChanceAdditive,
                    ref critDamageAdditive,
                    ref skillDamageFlat,
                    ref skillAccuracyAdditive,
                    ref skillHitCountAdditive,
                    ref skillChanceToNotEndTurnAdditive,
                    ref skillHealEffectivenessAdditive,
                    ref skillHealDoubleChanceAdditive);
                break;
            }

            foreach (var condition in GetMatchingConditions(
                         passiveDefinition,
                         PassiveActivationKind.WhileHavingStatus,
                         owner,
                         oppositeCombatant,
                         skill,
                         damageAmount: 0,
                         tokenType: null,
                         tokenDelta: 0,
                         hpPercentBefore: null,
                         hpPercentAfter: null))
            {
                AccumulateContinuousEffects(
                    state,
                    owner,
                    oppositeCombatant,
                    skill,
                    passiveDefinition,
                    ref damageCausedAdditive,
                    ref defenseChanceAdditive,
                    ref accuracyAdditive,
                    ref critChanceAdditive,
                    ref critDamageAdditive,
                    ref skillDamageFlat,
                    ref skillAccuracyAdditive,
                    ref skillHitCountAdditive,
                    ref skillChanceToNotEndTurnAdditive,
                    ref skillHealEffectivenessAdditive,
                    ref skillHealDoubleChanceAdditive);
                break;
            }

            foreach (var oppositeStatusCondition in GetMatchingConditions(
                         passiveDefinition,
                         PassiveActivationKind.WhileOppositeHasStatus,
                         owner,
                         oppositeCombatant,
                         skill,
                         damageAmount: 0,
                         tokenType: null,
                         tokenDelta: 0,
                         hpPercentBefore: null,
                         hpPercentAfter: null))
            {
                AccumulateContinuousEffects(
                    state,
                    owner,
                    oppositeCombatant,
                    skill,
                    passiveDefinition,
                    ref damageCausedAdditive,
                    ref defenseChanceAdditive,
                    ref accuracyAdditive,
                    ref critChanceAdditive,
                    ref critDamageAdditive,
                    ref skillDamageFlat,
                    ref skillAccuracyAdditive,
                    ref skillHitCountAdditive,
                    ref skillChanceToNotEndTurnAdditive,
                    ref skillHealEffectivenessAdditive,
                    ref skillHealDoubleChanceAdditive);
                break;
            }
        }

        AccumulateAllyAuraContinuousEffects(
            state,
            owner,
            oppositeCombatant,
            skill,
            ref damageCausedAdditive,
            ref defenseChanceAdditive,
            ref accuracyAdditive,
            ref critChanceAdditive,
            ref critDamageAdditive,
            ref skillDamageFlat,
            ref skillAccuracyAdditive,
            ref skillHitCountAdditive,
            ref skillChanceToNotEndTurnAdditive,
            ref skillHealEffectivenessAdditive,
            ref skillHealDoubleChanceAdditive);

        return new PermanentStatModifiers(
            damageCausedAdditive,
            defenseChanceAdditive,
            accuracyAdditive,
            critChanceAdditive,
            critDamageAdditive,
            skillDamageFlat,
            skillAccuracyAdditive,
            skillHitCountAdditive,
            skillChanceToNotEndTurnAdditive,
            skillHealEffectivenessAdditive,
            skillHealDoubleChanceAdditive);
    }

    public static double GetTokenEfficiencyMultiplier(
        BattleState state,
        Combatant combatant,
        TokenType tokenType,
        Combatant? oppositeCombatant = null)
    {
        var efficiencyAdditive = 0.0;
        if (combatant.PassiveRuntime.TokenEfficiencyAdditiveByToken.TryGetValue(tokenType, out var stored))
        {
            efficiencyAdditive += stored;
        }

        foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(combatant, state))
        {
            if (!passiveDefinition.HasDataDrivenEffects)
            {
                continue;
            }

            if (!HasContinuousActivation(passiveDefinition, combatant, oppositeCombatant))
            {
                continue;
            }

            efficiencyAdditive += SumTokenStatChangeMagnitude(
                combatant,
                oppositeCombatant,
                passiveDefinition,
                tokenType,
                includeOppositeTargetedEffects: false);
        }

        if (oppositeCombatant != null)
        {
            foreach (var applierPassiveDefinition in PassiveRuleApplier.EnumerateActivePassives(oppositeCombatant, state))
            {
                if (!applierPassiveDefinition.HasDataDrivenEffects)
                {
                    continue;
                }

                if (!HasContinuousActivation(applierPassiveDefinition, oppositeCombatant, combatant))
                {
                    continue;
                }

                efficiencyAdditive += SumTokenStatChangeMagnitude(
                    oppositeCombatant,
                    combatant,
                    applierPassiveDefinition,
                    tokenType,
                    includeOppositeTargetedEffects: true);
            }
        }

        return 1.0 + efficiencyAdditive;
    }

    private static double SumTokenStatChangeMagnitude(
        Combatant owner,
        Combatant? oppositeCombatant,
        PassiveDefinition passiveDefinition,
        TokenType tokenType,
        bool includeOppositeTargetedEffects)
    {
        var efficiencyAdditive = 0.0;
        foreach (var effect in passiveDefinition.Effects)
        {
            if (effect.Operation != PassiveEffectOperationKind.TokenStatChange)
            {
                continue;
            }

            var isOppositeTargeted = effect.StatChangeTarget == PassiveStatChangeTarget.Opposite;
            if (includeOppositeTargetedEffects != isOppositeTargeted)
            {
                continue;
            }

            if (effect.Token is not null && effect.Token.Value != tokenType)
            {
                continue;
            }

            efficiencyAdditive += effect.Magnitude * ReadResourceScaleOrOne(owner, oppositeCombatant, effect);
        }

        return efficiencyAdditive;
    }

    public static double GetTokenEndOfTurnDecaySkipChance(
        BattleState state,
        Combatant combatant,
        TokenType tokenType)
    {
        var skipChance = 0.0;
        foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(combatant, state))
        {
            if (!passiveDefinition.HasDataDrivenEffects)
            {
                continue;
            }

            if (!HasContinuousActivation(passiveDefinition, combatant, oppositeCombatant: null))
            {
                continue;
            }

            foreach (var effect in passiveDefinition.Effects)
            {
                if (effect.SkipEndOfTurnDecayChance <= 0)
                {
                    continue;
                }

                if (effect.Token is not null && effect.Token.Value != tokenType)
                {
                    continue;
                }

                skipChance += effect.SkipEndOfTurnDecayChance;
            }
        }

        return Math.Clamp(skipChance, 0.0, 1.0);
    }

    public static Func<TokenType, double> CreateTokenEfficiencyLookup(
        BattleState state,
        Combatant combatant,
        Combatant? oppositeCombatant = null) =>
        tokenType => GetTokenEfficiencyMultiplier(state, combatant, tokenType, oppositeCombatant);

    public static SkillTargetKind GetEffectiveSkillTargetKind(
        BattleState state,
        Combatant owner,
        SkillDefinition skill)
    {
        var targetKind = skill.TargetKind;
        foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(owner, state))
        {
            if (!passiveDefinition.HasDataDrivenEffects)
            {
                continue;
            }

            if (!HasContinuousActivation(passiveDefinition, owner, oppositeCombatant: null))
            {
                continue;
            }

            foreach (var effect in passiveDefinition.Effects)
            {
                if (effect.Operation != PassiveEffectOperationKind.SkillStatChange ||
                    effect.SkillStat != PassiveSkillStatKind.TargetKind)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(effect.SkillId) &&
                    !string.Equals(effect.SkillId, skill.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                var coercedTargetKind = (int)Math.Round(effect.Magnitude);
                if (Enum.IsDefined(typeof(SkillTargetKind), coercedTargetKind))
                {
                    targetKind = (SkillTargetKind)coercedTargetKind;
                }
            }
        }

        return targetKind;
    }

    public static void HandleActivation(
        BattleState state,
        Combatant owner,
        PassiveActivationKind activation,
        CombatPassiveEventContext context)
    {
        if (EvaluationDepth >= MaximumNestedEvaluations)
        {
            return;
        }

        EvaluationDepth++;
        try
        {
            foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(owner, state))
            {
                if (!passiveDefinition.HasDataDrivenEffects)
                {
                    continue;
                }

                var matchingConditions = GetMatchingConditions(
                    passiveDefinition,
                    activation,
                    owner,
                    ResolveOppositeCombatant(owner, context),
                    context.Skill,
                    context.DamageAmount,
                    context.TokenType,
                    context.TokenDelta,
                    context.HpPercentBefore,
                    context.HpPercentAfter,
                    eventHitter: context.Killer);
                if (matchingConditions.Count == 0)
                {
                    continue;
                }

                if (!TryConsumePassiveTriggerBudget(state, owner, passiveDefinition))
                {
                    continue;
                }

                var repeatCount = ResolveUponDamageTakenRepeatCount(
                    owner,
                    activation,
                    matchingConditions,
                    passiveDefinition.Id);
                for (var repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                {
                    foreach (var effect in passiveDefinition.Effects)
                    {
                        TryApplyEffect(state, owner, context, passiveDefinition, effect);
                    }
                }
            }
        }
        finally
        {
            EvaluationDepth--;
        }
    }

    public static void RegisterDamageTakenTowardEveryXHitPoints(
        Combatant owner,
        int damageAmount)
    {
        if (damageAmount <= 0)
        {
            return;
        }

        foreach (var entry in owner.Progression.UnlockedNodes)
        {
            if (!entry.Value)
            {
                continue;
            }

            if (!owner.PassiveRuntime.HitPointsLostAccumulatorByPassiveId.TryGetValue(entry.Key, out var current))
            {
                current = 0;
            }

            owner.PassiveRuntime.HitPointsLostAccumulatorByPassiveId[entry.Key] = current + damageAmount;
        }
    }

    private static void AccumulateContinuousEffects(
        BattleState state,
        Combatant owner,
        Combatant? oppositeCombatant,
        SkillDefinition? skill,
        PassiveDefinition passiveDefinition,
        ref double damageCausedAdditive,
        ref double defenseChanceAdditive,
        ref double accuracyAdditive,
        ref double critChanceAdditive,
        ref double critDamageAdditive,
                    ref double skillDamageFlat,
        ref double skillAccuracyAdditive,
        ref int skillHitCountAdditive,
        ref double skillChanceToNotEndTurnAdditive,
        ref double skillHealEffectivenessAdditive,
        ref double skillHealDoubleChanceAdditive)
    {
        foreach (var effect in passiveDefinition.Effects)
        {
            switch (effect.Operation)
            {
                case PassiveEffectOperationKind.CharacterStatChange:
                    if (effect.ExpiresAtEndOfOpposingSideTurn)
                    {
                        break;
                    }

                    ApplyContinuousCharacterStat(
                        effect,
                        ref damageCausedAdditive,
                        ref defenseChanceAdditive,
                        ref accuracyAdditive,
                        ref critChanceAdditive,
                        ref critDamageAdditive);
                    break;
                case PassiveEffectOperationKind.SkillStatChange:
                    if (skill == null &&
                        effect.SkillStat is not PassiveSkillStatKind.HealEffectiveness
                            and not PassiveSkillStatKind.HealDoubleChance)
                    {
                        break;
                    }

                    if (!string.IsNullOrEmpty(effect.SkillId) &&
                        (skill == null || !string.Equals(effect.SkillId, skill.Id, StringComparison.Ordinal)))
                    {
                        break;
                    }

                    if (effect.SkillStat == PassiveSkillStatKind.Damage)
                    {
                        skillDamageFlat += effect.Magnitude;
                    }
                    else if (effect.SkillStat == PassiveSkillStatKind.Accuracy)
                    {
                        skillAccuracyAdditive += effect.Magnitude;
                    }
                    else if (effect.SkillStat == PassiveSkillStatKind.CriticalChance)
                    {
                        critChanceAdditive += effect.Magnitude;
                    }
                    else if (effect.SkillStat == PassiveSkillStatKind.HitCount)
                    {
                        skillHitCountAdditive += (int)Math.Round(effect.Magnitude);
                    }
                    else if (effect.SkillStat == PassiveSkillStatKind.ChanceToNotEndTurn)
                    {
                        skillChanceToNotEndTurnAdditive += effect.Magnitude;
                    }
                    else if (effect.SkillStat == PassiveSkillStatKind.HealEffectiveness)
                    {
                        skillHealEffectivenessAdditive += effect.Magnitude;
                    }
                    else if (effect.SkillStat == PassiveSkillStatKind.HealDoubleChance)
                    {
                        skillHealDoubleChanceAdditive += effect.Magnitude;
                    }

                    break;
                case PassiveEffectOperationKind.ExtraStatsFromResource:
                    ApplyContinuousExtraStatsFromResource(
                        owner,
                        oppositeCombatant,
                        skill,
                        effect,
                        ref damageCausedAdditive,
                        ref defenseChanceAdditive,
                        ref accuracyAdditive,
                        ref critChanceAdditive,
                        ref critDamageAdditive,
                        ref skillDamageFlat);
                    break;
            }
        }
    }

    private static void AccumulateAllyAuraContinuousEffects(
        BattleState state,
        Combatant beneficiary,
        Combatant? oppositeCombatant,
        SkillDefinition? skill,
        ref double damageCausedAdditive,
        ref double defenseChanceAdditive,
        ref double accuracyAdditive,
        ref double critChanceAdditive,
        ref double critDamageAdditive,
        ref double skillDamageFlat,
        ref double skillAccuracyAdditive,
        ref int skillHitCountAdditive,
        ref double skillChanceToNotEndTurnAdditive,
        ref double skillHealEffectivenessAdditive,
        ref double skillHealDoubleChanceAdditive)
    {
        foreach (var ally in LivingSameSide(state, beneficiary))
        {
            if (string.Equals(ally.Identity.Id, beneficiary.Identity.Id, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var passiveDefinition in PassiveRuleApplier.EnumerateActivePassives(ally, state))
            {
                if (!passiveDefinition.HasDataDrivenEffects || !HasTeamAuraEffects(passiveDefinition))
                {
                    continue;
                }

                var hasMatchingAuraCondition =
                    GetMatchingConditions(
                        passiveDefinition,
                        PassiveActivationKind.Permanent,
                        beneficiary,
                        oppositeCombatant,
                        skill,
                        damageAmount: 0,
                        tokenType: null,
                        tokenDelta: 0,
                        hpPercentBefore: null,
                        hpPercentAfter: null).Count > 0 ||
                    GetMatchingConditions(
                        passiveDefinition,
                        PassiveActivationKind.WhileHavingStatus,
                        beneficiary,
                        oppositeCombatant,
                        skill,
                        damageAmount: 0,
                        tokenType: null,
                        tokenDelta: 0,
                        hpPercentBefore: null,
                        hpPercentAfter: null).Count > 0;
                if (!hasMatchingAuraCondition)
                {
                    continue;
                }

                AccumulateContinuousEffects(
                    state,
                    beneficiary,
                    oppositeCombatant,
                    skill,
                    passiveDefinition,
                    ref damageCausedAdditive,
                    ref defenseChanceAdditive,
                    ref accuracyAdditive,
                    ref critChanceAdditive,
                    ref critDamageAdditive,
                    ref skillDamageFlat,
                    ref skillAccuracyAdditive,
                    ref skillHitCountAdditive,
                    ref skillChanceToNotEndTurnAdditive,
                    ref skillHealEffectivenessAdditive,
                    ref skillHealDoubleChanceAdditive);
            }
        }
    }

    private static bool HasTeamAuraEffects(PassiveDefinition passiveDefinition)
    {
        foreach (var effect in passiveDefinition.Effects)
        {
            if (effect.StatChangeTarget is PassiveStatChangeTarget.SelfAndAlly or PassiveStatChangeTarget.Ally)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesTurnStartStatThreshold(Combatant owner, PassiveConditionDefinition condition)
    {
        if (condition.StatThresholdFraction <= 0)
        {
            return true;
        }

        if (owner.Health == null || owner.Health.MaxHp <= 0)
        {
            return false;
        }

        var hpFraction = (double)owner.Health.CurrentHp / owner.Health.MaxHp;
        return condition.StatThresholdComparison == PassiveStatThresholdComparison.AboveOrEqual
            ? hpFraction >= condition.StatThresholdFraction
            : hpFraction <= condition.StatThresholdFraction;
    }

    private static void ApplyContinuousCharacterStat(
        PassiveEffectDefinition effect,
        ref double damageCausedAdditive,
        ref double defenseChanceAdditive,
        ref double accuracyAdditive,
        ref double critChanceAdditive,
        ref double critDamageAdditive)
    {
        switch (effect.CharacterStat)
        {
            case PassiveCharacterStatKind.DamageCaused:
                damageCausedAdditive += effect.Magnitude;
                break;
            case PassiveCharacterStatKind.DefenseChance:
                defenseChanceAdditive += effect.Magnitude;
                break;
            case PassiveCharacterStatKind.Accuracy:
                accuracyAdditive += effect.Magnitude;
                break;
            case PassiveCharacterStatKind.CriticalChance:
                critChanceAdditive += effect.Magnitude;
                break;
            case PassiveCharacterStatKind.CriticalDamage:
                critDamageAdditive += effect.Magnitude;
                break;
        }
    }

    private static void ApplyContinuousExtraStatsFromResource(
        Combatant owner,
        Combatant? oppositeCombatant,
        SkillDefinition? skill,
        PassiveEffectDefinition effect,
        ref double damageCausedAdditive,
        ref double defenseChanceAdditive,
        ref double accuracyAdditive,
        ref double critChanceAdditive,
        ref double critDamageAdditive,
        ref double skillDamageFlat)
    {
        var scaled = effect.Magnitude * ReadResourceMagnitude(owner, oppositeCombatant, effect);
        if (effect.SkillStat == PassiveSkillStatKind.Damage)
        {
            if (skill == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(effect.SkillId) &&
                !string.Equals(effect.SkillId, skill.Id, StringComparison.Ordinal))
            {
                return;
            }

            skillDamageFlat += scaled;
            return;
        }

        var scaledEffect = new PassiveEffectDefinition
        {
            Operation = PassiveEffectOperationKind.CharacterStatChange,
            CharacterStat = effect.CharacterStat == PassiveCharacterStatKind.None
                ? PassiveCharacterStatKind.DamageCaused
                : effect.CharacterStat,
            Magnitude = scaled,
        };
        ApplyContinuousCharacterStat(
            scaledEffect,
            ref damageCausedAdditive,
            ref defenseChanceAdditive,
            ref accuracyAdditive,
            ref critChanceAdditive,
            ref critDamageAdditive);
    }

    private static double ReadResourceScaleOrOne(
        Combatant owner,
        Combatant? oppositeCombatant,
        PassiveEffectDefinition effect)
    {
        if (effect.Resource == PassiveResourceKind.None)
        {
            return 1.0;
        }

        return ReadResourceMagnitude(owner, oppositeCombatant, effect);
    }

    private static double ReadResourceMagnitude(
        Combatant owner,
        Combatant? oppositeCombatant,
        PassiveEffectDefinition effect)
    {
        var chunkSize = Math.Max(1, effect.Stacks);
        var resourceToken = effect.ResourceToken ?? effect.Token;
        return effect.Resource switch
        {
            PassiveResourceKind.MissingHitPointsFraction =>
                owner.Health.MaxHp <= 0
                    ? 0
                    : 1.0 - ((double)owner.Health.CurrentHp / owner.Health.MaxHp),
            PassiveResourceKind.CurrentHitPointsFraction =>
                owner.Health.MaxHp <= 0
                    ? 0
                    : (double)owner.Health.CurrentHp / owner.Health.MaxHp,
            PassiveResourceKind.MissingHitPointsChunks =>
                owner.Health.MaxHp <= 0
                    ? 0
                    : Math.Floor(Math.Max(0, owner.Health.MaxHp - owner.Health.CurrentHp) / (double)chunkSize),
            PassiveResourceKind.TokenStacks when resourceToken is not null =>
                owner.Tokens.GetStacks(resourceToken.Value),
            PassiveResourceKind.TokenStacksChunks when resourceToken is not null =>
                Math.Floor(owner.Tokens.GetStacks(resourceToken.Value) / (double)chunkSize),
            PassiveResourceKind.OppositeTokenStacks when resourceToken is not null && oppositeCombatant != null =>
                oppositeCombatant.Tokens.GetStacks(resourceToken.Value),
            PassiveResourceKind.EnemiesDefeatedThisBattle =>
                owner.PassiveRuntime.EnemiesDefeatedThisBattle,
            _ => 0,
        };
    }

    private static bool HasContinuousActivation(
        PassiveDefinition passiveDefinition,
        Combatant owner,
        Combatant? oppositeCombatant)
    {
        if (passiveDefinition.Conditions.Count == 0)
        {
            return true;
        }

        foreach (var condition in passiveDefinition.Conditions)
        {
            if (condition.Activation == PassiveActivationKind.Permanent)
            {
                return true;
            }

            if (condition.Activation == PassiveActivationKind.WhileHavingStatus &&
                MatchesRequiredStatus(owner, condition, tokenType: null, tokenDelta: 0, requireEventToken: false))
            {
                return true;
            }

            if (condition.Activation == PassiveActivationKind.WhileOppositeHasStatus &&
                oppositeCombatant != null &&
                MatchesRequiredStatus(
                    oppositeCombatant,
                    condition,
                    tokenType: null,
                    tokenDelta: 0,
                    requireEventToken: false))
            {
                return true;
            }
        }

        return false;
    }

    private static List<PassiveConditionDefinition> GetMatchingConditions(
        PassiveDefinition passiveDefinition,
        PassiveActivationKind activation,
        Combatant owner,
        Combatant? oppositeCombatant,
        SkillDefinition? skill,
        int damageAmount,
        TokenType? tokenType,
        int tokenDelta,
        double? hpPercentBefore,
        double? hpPercentAfter,
        Combatant? eventHitter = null)
    {
        var matches = new List<PassiveConditionDefinition>();
        var conditions = passiveDefinition.Conditions.Count > 0
            ? passiveDefinition.Conditions
            : [new PassiveConditionDefinition { Activation = PassiveActivationKind.Permanent }];

        foreach (var condition in conditions)
        {
            if (condition.Activation != activation)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(condition.SkillId) &&
                (skill == null || !string.Equals(condition.SkillId, skill.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            if (!ConditionMatches(
                    condition,
                    owner,
                    oppositeCombatant,
                    damageAmount,
                    tokenType,
                    tokenDelta,
                    hpPercentBefore,
                    hpPercentAfter,
                    passiveDefinition.Id,
                    eventHitter))
            {
                continue;
            }

            matches.Add(condition);
        }

        return matches;
    }

    private static bool ConditionMatches(
        PassiveConditionDefinition condition,
        Combatant owner,
        Combatant? oppositeCombatant,
        int damageAmount,
        TokenType? tokenType,
        int tokenDelta,
        double? hpPercentBefore,
        double? hpPercentAfter,
        string passiveId,
        Combatant? eventHitter = null)
    {
        switch (condition.Activation)
        {
            case PassiveActivationKind.Permanent:
                return true;
            case PassiveActivationKind.WhileHavingStatus:
                return MatchesRequiredStatus(owner, condition, tokenType, tokenDelta, requireEventToken: false);
            case PassiveActivationKind.WhileOppositeHasStatus:
                return oppositeCombatant != null &&
                       MatchesRequiredStatus(
                           oppositeCombatant,
                           condition,
                           tokenType: null,
                           tokenDelta: 0,
                           requireEventToken: false);
            case PassiveActivationKind.UponDamageTaken:
                if (damageAmount <= 0)
                {
                    return false;
                }

                if (condition.HitPointsLostPerTrigger > 0 &&
                    !HasEnoughHitPointsLost(owner, condition, passiveId))
                {
                    return false;
                }

                if (condition.RequiredStatus is not null)
                {
                    return oppositeCombatant != null &&
                           oppositeCombatant.Tokens.GetStacks(condition.RequiredStatus.Value) > 0;
                }

                return true;
            case PassiveActivationKind.UponDealingDamage:
                if (damageAmount <= 0)
                {
                    return false;
                }

                if (condition.RequiredStatus is not null)
                {
                    return oppositeCombatant != null &&
                           oppositeCombatant.Tokens.GetStacks(condition.RequiredStatus.Value) > 0;
                }

                if (condition.StatusMatch == PassiveStatusMatchKind.AnyBuff)
                {
                    return HasAnyMatchingToken(owner, CombatStatusRules.IsBuffToken);
                }

                return true;
            case PassiveActivationKind.UponAllyDamageTaken:
                return damageAmount > 0;
            case PassiveActivationKind.UponAllyDealingDamage:
                if (damageAmount <= 0)
                {
                    return false;
                }

                if (condition.StatusMatch == PassiveStatusMatchKind.AnyBuff)
                {
                    var hitter = eventHitter ?? owner;
                    return HasAnyMatchingToken(hitter, CombatStatusRules.IsBuffToken);
                }

                return true;
            case PassiveActivationKind.UponKill:
            case PassiveActivationKind.UponCriticalStrike:
            case PassiveActivationKind.UponUsingSkill:
            case PassiveActivationKind.OnTurnEnd:
            case PassiveActivationKind.UponHealing:
                return true;
            case PassiveActivationKind.OnTurnStart:
                return MatchesTurnStartStatThreshold(owner, condition);
            case PassiveActivationKind.UponApplyingStatus:
            case PassiveActivationKind.UponReceivingStatus:
                return tokenDelta > 0 &&
                       MatchesRequiredStatus(owner, condition, tokenType, tokenDelta, requireEventToken: true);
            case PassiveActivationKind.UponHittingTargetWithStatus:
                return oppositeCombatant != null &&
                       MatchesRequiredStatus(
                           oppositeCombatant,
                           condition,
                           tokenType: null,
                           tokenDelta: 0,
                           requireEventToken: false);
            case PassiveActivationKind.UponStatThreshold:
                return MatchesStatThreshold(condition, hpPercentBefore, hpPercentAfter);
            default:
                return false;
        }
    }

    private static int ResolveUponDamageTakenRepeatCount(
        Combatant owner,
        PassiveActivationKind activation,
        IReadOnlyList<PassiveConditionDefinition> matchingConditions,
        string passiveId)
    {
        if (activation != PassiveActivationKind.UponDamageTaken)
        {
            return 1;
        }

        var chunkCondition = matchingConditions.FirstOrDefault(condition => condition.HitPointsLostPerTrigger > 0);
        if (chunkCondition == null)
        {
            return 1;
        }

        if (!owner.PassiveRuntime.HitPointsLostAccumulatorByPassiveId.TryGetValue(passiveId, out var accumulated))
        {
            return 0;
        }

        var chunkSize = chunkCondition.HitPointsLostPerTrigger;
        var repeatCount = accumulated / chunkSize;
        owner.PassiveRuntime.HitPointsLostAccumulatorByPassiveId[passiveId] = accumulated % chunkSize;
        return Math.Max(0, repeatCount);
    }

    private static bool HasEnoughHitPointsLost(
        Combatant owner,
        PassiveConditionDefinition condition,
        string passiveId)
    {
        if (condition.HitPointsLostPerTrigger <= 0)
        {
            return true;
        }

        return owner.PassiveRuntime.HitPointsLostAccumulatorByPassiveId.TryGetValue(passiveId, out var accumulated) &&
               accumulated >= condition.HitPointsLostPerTrigger;
    }

    private static bool MatchesStatThreshold(
        PassiveConditionDefinition condition,
        double? hpPercentBefore,
        double? hpPercentAfter)
    {
        if (condition.StatThresholdFraction <= 0 || hpPercentBefore is null || hpPercentAfter is null)
        {
            return false;
        }

        var threshold = condition.StatThresholdFraction;
        if (condition.StatThresholdComparison == PassiveStatThresholdComparison.AboveOrEqual)
        {
            return hpPercentBefore.Value < threshold && hpPercentAfter.Value >= threshold;
        }

        return hpPercentBefore.Value > threshold && hpPercentAfter.Value <= threshold;
    }

    private static bool MatchesRequiredStatus(
        Combatant combatant,
        PassiveConditionDefinition condition,
        TokenType? tokenType,
        int tokenDelta,
        bool requireEventToken)
    {
        if (requireEventToken)
        {
            if (tokenType is null || tokenDelta <= 0)
            {
                return false;
            }

            if (condition.RequiredStatus is not null && condition.RequiredStatus.Value != tokenType.Value)
            {
                return false;
            }

            return MatchesStatusKind(condition.StatusMatch, tokenType.Value, combatant);
        }

        if (condition.RequiredStatus is not null)
        {
            return combatant.Tokens.GetStacks(condition.RequiredStatus.Value) > 0;
        }

        return condition.StatusMatch switch
        {
            PassiveStatusMatchKind.AnyDebuff => HasAnyMatchingToken(combatant, CombatStatusRules.IsDebuffToken),
            PassiveStatusMatchKind.AnyBuff => HasAnyMatchingToken(combatant, CombatStatusRules.IsBuffToken),
            PassiveStatusMatchKind.AnyStatus => combatant.Tokens.Entries.Any(entry => entry.Stacks > 0),
            _ => true,
        };
    }

    private static bool MatchesStatusKind(
        PassiveStatusMatchKind statusMatch,
        TokenType tokenType,
        Combatant combatant)
    {
        return statusMatch switch
        {
            PassiveStatusMatchKind.AnyDebuff => CombatStatusRules.IsDebuffToken(tokenType),
            PassiveStatusMatchKind.AnyBuff => CombatStatusRules.IsBuffToken(tokenType),
            _ => true,
        };
    }

    private static bool HasAnyMatchingToken(Combatant combatant, Func<TokenType, bool> predicate)
    {
        foreach (var entry in combatant.Tokens.Entries)
        {
            if (entry.Stacks > 0 && predicate(entry.Type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryConsumePassiveTriggerBudget(
        BattleState state,
        Combatant owner,
        PassiveDefinition passiveDefinition)
    {
        if (passiveDefinition.ChanceToTrigger < 1.0 &&
            state.PassiveTriggerRandom.NextDouble() >= passiveDefinition.ChanceToTrigger)
        {
            return false;
        }

        if (passiveDefinition.MaxTriggersPerBattle > 0)
        {
            var battleCount = ReadCounter(owner.PassiveRuntime.TriggersThisBattleByPassiveId, passiveDefinition.Id);
            if (battleCount >= passiveDefinition.MaxTriggersPerBattle)
            {
                return false;
            }
        }

        if (passiveDefinition.MaxTriggersPerTurn > 0)
        {
            var turnCount = ReadCounter(owner.PassiveRuntime.TriggersThisTurnByPassiveId, passiveDefinition.Id);
            if (turnCount >= passiveDefinition.MaxTriggersPerTurn)
            {
                return false;
            }
        }

        IncrementCounter(owner.PassiveRuntime.TriggersThisBattleByPassiveId, passiveDefinition.Id);
        IncrementCounter(owner.PassiveRuntime.TriggersThisTurnByPassiveId, passiveDefinition.Id);
        return true;
    }

    private static void TryApplyEffect(
        BattleState state,
        Combatant owner,
        CombatPassiveEventContext context,
        PassiveDefinition passiveDefinition,
        PassiveEffectDefinition effect)
    {
        var effectKey = $"{passiveDefinition.Id}:{effect.Operation}:{effect.CharacterStat}:{effect.Token}";
        var chance = effect.ChanceToTrigger <= 0 ? 1.0 : effect.ChanceToTrigger;
        if (chance < 1.0 && state.PassiveTriggerRandom.NextDouble() >= chance)
        {
            return;
        }

        if (effect.MaxTriggersPerBattle > 0 &&
            ReadCounter(owner.PassiveRuntime.EffectTriggersThisBattleByKey, effectKey) >= effect.MaxTriggersPerBattle)
        {
            return;
        }

        if (effect.MaxTriggersPerTurn > 0 &&
            ReadCounter(owner.PassiveRuntime.EffectTriggersThisTurnByKey, effectKey) >= effect.MaxTriggersPerTurn)
        {
            return;
        }

        IncrementCounter(owner.PassiveRuntime.EffectTriggersThisBattleByKey, effectKey);
        IncrementCounter(owner.PassiveRuntime.EffectTriggersThisTurnByKey, effectKey);

        switch (effect.Operation)
        {
            case PassiveEffectOperationKind.CharacterStatChange:
                if (IsContinuousCharacterStat(effect.CharacterStat) &&
                    !effect.ExpiresAtEndOfOpposingSideTurn &&
                    HasContinuousActivation(passiveDefinition, owner, ResolveOppositeCombatant(owner, context)))
                {
                    break;
                }

                ApplyTriggeredCharacterStat(state, owner, context, effect);
                break;
            case PassiveEffectOperationKind.SkillStatChange:
                if (effect.SkillStat == PassiveSkillStatKind.TargetKind)
                {
                    break;
                }

                ApplyTriggeredSkillStat(owner, effect);
                break;
            case PassiveEffectOperationKind.TokenStatChange:
                if (effect.Token is not null)
                {
                    owner.PassiveRuntime.TokenEfficiencyAdditiveByToken.TryGetValue(effect.Token.Value, out var current);
                    owner.PassiveRuntime.TokenEfficiencyAdditiveByToken[effect.Token.Value] =
                        current + effect.Magnitude;
                }

                break;
            case PassiveEffectOperationKind.ExtraStatsFromResource:
                ApplyTriggeredCharacterStat(
                    state,
                    owner,
                    context,
                    new PassiveEffectDefinition
                    {
                        Operation = PassiveEffectOperationKind.CharacterStatChange,
                        CharacterStat = effect.CharacterStat == PassiveCharacterStatKind.None
                            ? PassiveCharacterStatKind.DamageCaused
                            : effect.CharacterStat,
                        Magnitude = effect.Magnitude * ReadResourceMagnitude(
                            owner,
                            ResolveOppositeCombatant(owner, context),
                            effect),
                        StatChangeTarget = effect.StatChangeTarget,
                    });
                break;
            case PassiveEffectOperationKind.TokenManipulation:
                ApplyTokenManipulation(state, owner, context, effect);
                break;
            case PassiveEffectOperationKind.TurnManipulation:
                owner.Tokens.Add(TokenType.BonusAction, Math.Max(1, effect.Stacks == 0 ? 1 : effect.Stacks));
                owner.PassiveRuntime.ShouldRetainTurnForBonusAction = true;
                break;
            case PassiveEffectOperationKind.Heal:
                ApplyHealToTargets(state, owner, context, effect);
                break;
            case PassiveEffectOperationKind.DealDamage:
                ApplyDamageToTargets(state, owner, context, effect);
                break;
            case PassiveEffectOperationKind.TriggerDestabilization:
                foreach (var recipient in ResolveEffectRecipients(state, owner, context, effect, preferOpposite: true))
                {
                    BattleCombatStatusTicker.TriggerDestabilizationExplosion(
                        state,
                        recipient,
                        eventEmitter: null,
                        skillId: context.Skill?.Id ?? string.Empty,
                        actorId: owner.Identity.Id);
                }

                break;
            case PassiveEffectOperationKind.CastSkill:
                if (!string.IsNullOrWhiteSpace(effect.SkillId))
                {
                    var preferredTargetCombatantId = context.Other?.Identity.Id ?? context.Victim?.Identity.Id;
                    owner.PassiveRuntime.PendingCastSkills.Enqueue(
                        new PendingCastSkillRequest(effect.SkillId, preferredTargetCombatantId));
                }

                break;
            case PassiveEffectOperationKind.SummonEnemy:
                TrySummonEnemy(state, effect, state.PassiveTriggerRandom);
                break;
        }
    }

    private static bool IsContinuousCharacterStat(PassiveCharacterStatKind characterStat) =>
        characterStat is PassiveCharacterStatKind.DefenseChance
            or PassiveCharacterStatKind.DamageCaused
            or PassiveCharacterStatKind.Accuracy
            or PassiveCharacterStatKind.CriticalChance
            or PassiveCharacterStatKind.CriticalDamage;

    private static void ApplyTriggeredCharacterStat(
        BattleState state,
        Combatant owner,
        CombatPassiveEventContext context,
        PassiveEffectDefinition effect)
    {
        foreach (var recipient in ResolveEffectRecipients(state, owner, context, effect, preferOpposite: false))
        {
            switch (effect.CharacterStat)
            {
                case PassiveCharacterStatKind.CurrentHitPoints:
                    if (effect.Magnitude >= 0)
                    {
                        CombatHealUnlock.ApplyHealHpToRecipient(recipient, (int)Math.Round(effect.Magnitude));
                    }
                    else
                    {
                        BattleCombatStatusTicker.ApplyDirectHpLoss(
                            state,
                            recipient,
                            (int)Math.Round(-effect.Magnitude),
                            eventEmitter: null,
                            owner.Identity.Id,
                            context.Skill?.Id ?? string.Empty,
                            markDeath: true);
                    }

                    break;
                case PassiveCharacterStatKind.MaximumHitPoints:
                    var maxHpBonus = (int)Math.Round(effect.Magnitude);
                    if (maxHpBonus != 0)
                    {
                        recipient.Health = new HealthComponent
                        {
                            CurrentHp = Math.Max(0, recipient.Health.CurrentHp + Math.Max(0, maxHpBonus)),
                            MaxHp = Math.Max(1, recipient.Health.MaxHp + maxHpBonus),
                            IsDead = recipient.Health.IsDead,
                            IsDeathblowPending = recipient.Health.IsDeathblowPending,
                        };
                    }

                    break;
                case PassiveCharacterStatKind.DamageCaused:
                    recipient.PassiveRuntime.BattleDamageCausedAdditive += effect.Magnitude;
                    break;
                case PassiveCharacterStatKind.DefenseChance:
                    if (effect.ExpiresAtEndOfOpposingSideTurn)
                    {
                        recipient.PassiveRuntime.UntilOpposingSideTurnEndDefenseChanceAdditive += effect.Magnitude;
                    }
                    else
                    {
                        recipient.PassiveRuntime.BattleDefenseChanceAdditive += effect.Magnitude;
                    }

                    break;
                case PassiveCharacterStatKind.Accuracy:
                    recipient.PassiveRuntime.BattleAccuracyAdditive += effect.Magnitude;
                    break;
                case PassiveCharacterStatKind.CriticalChance:
                    recipient.PassiveRuntime.BattleCritChanceAdditive += effect.Magnitude;
                    break;
                case PassiveCharacterStatKind.CriticalDamage:
                    recipient.PassiveRuntime.BattleCritDamageAdditive += effect.Magnitude;
                    break;
            }
        }
    }

    private static void ApplyTriggeredSkillStat(Combatant owner, PassiveEffectDefinition effect)
    {
        if (string.IsNullOrEmpty(effect.SkillId))
        {
            return;
        }

        if (effect.SkillStat == PassiveSkillStatKind.Damage)
        {
            owner.PassiveRuntime.SkillDamageFlatBonusBySkillId.TryGetValue(effect.SkillId, out var current);
            owner.PassiveRuntime.SkillDamageFlatBonusBySkillId[effect.SkillId] = current + effect.Magnitude;
        }
        else if (effect.SkillStat == PassiveSkillStatKind.Accuracy)
        {
            owner.PassiveRuntime.SkillAccuracyAdditiveBySkillId.TryGetValue(effect.SkillId, out var current);
            owner.PassiveRuntime.SkillAccuracyAdditiveBySkillId[effect.SkillId] = current + effect.Magnitude;
        }
    }

    private static void ApplyTokenManipulation(
        BattleState state,
        Combatant owner,
        CombatPassiveEventContext context,
        PassiveEffectDefinition effect)
    {
        TokenType tokenToManipulate;
        if (effect.Token is null)
        {
            if (context.TokenType is null)
            {
                return;
            }

            tokenToManipulate = context.TokenType.Value;
        }
        else
        {
            tokenToManipulate = effect.Token.Value;
        }

        var stacks = ResolveTokenManipulationStacks(context, effect);
        if (stacks <= 0)
        {
            return;
        }

        var shouldRaiseTokenChangedEvent = tokenToManipulate != context.TokenType;
        foreach (var recipient in ResolveEffectRecipients(state, owner, context, effect, preferOpposite: false))
        {
            if (effect.TokenManipulationMode == PassiveTokenManipulationMode.Remove)
            {
                for (var stackIndex = 0; stackIndex < stacks; stackIndex++)
                {
                    if (!recipient.Tokens.ConsumeOne(tokenToManipulate))
                    {
                        break;
                    }
                }

                if (shouldRaiseTokenChangedEvent)
                {
                    state.PassiveBus.RaiseTokenStacksChanged(
                        state,
                        owner,
                        recipient,
                        context.Skill,
                        tokenToManipulate,
                        delta: -stacks);
                }
            }
            else
            {
                recipient.Tokens.Add(tokenToManipulate, stacks);
                if (tokenToManipulate == TokenType.Hypnosis)
                {
                    CombatHypnosisRules.CaptureLockFromLastResolvedSkill(recipient);
                }

                if (shouldRaiseTokenChangedEvent)
                {
                    state.PassiveBus.RaiseTokenStacksChanged(
                        state,
                        owner,
                        recipient,
                        context.Skill,
                        tokenToManipulate,
                        stacks);
                }
            }
        }
    }

    private static int ResolveTokenManipulationStacks(
        CombatPassiveEventContext context,
        PassiveEffectDefinition effect)
    {
        if (effect.ScaleStacksPerSourceStack > 0 || effect.ScaleStacksSourceDivisor > 1)
        {
            var stacksPerSource = effect.ScaleStacksPerSourceStack <= 0 ? 1 : effect.ScaleStacksPerSourceStack;
            var divisor = effect.ScaleStacksSourceDivisor <= 0 ? 1 : effect.ScaleStacksSourceDivisor;
            return context.TokenDelta * stacksPerSource / divisor;
        }

        return Math.Max(1, effect.Stacks == 0 ? 1 : Math.Abs(effect.Stacks));
    }

    private static void ApplyHealToTargets(
        BattleState state,
        Combatant owner,
        CombatPassiveEventContext context,
        PassiveEffectDefinition effect)
    {
        var healAmount = (int)Math.Round(effect.Magnitude);
        if (effect.ScaleStacksPerSourceStack > 0 || effect.ScaleStacksSourceDivisor > 1)
        {
            var stacksPerSource = effect.ScaleStacksPerSourceStack <= 0 ? 1 : effect.ScaleStacksPerSourceStack;
            var divisor = effect.ScaleStacksSourceDivisor <= 0 ? 1 : effect.ScaleStacksSourceDivisor;
            healAmount = context.TokenDelta * stacksPerSource / divisor * healAmount;
        }
        else if (context.DamageAmount > 0 && effect.Magnitude > 0 && effect.Magnitude < 1.0)
        {
            healAmount = Math.Max(1, (int)Math.Round(context.DamageAmount * effect.Magnitude));
        }

        if (healAmount <= 0)
        {
            return;
        }

        foreach (var recipient in ResolveEffectRecipients(state, owner, context, effect, preferOpposite: false))
        {
            var applied = CombatHealUnlock.ApplyHealHpToRecipient(recipient, healAmount);
            if (applied > 0)
            {
                state.PassiveBus.RaiseHealingDealt(state, owner, recipient, context.Skill, applied);
            }
        }
    }

    private static void ApplyDamageToTargets(
        BattleState state,
        Combatant owner,
        CombatPassiveEventContext context,
        PassiveEffectDefinition effect)
    {
        var damage = (int)Math.Round(effect.Magnitude);
        if (effect.ScaleStacksPerSourceStack > 0 || effect.ScaleStacksSourceDivisor > 1)
        {
            var stacksPerSource = effect.ScaleStacksPerSourceStack <= 0 ? 1 : effect.ScaleStacksPerSourceStack;
            var divisor = effect.ScaleStacksSourceDivisor <= 0 ? 1 : effect.ScaleStacksSourceDivisor;
            damage = context.TokenDelta * stacksPerSource / divisor * Math.Max(1, damage);
        }

        if (damage <= 0)
        {
            return;
        }

        foreach (var recipient in ResolveEffectRecipients(state, owner, context, effect, preferOpposite: true))
        {
            BattleCombatStatusTicker.ApplyDirectHpLoss(
                state,
                recipient,
                damage,
                eventEmitter: null,
                owner.Identity.Id,
                context.Skill?.Id ?? string.Empty,
                markDeath: true);
        }
    }

    private static IReadOnlyList<Combatant> ResolveEffectRecipients(
        BattleState state,
        Combatant owner,
        CombatPassiveEventContext context,
        PassiveEffectDefinition effect,
        bool preferOpposite)
    {
        switch (effect.StatChangeTarget)
        {
            case PassiveStatChangeTarget.Ally:
                return LivingSameSide(state, owner)
                    .Where(combatant => !string.Equals(combatant.Identity.Id, owner.Identity.Id, StringComparison.Ordinal))
                    .ToList();
            case PassiveStatChangeTarget.SelfAndAlly:
                return LivingSameSide(state, owner);
            case PassiveStatChangeTarget.Opposite:
                var oppositeRecipient = ResolveOppositeCombatant(owner, context);
                return oppositeRecipient != null && !oppositeRecipient.Health.IsDead
                    ? [oppositeRecipient]
                    : [];
            case PassiveStatChangeTarget.AllOpposites:
                return LivingOppositeSide(state, owner);
            case PassiveStatChangeTarget.EventRecipient:
                var eventRecipient = context.Other ?? context.Victim ?? owner;
                return eventRecipient != null && !eventRecipient.Health.IsDead
                    ? [eventRecipient]
                    : [];
        }

        if (preferOpposite)
        {
            var opposite = ResolveOppositeCombatant(owner, context);
            if (opposite != null && !opposite.Health.IsDead)
            {
                return [opposite];
            }
        }

        return [owner];
    }

    private static List<Combatant> LivingSameSide(BattleState state, Combatant owner)
    {
        var roster = owner.Position.Side == Side.Allies ? state.Allies : state.Enemies;
        return roster.Where(combatant => !combatant.Health.IsDead).ToList();
    }

    private static List<Combatant> LivingOppositeSide(BattleState state, Combatant owner)
    {
        var roster = owner.Position.Side == Side.Allies ? state.Enemies : state.Allies;
        return roster.Where(combatant => !combatant.Health.IsDead).ToList();
    }

    private static Combatant? ResolveOppositeCombatant(Combatant owner, CombatPassiveEventContext context)
    {
        if (context.Other != null &&
            !string.Equals(context.Other.Identity.Id, owner.Identity.Id, StringComparison.Ordinal))
        {
            return context.Other;
        }

        if (context.Victim != null &&
            !string.Equals(context.Victim.Identity.Id, owner.Identity.Id, StringComparison.Ordinal))
        {
            return context.Victim;
        }

        return context.Self != null &&
               !string.Equals(context.Self.Identity.Id, owner.Identity.Id, StringComparison.Ordinal)
            ? context.Self
            : null;
    }

    private static void TrySummonEnemy(BattleState state, PassiveEffectDefinition effect, IRandomSource random)
    {
        if (string.IsNullOrWhiteSpace(effect.SummonEnemyId))
        {
            return;
        }

        EnemySpawnHelper.TrySpawnEnemyInDeadSlot(
            state,
            effect.SummonEnemyId,
            BattleFactory.DefaultEnemySkillIds,
            random,
            out _,
            out _);
    }

    private static int ReadCounter(Dictionary<string, int> counters, string key) =>
        counters.TryGetValue(key, out var value) ? value : 0;

    private static void IncrementCounter(Dictionary<string, int> counters, string key) =>
        counters[key] = ReadCounter(counters, key) + 1;
}
