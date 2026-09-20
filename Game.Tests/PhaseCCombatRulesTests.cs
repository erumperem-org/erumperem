using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Tests;

public sealed class PhaseCCombatRulesTests
{
    private const int FixedSkillDamage = 20;

    [Fact]
    public void Corrosion_EndOfTurn_DealsFiveDamageAndLosesOneStack()
    {
        var smack = CreateFixedDamageSkill("phase_c_corrosion_smack", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack);
        var actor = battle.Allies[0];
        actor.Tokens.Add(TokenType.Corrosion, 3);
        actor.Tokens.Add(TokenType.Stun, 1);
        var hpBeforeTick = actor.Health.CurrentHp;

        var simulator = CreateAlwaysHittingSimulator();
        Assert.False(simulator.TryPrepareActorTurn(battle, actor));

        Assert.Equal(hpBeforeTick - CombatStatusRules.CorrosionEndOfTurnDamage, actor.Health.CurrentHp);
        Assert.Equal(2, actor.Tokens.GetStacks(TokenType.Corrosion));
    }

    [Fact]
    public void Taunt_ConsumesOnlyWhenAttackerIsEnemy()
    {
        var smack = CreateFixedDamageSkill("phase_c_taunt_smack", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack, allyCount: 2);
        NeutralizeCombatantElements(battle);
        var playerAttacker = battle.Allies[0];
        var tauntedAlly = battle.Allies[1];
        var enemyAttacker = battle.Enemies[0];
        tauntedAlly.Tokens.Add(TokenType.Taunt, 2);

        ResolveSkill(battle, smack, playerAttacker, tauntedAlly);
        Assert.Equal(2, tauntedAlly.Tokens.GetStacks(TokenType.Taunt));

        enemyAttacker.SkillLoadout.Skills.Clear();
        enemyAttacker.SkillLoadout.Skills.Add(smack.Id);
        battle.SkillsById[smack.Id] = smack;
        ResolveSkill(battle, smack, enemyAttacker, tauntedAlly);
        Assert.Equal(1, tauntedAlly.Tokens.GetStacks(TokenType.Taunt));
    }

    [Fact]
    public void ControlledInstability_ReflectsOnlyWhenAttackerIsEnemy()
    {
        var smack = CreateFixedDamageSkill("phase_c_ci_smack", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack, allyCount: 2);
        NeutralizeCombatantElements(battle);
        var playerAttacker = battle.Allies[0];
        var shieldedAlly = battle.Allies[1];
        var enemyAttacker = battle.Enemies[0];
        shieldedAlly.Tokens.Add(TokenType.ControlledInstability, 2);
        var playerHpBefore = playerAttacker.Health.CurrentHp;
        ResolveSkill(battle, smack, playerAttacker, shieldedAlly);
        Assert.Equal(playerHpBefore, playerAttacker.Health.CurrentHp);

        var enemyHpBefore = enemyAttacker.Health.CurrentHp;
        enemyAttacker.SkillLoadout.Skills.Clear();
        enemyAttacker.SkillLoadout.Skills.Add(smack.Id);
        ResolveSkill(battle, smack, enemyAttacker, shieldedAlly);
        Assert.Equal(
            enemyHpBefore - (CombatStatusRules.ControlledInstabilityReflectDamagePerStack * 2),
            enemyAttacker.Health.CurrentHp);
    }

    [Fact]
    public void Confusion_SwapsEnemySkillToAllyTargets()
    {
        var enemySkill = CreateFixedDamageSkill("phase_c_confusion_enemy", FixedSkillDamage);
        var battle = CreateNeutralBattle(enemySkill, allyCount: 2, enemyCount: 1);
        var actor = battle.Allies[0];
        actor.PassiveRuntime.ConfusedSkillIdsThisTurn.Add(enemySkill.Id);

        var swappedTargets = SkillTargetResolver.ResolvePrimaryTargets(
            battle,
            actor,
            enemySkill,
            battle.Enemies[0]);

        Assert.Contains(swappedTargets, combatant => combatant.Position.Side == Side.Allies);
        Assert.DoesNotContain(swappedTargets, combatant => combatant.Position.Side == Side.Enemies);
    }

