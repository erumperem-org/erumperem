using Game.Core.Diagnostics;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Passives;

/// <summary>
/// Avalia passivas desbloqueadas (<see cref="ProgressionComponent.UnlockedNodes"/>) contra <see cref="BattleState.PassivesById"/>.
/// </summary>
public static class PassiveRuleApplier
{
    public static IEnumerable<PassiveDefinition> EnumerateActivePassives(Combatant actor, BattleState state)
    {
        if (state.PassivesById.Count == 0) yield break;
        foreach (var nodeIdAndUnlocked in actor.Progression.UnlockedNodes)
        {
            if (!nodeIdAndUnlocked.Value) continue;
            if (state.PassivesById.TryGetValue(nodeIdAndUnlocked.Key, out var passiveDefinition))
            {
                yield return passiveDefinition;
            }
        }
    }

    public static void ApplyTurnStartPassives(BattleState state, Combatant actor, Action<TokenType, int>? onTokenGranted = null)
    {
        foreach (var def in EnumerateActivePassives(actor, state))
        {
            if (def.EffectKind != PassiveEffectKind.GrantTokenAtTurnStartIfCondition) continue;
            if (def.IfHasTokenType is not null && actor.Tokens.GetStacks(def.IfHasTokenType.Value) <= 0) continue;
            if (def.UnlessHasTokenType is not null && actor.Tokens.GetStacks(def.UnlessHasTokenType.Value) > 0) continue;
            if (def.GrantTokenType is null) continue;
            var stacks = Math.Max(1, def.IntValue);
            actor.Tokens.Add(def.GrantTokenType.Value, stacks);
            onTokenGranted?.Invoke(def.GrantTokenType.Value, stacks);
        }
    }

    public static void OnOutgoingHitSuccess(BattleState state, Combatant actor, SkillDefinition skill, bool hit)
    {
        if (!hit) return;
        foreach (var def in EnumerateActivePassives(actor, state))
        {
            if (def.EffectKind != PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill) continue;
            if (def.PrerequisiteSkillId == skill.Id)
            {
                actor.PassiveRuntime.ImpetoCleaveBonusPending = true;
            }
        }
    }

    public static (DamageModifierAccumulator Acc, bool ConsumeImpeto, List<PassiveCombatNote> Notes)
        AccumulateOutgoingDamageModifiers(
            BattleState state,
            Combatant actor,
            Combatant target,
            SkillDefinition skill,
            List<PassiveCombatNote>? noteSink = null)
    {
        var acc = new DamageModifierAccumulator();
        var consumeImpeto = false;
        foreach (var def in EnumerateActivePassives(actor, state))
        {
            switch (def.EffectKind)
            {
                case PassiveEffectKind.OutgoingDamageVsSkillId:
                    if (skill.Id == def.SkillId)
                    {
                        acc.OutgoingDamageAdditiveSum += def.Additive;
                        noteSink?.Add(
                            new PassiveCombatNote(
                                def.Id,
                                def.EffectKind,
                                def.Additive,
                                def.SkillId,
                                null,
                                null,
                                0,
                                0,
                                0));
                    }

                    break;
                case PassiveEffectKind.OutgoingDamageVsDotOnTarget:
                    if (def.DotType is null) break;
                    var stacks = CountDotStacks(target, def.DotType.Value);
                    if (stacks <= 0) break;
                    var bonus = def.AdditivePerStack > 0 ? stacks * def.AdditivePerStack : def.Additive;
                    if (def.Cap > 0) bonus = Math.Min(bonus, def.Cap);
                    acc.OutgoingDamageAdditiveSum += bonus;
                    noteSink?.Add(
                        new PassiveCombatNote(
                            def.Id,
                            def.EffectKind,
                            bonus,
                            def.SkillId,
                            def.DotType.Value.ToString(),
                            null,
                            0,
                            0,
                            0));
                    break;
                case PassiveEffectKind.OutgoingDamagePenaltyWhenToken:
                    if (def.TokenType is not null && actor.Tokens.GetStacks(def.TokenType.Value) > 0)
                    {
                        acc.OutgoingDamageAdditiveSum += def.Additive;
                        noteSink?.Add(
                            new PassiveCombatNote(
                                def.Id,
                                def.EffectKind,
                                def.Additive,
                                def.SkillId,
                                null,
                                def.TokenType.Value.ToString(),
                                0,
                                0,
                                0));
                    }

                    break;
                case PassiveEffectKind.OutgoingDamageAfterPrerequisiteSkill:
                    if (skill.Id == def.SkillId && actor.PassiveRuntime.ImpetoCleaveBonusPending)
                    {
                        acc.OutgoingDamageAdditiveSum += def.Additive;
                        consumeImpeto = true;
                        noteSink?.Add(
                            new PassiveCombatNote(
                                def.Id,
                                def.EffectKind,
                                def.Additive,
                                def.SkillId,
                                null,
                                null,
                                0,
                                0,
                                0));
                    }

                    break;
                case PassiveEffectKind.OutgoingDamageVsSkillIfTargetHasDot:
                    if (skill.Id != def.SkillId || def.DotType is null) break;
                    if (CountDotStacks(target, def.DotType.Value) > 0)
                    {
                        acc.OutgoingDamageAdditiveSum += def.Additive;
                        noteSink?.Add(
                            new PassiveCombatNote(
                                def.Id,
                                def.EffectKind,
                                def.Additive,
                                def.SkillId,
                                def.DotType.Value.ToString(),
                                null,
                                0,
                                0,
                                0));
                    }

                    break;
            }
        }

        return (acc, consumeImpeto, noteSink ?? new List<PassiveCombatNote>());
    }

    public static (double Mult, List<PassiveCombatNote> Notes) AccumulateIncomingDamageMultiplier(
        BattleState state,
        Combatant defender,
        List<PassiveCombatNote>? noteSink = null)
    {
        var mult = 1.0;
        foreach (var def in EnumerateActivePassives(defender, state))
        {
            if (def.EffectKind != PassiveEffectKind.IncomingDamageMultiplierWhenHpBelow) continue;
            if (def.Additive <= 0 || def.HpBelowPercent <= 0) continue;
            var hpPct = defender.Health.MaxHp <= 0 ? 0 : (double)defender.Health.CurrentHp / defender.Health.MaxHp;
            if (hpPct < def.HpBelowPercent)
            {
                mult *= def.Additive;
                noteSink?.Add(
                    new PassiveCombatNote(
                        def.Id,
                        def.EffectKind,
                        def.Additive,
                        def.SkillId,
                        null,
                        null,
                        0,
                        0,
                        0));
            }
        }

        return (mult, noteSink ?? new List<PassiveCombatNote>());
    }

    public static int AdjustDotDuration(BattleState state, Combatant actor, DotType dotType, int baseDuration)
    {
        var extra = 0;
        var maxTotal = int.MaxValue;
        foreach (var def in EnumerateActivePassives(actor, state))
        {
            if (def.EffectKind != PassiveEffectKind.DotDurationBonus) continue;
            if (def.DotType != dotType) continue;
            extra += def.IntValue;
            if (def.IntValue2 > 0)
            {
                maxTotal = Math.Min(maxTotal, def.IntValue2);
            }
        }

        var total = baseDuration + extra;
        if (maxTotal < int.MaxValue) total = Math.Min(total, maxTotal);
        return Math.Max(1, total);
    }

    public static double GetDotTickDamageMultiplier(BattleState state, Combatant victim, DotInstance dot)
    {
        var applier = state.GetAllCombatants().FirstOrDefault(combatant => combatant.Identity.Id == dot.AppliedById);
        if (applier is null) return 1.0;
        var mult = 1.0;
        foreach (var def in EnumerateActivePassives(applier, state))
        {
            if (def.EffectKind != PassiveEffectKind.DotTickDamageBonusWhenTargetHpBelow) continue;
            if (def.DotType != dot.Type) continue;
            var hpPct = victim.Health.MaxHp <= 0 ? 0 : (double)victim.Health.CurrentHp / victim.Health.MaxHp;
            if (hpPct < def.HpBelowPercent)
            {
                mult += def.Additive;
            }
        }

        return mult;
    }