    [Fact]
    public void Confusion_SwapsAllySkillToEnemyTargets()
    {
        var allySkill = CreateTokenSkill("phase_c_confusion_ally", SkillTargetKind.OneAlly, TokenType.Taunt);
        var battle = CreateNeutralBattle(allySkill, allyCount: 2, enemyCount: 1);
        var actor = battle.Allies[0];
        actor.PassiveRuntime.ConfusedSkillIdsThisTurn.Add(allySkill.Id);

        var swappedTargets = SkillTargetResolver.ResolvePrimaryTargets(
            battle,
            actor,
            allySkill,
            battle.Allies[1]);

        Assert.Equal([battle.Enemies[0].Identity.Id], swappedTargets.Select(combatant => combatant.Identity.Id));
    }

    [Fact]
    public void Confusion_SelfSkillBecomesNone()
    {
        var selfSkill = CreateTokenSkill("phase_c_confusion_self", SkillTargetKind.Self, TokenType.Strength);
        var battle = CreateNeutralBattle(selfSkill);
        var actor = battle.Allies[0];
        actor.PassiveRuntime.ConfusedSkillIdsThisTurn.Add(selfSkill.Id);

        Assert.Empty(SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfSkill, actor));
    }

    [Fact]
    public void Bleeding_EndOfTurn_DealsFivePercentMaxHpAndLosesOneStack()
    {
        var smack = CreateFixedDamageSkill("phase_c_bleed_smack", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack);
        var actor = battle.Allies[0];
        actor.Tokens.Add(TokenType.Bleeding, 2);
        actor.Tokens.Add(TokenType.Stun, 1);
        var expectedDamage = (int)Math.Floor(
            actor.Health.MaxHp * CombatStatusRules.BleedingMaxHpDamageFractionPerStack * 2);
        var hpBeforeTick = actor.Health.CurrentHp;

        Assert.False(CreateAlwaysHittingSimulator().TryPrepareActorTurn(battle, actor));

        Assert.Equal(hpBeforeTick - expectedDamage, actor.Health.CurrentHp);
        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.Bleeding));
    }

    [Fact]
    public void ApplyDotBurn_ConvertsToBurnTokenWithoutLeavingDotInstance()
    {
        var burnSkill = new SkillDefinition
        {
            Id = "phase_c_apply_burn",
            Name = "phase_c_apply_burn",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 1, Max = 1 },
            BaseCritChance = 0,
            Accuracy = 1,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.ApplyDot,
                    Dot = DotType.Burn,
                    Potency = 4,
                    Duration = 3,
                    Chance = 1,
                },
            ],
        };
        var battle = CreateNeutralBattle(burnSkill);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        target.Resistances = new ResistanceComponent
        {
            BurnRes = 0,
            BlightRes = 0,
            MoveRes = 0,
            StunRes = 0,
            DeathblowRes = 0,
        };

        ResolveSkill(battle, burnSkill, actor, target);

        Assert.Empty(target.Dots.ActiveDots);
        Assert.Equal(4, target.Tokens.GetStacks(TokenType.Burn));
    }

    [Fact]
    public void BurnToken_EndOfTurn_DealsDamageEqualToStacksAndLosesOneStack()
    {
        var smack = CreateFixedDamageSkill("phase_c_burn_smack", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack);
        var actor = battle.Allies[0];
        actor.Tokens.Add(TokenType.Burn, 3);
        actor.Tokens.Add(TokenType.Stun, 1);
        var hpBeforeTick = actor.Health.CurrentHp;

        Assert.False(CreateAlwaysHittingSimulator().TryPrepareActorTurn(battle, actor));

        Assert.Equal(hpBeforeTick - 3, actor.Health.CurrentHp);
        Assert.Equal(2, actor.Tokens.GetStacks(TokenType.Burn));
    }

    [Fact]
    public void Hypnosis_LocksCombatantToLastResolvedSkill()
    {
        var firstSkill = CreateFixedDamageSkill("phase_c_hypnosis_first", FixedSkillDamage);
        var secondSkill = CreateFixedDamageSkill("phase_c_hypnosis_second", FixedSkillDamage);
        var battle = BattleFactory.CreateSampleBattle(
            [firstSkill, secondSkill],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [firstSkill.Id, secondSkill.Id]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.PassiveRuntime.LastResolvedSkillId = firstSkill.Id;
        actor.Tokens.Add(TokenType.Hypnosis, 1);
        CombatHypnosisRules.CaptureLockFromLastResolvedSkill(actor);

        var simulator = CreateAlwaysHittingSimulator();
        Assert.True(simulator.IsSkillUsable(actor, firstSkill));
        Assert.False(simulator.IsSkillUsable(actor, secondSkill));
    }

    [Fact]
    public void Dizzy_RetargetsToAnotherValidEnemy()
    {
        var smack = CreateFixedDamageSkill("phase_c_dizzy_smack", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack, allyCount: 1, enemyCount: 2);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.Tokens.Add(TokenType.Dizzy, 1);
        var selectedEnemy = battle.Enemies[0];
        var otherEnemy = battle.Enemies[1];
        otherEnemy.Health.CurrentHp = otherEnemy.Health.MaxHp;
        selectedEnemy.Health.CurrentHp = selectedEnemy.Health.MaxHp;

        var simulator = new BattleSimulator(
            new ScriptedRandomSource(doubles: [0.0], integers: [1]),
            new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = selectedEnemy,
                Skill = smack,
                ActionType = ActionType.Skill,
            });

        Assert.Equal(otherEnemy.Health.MaxHp - FixedSkillDamage, otherEnemy.Health.CurrentHp);
        Assert.Equal(selectedEnemy.Health.MaxHp, selectedEnemy.Health.CurrentHp);
    }

    [Fact]
    public void PermaStrength_IsQuarterEfficiencyAndDoesNotDecay()
    {
        var smack = CreateFixedDamageSkill("phase_c_perma_strength", FixedSkillDamage);
        var battle = CreateNeutralBattle(smack);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];

        var baseline = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, FixedSkillDamage, isCriticalStrike: false, consumeMitigationTokens: false);

        actor.Tokens.Add(TokenType.PermaStrength, 1);
        var withPerma = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, FixedSkillDamage, isCriticalStrike: false, consumeMitigationTokens: false);

        Assert.Equal(
            (int)Math.Round(baseline * (1.0 + CombatStatusRules.StrengthDamageCausedBonusPerStack *
                                        CombatStatusRules.PermanentStatusEfficiencyMultiplier)),
            withPerma);

        actor.Tokens.Add(TokenType.Stun, 1);
        CreateAlwaysHittingSimulator().TryPrepareActorTurn(battle, actor);
        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.PermaStrength));
        Assert.DoesNotContain(TokenType.PermaStrength, CombatStatusRules.EndOfTurnDecayTokens);
    }

    [Fact]
    public void Destabilization_DamagesAllOtherLivingCombatants()
    {
        var smack = CreateFixedDamageSkill("phase_c_destab_smack", 100);
        var battle = CreateNeutralBattle(smack, allyCount: 2, enemyCount: 2);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var explodingEnemy = battle.Enemies[0];
        explodingEnemy.Health = new HealthComponent { CurrentHp = 1, MaxHp = 20, IsDead = false };
        explodingEnemy.Tokens.Add(TokenType.Destabilization, 2);
        var companionHpBefore = battle.Allies[1].Health.CurrentHp;
        var otherEnemyHpBefore = battle.Enemies[1].Health.CurrentHp;

        ResolveSkill(battle, smack, actor, explodingEnemy);

        var explosionDamage = CombatStatusRules.DestabilizationDamagePerStack * 2;
        Assert.True(explodingEnemy.Health.IsDead);
        Assert.Equal(0, explodingEnemy.Tokens.GetStacks(TokenType.Destabilization));
        Assert.Equal(companionHpBefore - explosionDamage, battle.Allies[1].Health.CurrentHp);
        Assert.Equal(otherEnemyHpBefore - explosionDamage, battle.Enemies[1].Health.CurrentHp);
        Assert.Equal(actor.Health.MaxHp - explosionDamage, actor.Health.CurrentHp);
    }

    [Fact]
    public void Unleash_ConsumesAllDestabilizationAndDoesNotDamageCarrier()
    {
        var unleash = new SkillDefinition
        {
            Id = "phase_c_unleash",
            Name = "phase_c_unleash",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.TriggerDestabilizationOnTargets,
                    Chance = 1,
                },
            ],
        };
        var battle = CreateNeutralBattle(unleash, allyCount: 1, enemyCount: 2);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var carrier = battle.Enemies[0];
        var bystander = battle.Enemies[1];
        carrier.Tokens.Add(TokenType.Destabilization, 3);
        var carrierHpBefore = carrier.Health.CurrentHp;
        var bystanderHpBefore = bystander.Health.CurrentHp;
        var actorHpBefore = actor.Health.CurrentHp;

        ResolveSkill(battle, unleash, actor, carrier);

        var explosionDamage = CombatStatusRules.DestabilizationDamagePerStack * 3;
        Assert.Equal(0, carrier.Tokens.GetStacks(TokenType.Destabilization));
        Assert.Equal(carrierHpBefore, carrier.Health.CurrentHp);
        Assert.Equal(bystanderHpBefore - explosionDamage, bystander.Health.CurrentHp);
        Assert.Equal(actorHpBefore - explosionDamage, actor.Health.CurrentHp);
    }

    [Fact]
    public void Preview_HitChanceMatchesCalculatorIncludingDebuffAccuracy()
    {
        var probe = new SkillDefinition
        {
            Id = "phase_c_preview_accuracy",
            Name = "phase_c_preview_accuracy",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 5, Max = 5 },
            BaseCritChance = 0,
            Accuracy = 0.5,
            TargetKind = SkillTargetKind.OneEnemy,
            ComputeFromDebuffTypesOnTarget = true,
            AccuracyPerDistinctDebuffType = 0.1,
            AccuracyPenaltyPerLivingEnemy = 0.05,
            EffectsOnHit = [],
        };
        var battle = CreateNeutralBattle(probe, allyCount: 1, enemyCount: 2);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        target.Tokens.Add(TokenType.Weaken, 1);
        target.Tokens.Add(TokenType.Vulnerability, 1);

        Assert.True(SkillDamagePreviewCalculator.TryCompute(battle, actor, target, probe, out var preview));
        var calculatorHitChance = CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            battle, actor, target, probe);
        Assert.Equal(calculatorHitChance, preview.HitChanceFraction);
    }

    [Fact]
    public void Preview_DamageIncludesDefenseChanceAndCorruption()
    {
        var smack = CreateFixedDamageSkill("phase_c_preview_defense", 100);
        var battle = CreateNeutralBattle(smack, corruptionValue: 40);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        target.Stats = new StatsComponent
        {
            Speed = 4,
            Accuracy = 1,
            CritChance = 0,
            DefenseChance = 0.25,
        };

        Assert.True(SkillDamagePreviewCalculator.TryCompute(battle, actor, target, smack, out var preview));
        var minDamage = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, 100, isCriticalStrike: false, consumeMitigationTokens: false);
        var maxIncludesCrit = CombatDamageCalculator.EffectiveCritChanceFraction(battle, actor, target, smack) > 0;
        var maxDamage = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, 100, isCriticalStrike: maxIncludesCrit, consumeMitigationTokens: false);
        Assert.Equal(minDamage, preview.MinDamageOnHit);
        Assert.Equal(maxDamage, preview.MaxDamageOnHit);
    }

    [Fact]
    public void DataDriven_Permanent_CharacterStatChange_IncreasesDamageCaused()
    {
        var smack = CreateFixedDamageSkill("phase_c_permanent_smack", FixedSkillDamage);
        var passive = CreateDataDrivenPassive(
            "phase_c_permanent_damage",
            PassiveActivationKind.Permanent,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.CharacterStatChange,
                CharacterStat = PassiveCharacterStatKind.DamageCaused,
                Magnitude = 1.0,
            });
        var battle = CreateBattleWithPassive(smack, passive);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];

        var damage = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, FixedSkillDamage, isCriticalStrike: false, consumeMitigationTokens: false);

        Assert.Equal(FixedSkillDamage * 2, damage);
    }

    [Fact]
    public void DataDriven_WhileHavingStatus_RequiresToken()
    {
        var smack = CreateFixedDamageSkill("phase_c_while_status_smack", FixedSkillDamage);
        var passive = CreateDataDrivenPassive(
            "phase_c_while_strength",
            PassiveActivationKind.WhileHavingStatus,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.CharacterStatChange,
                CharacterStat = PassiveCharacterStatKind.DamageCaused,
                Magnitude = 1.0,
            },
            requiredStatus: TokenType.Strength);
        var battle = CreateBattleWithPassive(smack, passive);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];

        var withoutToken = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, FixedSkillDamage, isCriticalStrike: false, consumeMitigationTokens: false);
        actor.Tokens.Add(TokenType.Strength, 1);
        var withToken = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, FixedSkillDamage, isCriticalStrike: false, consumeMitigationTokens: false);

        Assert.Equal(FixedSkillDamage, withoutToken);
        Assert.True(withToken > withoutToken);
    }

    [Fact]
    public void DataDriven_UponDamageTaken_HealsOwner()
    {
        var smack = CreateFixedDamageSkill("phase_c_upon_damage_smack", FixedSkillDamage);
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_damage_heal",
            PassiveActivationKind.UponDamageTaken,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.Heal,
                Magnitude = 7,
            });
        var battle = CreateBattleWithPassive(smack, passive, enemyCount: 1);
        NeutralizeCombatantElements(battle);
        var defender = battle.Allies[0];
        var enemy = battle.Enemies[0];
        enemy.SkillLoadout.Skills.Clear();
        enemy.SkillLoadout.Skills.Add(smack.Id);
        defender.Health.CurrentHp = 30;

        ResolveSkill(battle, smack, enemy, defender);

        Assert.Equal(30 - FixedSkillDamage + 7, defender.Health.CurrentHp);
    }

    [Fact]
    public void DataDriven_UponDamageTaken_EveryXHitPointsLost()
    {
        var smack = CreateFixedDamageSkill("phase_c_every_hp_smack", 10);
        var passive = new PassiveDefinition
        {
            Id = "phase_c_every_8_hp",
            Conditions =
            [
                new PassiveConditionDefinition
                {
                    Activation = PassiveActivationKind.UponDamageTaken,
                    HitPointsLostPerTrigger = 8,
                },
            ],
            Effects =
            [
                new PassiveEffectDefinition
                {
                    Operation = PassiveEffectOperationKind.TokenManipulation,
                    Token = TokenType.ControlledInstability,
                    Stacks = 1,
                    TokenManipulationMode = PassiveTokenManipulationMode.Apply,
                },
            ],
        };
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);
        var defender = battle.Allies[0];
        var enemy = battle.Enemies[0];
        enemy.SkillLoadout.Skills.Clear();
        enemy.SkillLoadout.Skills.Add(smack.Id);

        ResolveSkill(battle, smack, enemy, defender);

        Assert.Equal(1, defender.Tokens.GetStacks(TokenType.ControlledInstability));
    }

    [Fact]
    public void DataDriven_UponKill_GrantsToken()
    {
        var smack = CreateFixedDamageSkill("phase_c_kill_smack", 100);
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_kill",
            PassiveActivationKind.UponKill,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.TokenManipulation,
                Token = TokenType.Taunt,
                Stacks = 2,
                TokenManipulationMode = PassiveTokenManipulationMode.Apply,
            });
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        target.Health = new HealthComponent { CurrentHp = 1, MaxHp = 20, IsDead = false };

        ResolveSkill(battle, smack, actor, target);

        Assert.True(target.Health.IsDead);
        Assert.Equal(2, actor.Tokens.GetStacks(TokenType.Taunt));
    }

    [Fact]
    public void DataDriven_UponCriticalStrike_Heals()
    {
        var critSkill = new SkillDefinition
        {
            Id = "phase_c_crit_smack",
            Name = "phase_c_crit_smack",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = FixedSkillDamage, Max = FixedSkillDamage },
            BaseCritChance = 1,
            Accuracy = 1,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_crit",
            PassiveActivationKind.UponCriticalStrike,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.Heal,
                Magnitude = 5,
            });
        var battle = CreateBattleWithPassive(critSkill, passive);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 20;

        ResolveSkill(battle, critSkill, actor, battle.Enemies[0]);

        Assert.Equal(25, actor.Health.CurrentHp);
    }

    [Fact]
    public void DataDriven_UponApplyingStatus_GrantsExtraToken()
    {
        var applySkill = CreateTokenSkill("phase_c_apply_mark", SkillTargetKind.OneEnemy, TokenType.Mark);
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_applying",
            PassiveActivationKind.UponApplyingStatus,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.TokenManipulation,
                Token = TokenType.Taunt,
                Stacks = 1,
                TokenManipulationMode = PassiveTokenManipulationMode.Apply,
            },
            requiredStatus: TokenType.Mark);
        var battle = CreateBattleWithPassive(applySkill, passive);
        NeutralizeCombatantElements(battle);

        ResolveSkill(battle, applySkill, battle.Allies[0], battle.Enemies[0]);

        Assert.Equal(1, battle.Enemies[0].Tokens.GetStacks(TokenType.Mark));
        Assert.Equal(1, battle.Allies[0].Tokens.GetStacks(TokenType.Taunt));
    }

    [Fact]
    public void DataDriven_UponReceivingStatus_Heals()
    {
        var applySkill = CreateTokenSkill("phase_c_receive_block", SkillTargetKind.OneAlly, TokenType.Taunt);
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_receiving",
            PassiveActivationKind.UponReceivingStatus,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.Heal,
                Magnitude = 4,
            },
            requiredStatus: TokenType.Taunt);
        var battle = CreateBattleWithPassive(applySkill, passive, allyCount: 1);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 10;

        ResolveSkill(battle, applySkill, actor, actor);

        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.Taunt));
        Assert.Equal(14, actor.Health.CurrentHp);
    }

    [Fact]
    public void DataDriven_UponStatThreshold_FiresWhenHpDropsBelowFraction()
    {
        var smack = CreateFixedDamageSkill("phase_c_threshold_smack", 30);
        var passive = new PassiveDefinition
        {
            Id = "phase_c_hp_threshold",
            Conditions =
            [
                new PassiveConditionDefinition
                {
                    Activation = PassiveActivationKind.UponStatThreshold,
                    StatThresholdFraction = 0.5,
                    StatThresholdComparison = PassiveStatThresholdComparison.BelowOrEqual,
                },
            ],
            Effects =
            [
                new PassiveEffectDefinition
                {
                    Operation = PassiveEffectOperationKind.TokenManipulation,
                    Token = TokenType.ControlledInstability,
                    Stacks = 3,
                    TokenManipulationMode = PassiveTokenManipulationMode.Apply,
                },
            ],
        };
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);
        var defender = battle.Allies[0];
        var enemy = battle.Enemies[0];
        enemy.SkillLoadout.Skills.Clear();
        enemy.SkillLoadout.Skills.Add(smack.Id);

        ResolveSkill(battle, smack, enemy, defender);

        Assert.True(defender.Health.CurrentHp <= defender.Health.MaxHp * 0.5);
        Assert.Equal(3, defender.Tokens.GetStacks(TokenType.ControlledInstability));
    }

    [Fact]
    public void DataDriven_OnTurnEnd_AppliesToken()
    {
        var smack = CreateFixedDamageSkill("phase_c_turn_end_smack", FixedSkillDamage);
        var passive = CreateDataDrivenPassive(
            "phase_c_on_turn_end",
            PassiveActivationKind.OnTurnEnd,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.TokenManipulation,
                Token = TokenType.Regeneration,
                Stacks = 1,
                TokenManipulationMode = PassiveTokenManipulationMode.Apply,
            });
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];

        ResolveSkill(battle, smack, actor, battle.Enemies[0]);

        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.Regeneration));
    }

    [Fact]
    public void DataDriven_UponHittingTargetWithStatus_DealsExtraDamageEffect()
    {
        var smack = CreateFixedDamageSkill("phase_c_hit_status_smack", 5);
        var passive = CreateDataDrivenPassive(
            "phase_c_hit_marked",
            PassiveActivationKind.UponHittingTargetWithStatus,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.DealDamage,
                Magnitude = 5,
            },
            requiredStatus: TokenType.Weaken);
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);
        var target = battle.Enemies[0];
        target.Tokens.Add(TokenType.Weaken, 1);
        var hpBefore = target.Health.CurrentHp;

        ResolveSkill(battle, smack, battle.Allies[0], target);

        Assert.Equal(hpBefore - 5 - 5, target.Health.CurrentHp);
    }

    [Fact]
    public void DataDriven_UponDealingDamage_GrantsToken()
    {
        var smack = CreateFixedDamageSkill("phase_c_dealing_smack", FixedSkillDamage);
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_dealing",
            PassiveActivationKind.UponDealingDamage,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.TokenManipulation,
                Token = TokenType.Taunt,
                Stacks = 1,
                TokenManipulationMode = PassiveTokenManipulationMode.Apply,
            });
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);

        ResolveSkill(battle, smack, battle.Allies[0], battle.Enemies[0]);

        Assert.Equal(1, battle.Allies[0].Tokens.GetStacks(TokenType.Taunt));
    }

    [Fact]
    public void DataDriven_UponHealing_GrantsToken()
    {
        var healSkill = new SkillDefinition
        {
            Id = "phase_c_heal_smack",
            Name = "phase_c_heal_smack",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1,
            TargetKind = SkillTargetKind.Self,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.HealHp,
                    Potency = 5,
                    Chance = 1,
                },
            ],
        };
        var passive = CreateDataDrivenPassive(
            "phase_c_upon_healing",
            PassiveActivationKind.UponHealing,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.TokenManipulation,
                Token = TokenType.Taunt,
                Stacks = 1,
                TokenManipulationMode = PassiveTokenManipulationMode.Apply,
            });
        var battle = CreateBattleWithPassive(healSkill, passive);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 10;

        ResolveSkill(battle, healSkill, actor, actor);

        Assert.Equal(15, actor.Health.CurrentHp);
        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.Taunt));
    }

    [Fact]
    public void DataDriven_MaxTriggersPerBattle_StopsAfterCap()
    {
        var smack = CreateFixedDamageSkill("phase_c_cap_smack", 5);
        var passive = new PassiveDefinition
        {
            Id = "phase_c_capped_heal",
            MaxTriggersPerBattle = 1,
            Conditions =
            [
                new PassiveConditionDefinition { Activation = PassiveActivationKind.UponDealingDamage },
            ],
            Effects =
            [
                new PassiveEffectDefinition
                {
                    Operation = PassiveEffectOperationKind.Heal,
                    Magnitude = 3,
                },
            ],
        };
        var battle = CreateBattleWithPassive(smack, passive);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 10;

        ResolveSkill(battle, smack, actor, battle.Enemies[0]);
        Assert.Equal(13, actor.Health.CurrentHp);
        ResolveSkill(battle, smack, actor, battle.Enemies[0]);
        Assert.Equal(13, actor.Health.CurrentHp);
    }

    [Fact]
    public void DataDriven_CorruptionMinTier_BlocksBelowThreshold()
    {
        var smack = CreateFixedDamageSkill("phase_c_corr_smack", FixedSkillDamage);
        var passive = CreateDataDrivenPassive(
            "phase_c_corr_gate",
            PassiveActivationKind.Permanent,
            new PassiveEffectDefinition
            {
                Operation = PassiveEffectOperationKind.CharacterStatChange,
                CharacterStat = PassiveCharacterStatKind.DamageCaused,
                Magnitude = 1.0,
            });
        passive = new PassiveDefinition
        {
            Id = passive.Id,
            RequiredPartyRole = passive.RequiredPartyRole,
            CorruptionMinTier = 2,
            Conditions = passive.Conditions,
            Effects = passive.Effects,
        };
        var battle = CreateBattleWithPassive(smack, passive, corruptionValue: 0);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];

        var damageAtTierZero = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle, actor, target, smack, FixedSkillDamage, isCriticalStrike: false, consumeMitigationTokens: false);
        Assert.Equal(FixedSkillDamage, damageAtTierZero);
        Assert.DoesNotContain(passive, PassiveRuleApplier.EnumerateActivePassives(actor, battle));
    }

    [Fact]
    public void DataDriven_LeaderRoleGate_StillHonored()
    {
        var smack = CreateFixedDamageSkill("phase_c_role_smack", FixedSkillDamage);
        var passive = new PassiveDefinition
        {
            Id = "phase_c_leader_data",
            RequiredPartyRole = PassiveRequiredPartyRole.Leader,
            Conditions = [new PassiveConditionDefinition { Activation = PassiveActivationKind.Permanent }],
            Effects =
            [
                new PassiveEffectDefinition
                {
                    Operation = PassiveEffectOperationKind.CharacterStatChange,
                    CharacterStat = PassiveCharacterStatKind.DamageCaused,
                    Magnitude = 1.0,
                },
            ],
        };
        var battle = CreateBattleWithPassive(smack, passive, allyCount: 2);
        NeutralizeCombatantElements(battle);

        Assert.Contains(passive, PassiveRuleApplier.EnumerateActivePassives(battle.Allies[0], battle));
        Assert.DoesNotContain(passive, PassiveRuleApplier.EnumerateActivePassives(battle.Allies[1], battle));
    }

    [Fact]
    public void LegacyEffectKind_StillAppliesWhenEffectsListIsEmpty()
    {
        var smack = CreateFixedDamageSkill("phase_c_legacy_smack", FixedSkillDamage);
        var legacyPassive = new PassiveDefinition
        {
            Id = "phase_c_legacy_kind",
            EffectKind = PassiveEffectKind.DamageCausedVsSkillId,
            SkillId = smack.Id,
            Additive = 1.0,
        };
        var battle = CreateBattleWithPassive(smack, legacyPassive);
        NeutralizeCombatantElements(battle);

        var damage = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            smack,
            FixedSkillDamage,
            isCriticalStrike: false,
            consumeMitigationTokens: false);

        Assert.Equal(FixedSkillDamage * 2, damage);
    }

    [Fact]
    public void Stealth_RemainsFlatAccuracyPenaltyWhilePresent()
    {
        Assert.Equal(0.40, CombatStatusRules.StealthTargetAccuracyPenalty);
        Assert.Contains(TokenType.Stealth, CombatStatusRules.EndOfTurnDecayTokens);
    }

    private static PassiveDefinition CreateDataDrivenPassive(
        string passiveId,
        PassiveActivationKind activation,
        PassiveEffectDefinition effect,
        TokenType? requiredStatus = null) =>
        new()
        {
            Id = passiveId,
            Conditions =
            [
                new PassiveConditionDefinition
                {
                    Activation = activation,
                    RequiredStatus = requiredStatus,
                },
            ],
            Effects = [effect],
        };

    private static BattleState CreateBattleWithPassive(
        SkillDefinition skill,
        PassiveDefinition passive,
        int allyCount = 1,
        int enemyCount = 1,
        double corruptionValue = 0)
    {
        var battle = BattleFactory.CreateSampleBattle(
            [skill],
            allyCount,
            enemyCount,
            corruptionValue,
            allySkillIds: [skill.Id],
            passivesById: new Dictionary<string, PassiveDefinition> { [passive.Id] = passive },
            unlockAllPassiveNodesForAllies: true);
        NeutralizeCombatantElements(battle);
        battle.Allies[0].Stats = new StatsComponent { Speed = 6, Accuracy = 1, CritChance = 0 };
        battle.Enemies[0].Stats = new StatsComponent { Speed = 4, Accuracy = 1, CritChance = 0 };
        return battle;
    }

    private static SkillDefinition CreateFixedDamageSkill(string skillId, int damage, double accuracy = 1.0) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = damage, Max = damage },
            BaseCritChance = 0,
            Accuracy = accuracy,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };

    private static SkillDefinition CreateTokenSkill(string skillId, SkillTargetKind targetKind, TokenType tokenType) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1,
            TargetKind = targetKind,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.ApplyToken,
                    Token = tokenType,
                    Stacks = 1,
                    Chance = 1,
                },
            ],
        };

    private static BattleState CreateNeutralBattle(
        SkillDefinition skill,
        int allyCount = 1,
        int enemyCount = 1,
        double corruptionValue = 0)
    {
        var battle = BattleFactory.CreateSampleBattle(
            [skill],
            allyCount,
            enemyCount,
            corruptionValue,
            allySkillIds: [skill.Id]);
        NeutralizeCombatantElements(battle);
        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.Stats = new StatsComponent
            {
                Speed = combatant.Identity.Faction == Faction.Player ? 6 : 4,
                Accuracy = 1.0,
                CritChance = 0,
            };
        }

        return battle;
    }

    private static void NeutralizeCombatantElements(BattleState battle)
    {
        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        }
    }

    private static BattleSimulator CreateAlwaysHittingSimulator() =>
        new(new AlwaysZeroRandomSource(), new CombatEventCollector());

    private static void ResolveSkill(
        BattleState battle,
        SkillDefinition skill,
        Combatant actor,
        Combatant target)
    {
        var simulator = CreateAlwaysHittingSimulator();
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = target,
                Skill = skill,
                ActionType = ActionType.Skill,
            });
    }

    private sealed class AlwaysZeroRandomSource : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;

        public double NextDouble() => 0.0;
    }

    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<double> _doubles;
        private readonly Queue<int> _integers;

        public ScriptedRandomSource(IEnumerable<double> doubles, IEnumerable<int> integers)
        {
            _doubles = new Queue<double>(doubles);
            _integers = new Queue<int>(integers);
        }

        public int Next(int minValue, int maxValue) =>
            _integers.Count > 0 ? _integers.Dequeue() : minValue;

        public double NextDouble() =>
            _doubles.Count > 0 ? _doubles.Dequeue() : 0.0;
    }
}