    public static void ApplyPostSkillPassiveExtras(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        Action<Combatant, TokenType, int>? onExtraTokenGranted = null,
        List<PassiveCombatNote>? narrativeNotes = null)
    {
        foreach (var def in EnumerateActivePassives(actor, state))
        {
            switch (def.EffectKind)
            {
                case PassiveEffectKind.ExtraTokenOnSelfSkill:
                    if (skill.TargetKind != SkillTargetKind.Self || def.SkillId != skill.Id || def.TokenType is null) break;
                    var tokenDelta = Math.Max(1, def.IntValue);
                    actor.Tokens.Add(def.TokenType.Value, tokenDelta);
                    onExtraTokenGranted?.Invoke(actor, def.TokenType.Value, tokenDelta);
                    narrativeNotes?.Add(
                        new PassiveCombatNote(
                            def.Id,
                            def.EffectKind,
                            def.IntValue,
                            def.SkillId,
                            null,
                            def.TokenType.Value.ToString(),
                            tokenDelta,
                            0,
                            0));
                    break;
                case PassiveEffectKind.ExtraHealPercentOnSelfSkill:
                    if (skill.TargetKind != SkillTargetKind.Self || def.SkillId != skill.Id) break;
                    if (def.Additive <= 0) break;
                    HealDebugTrace.Log(
                        $"[FORBIDDEN] [COMBAT] Passiva ExtraHealPercentOnSelfSkill ignorada passive='{def.Id}' " +
                        $"skill='{skill.Id}' ally='{actor.Identity.Id}' percent={def.Additive}. " +
                        "Cura de HP só é permitida pelo Main após 3s na vila.");
                    break;
            }
        }
    }

    /// <summary>
    /// Extra DOT from passives after an enemy-targeted skill when the target already carries a DOT type (simulator supplies elemental mult + resistance gate).
    /// </summary>
    public static void ApplyPassiveExtraDotsAfterEnemySkill(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        double elementalDamageMultiplier,
        Func<Combatant, DotType, bool> dotApplicationPassesResistanceCheck,
        List<PassiveCombatNote>? narrativeNotes = null)
    {
        if (skill.TargetKind != SkillTargetKind.Enemy) return;
        foreach (var def in EnumerateActivePassives(actor, state))
        {
            if (def.EffectKind != PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot) continue;
            if (def.SkillId != skill.Id || def.DotType is null) continue;
            if (CountDotStacks(target, def.DotType.Value) <= 0) continue;
            if (!dotApplicationPassesResistanceCheck(target, def.DotType.Value)) continue;
            var rawPotency = def.IntValue > 0 ? def.IntValue : 2;
            var baseDuration = def.IntValue2 > 0 ? def.IntValue2 : 2;
            var potency = (int)Math.Round(Math.Max(1, rawPotency) * elementalDamageMultiplier);
            var duration = AdjustDotDuration(state, actor, def.DotType.Value, baseDuration);
            target.Dots.ActiveDots.Add(new DotInstance
            {
                Type = def.DotType.Value,
                Potency = potency,
                RemainingTurns = duration,
                AppliedById = actor.Identity.Id,
            });
            narrativeNotes?.Add(
                new PassiveCombatNote(
                    def.Id,
                    def.EffectKind,
                    potency,
                    def.SkillId,
                    def.DotType.Value.ToString(),
                    null,
                    0,
                    0,
                    duration));
        }
    }

    public static int CountDotStacks(Combatant target, DotType dotType) =>
        target.Dots.ActiveDots.Count(dotInstance => dotInstance.Type == dotType);
}
