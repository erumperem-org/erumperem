using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;

namespace Game.Tests;

public sealed class PhaseGCombatRulesTests
{
    private const int NeutralSkillDamage = 20;
    private const double ProbeSkillAccuracy = 0.70;

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(120)]
    public void TiersOneThroughThree_PlayerDamageDealtAndTakenMultipliersAreOne(double corruptionValue)
    {
        var balanceConfig = CombatBalanceConfig.CreateDefault();
        var corruptionTier = CorruptionTierCalculator.GetTier(corruptionValue);
        Assert.InRange(corruptionTier, 0, 3);

        var tierModifiers = balanceConfig.GetTierModifiers(corruptionTier);
        Assert.Equal(1.0, tierModifiers.PlayerDamageDealtMultiplier);
        Assert.Equal(1.0, tierModifiers.PlayerDamageTakenMultiplier);
    }

    [Fact]
    public void TiersOneThroughThree_PlayerOutgoingAndIncomingDamageMatchTierZero()
    {
        var smack = CreateFixedDamageSkill("phase_g_player_damage_smack", NeutralSkillDamage);
        var damageDealtAtTierZero = ComputePlayerOutgoingDamage(smack, corruptionValue: 0);
        var damageTakenAtTierZero = ComputePlayerIncomingDamage(smack, corruptionValue: 0);

        foreach (var corruptionValue in new[] { 40d, 80d, 120d })
        {
            Assert.Equal(damageDealtAtTierZero, ComputePlayerOutgoingDamage(smack, corruptionValue));
            Assert.Equal(damageTakenAtTierZero, ComputePlayerIncomingDamage(smack, corruptionValue));
        }
    }

    [Fact]
    public void EnemyAccuracy_IncreasesWithCorruptionTier()
    {
        var probe = CreateFixedDamageSkill("phase_g_enemy_accuracy_probe", NeutralSkillDamage, ProbeSkillAccuracy);
        var hitChanceByTier = new[]
        {
            ComputeEnemyHitChance(probe, corruptionValue: 0),
            ComputeEnemyHitChance(probe, corruptionValue: 40),
            ComputeEnemyHitChance(probe, corruptionValue: 80),
            ComputeEnemyHitChance(probe, corruptionValue: 120),
            ComputeEnemyHitChance(probe, corruptionValue: 200),
        };

        Assert.Equal(ProbeSkillAccuracy, hitChanceByTier[0], 5);
        for (var tierIndex = 1; tierIndex < hitChanceByTier.Length; tierIndex++)
        {
            Assert.True(
                hitChanceByTier[tierIndex] > hitChanceByTier[tierIndex - 1],
                $"Enemy accuracy should increase from tier {tierIndex - 1} to {tierIndex}.");
        }

        var playerHitChanceAtTierFour = ComputePlayerHitChance(probe, corruptionValue: 200);
        Assert.Equal(ProbeSkillAccuracy, playerHitChanceAtTierFour, 5);
    }

    [Fact]
    public void TierFour_EnemiesAreStrongerWithoutPlayerDamageDealtBuff()
    {
        var balanceConfig = CombatBalanceConfig.CreateDefault();
        var tierFourModifiers = balanceConfig.GetTierModifiers(4);

        Assert.Equal(1.0, tierFourModifiers.PlayerDamageDealtMultiplier);
        Assert.Equal(0.0, tierFourModifiers.PlayerCritBonus);
        Assert.Equal(1.35, tierFourModifiers.EnemyCritDamageMultiplierAgainstPlayer);
        Assert.True(tierFourModifiers.PlayerDamageTakenMultiplier > 1.0);
        Assert.True(
            tierFourModifiers.EnemyAccuracyBonus >
            balanceConfig.GetTierModifiers(3).EnemyAccuracyBonus);
        Assert.True(
            tierFourModifiers.EnemyCritBonusAgainstPlayer >
            balanceConfig.GetTierModifiers(3).EnemyCritBonusAgainstPlayer);

        var smack = CreateFixedDamageSkill("phase_g_tier4_smack", NeutralSkillDamage);
        var outgoingAtTierZero = ComputePlayerOutgoingDamage(smack, corruptionValue: 0);
        var outgoingAtTierFour = ComputePlayerOutgoingDamage(smack, corruptionValue: 200);
        Assert.Equal(outgoingAtTierZero, outgoingAtTierFour);

        var incomingAtTierZero = ComputePlayerIncomingDamage(smack, corruptionValue: 0);
        var incomingAtTierFour = ComputePlayerIncomingDamage(smack, corruptionValue: 200);
        Assert.Equal(
            (int)Math.Round(incomingAtTierZero * tierFourModifiers.PlayerDamageTakenMultiplier),
            incomingAtTierFour);

        var critDamageAtTierFour = ComputeEnemyCriticalDamageAgainstPlayer(smack, corruptionValue: 200);
        Assert.Equal(
            (int)Math.Round(
                NeutralSkillDamage *
                tierFourModifiers.PlayerDamageTakenMultiplier *
                CombatStatusRules.CriticalStrikeBaseDamageMultiplier *
                tierFourModifiers.EnemyCritDamageMultiplierAgainstPlayer),
            critDamageAtTierFour);
    }

    [Fact]
    public void PresentedCorruptionSlider_ClampsAboveOneHundred_WhileRealValueCanExceedTwoHundred()
    {
        Assert.Equal(100, CorruptionPresentation.ClampToPresentedSliderValue(100));
        Assert.Equal(100, CorruptionPresentation.ClampToPresentedSliderValue(250));
        Assert.Equal(4, CorruptionTierCalculator.GetTier(250));
        Assert.Equal(1f, CorruptionPresentation.ToPresentedSliderNormalizedValue(199));

        var hoverMarkup = CorruptionPresentation.BuildTierHoverAuthoredMarkup(
            4,
            CombatBalanceConfig.CreateDefault().GetTierModifiers(4));
        Assert.Contains("Tier 4", hoverMarkup, StringComparison.Ordinal);
        Assert.Contains("1.35", hoverMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfSkill_PlayerActionRequiresSelectingSelf_NotAnEnemy()
    {
        var selfSkill = CreateFixedDamageSkill(
            "phase_g_self_cast",
            damage: 0,
            targetKind: SkillTargetKind.Self);
        var battle = CreateNeutralBattle(selfSkill, allyCount: 1, enemyCount: 1);
        var actor = battle.Allies[0];
        var enemy = battle.Enemies[0];
        actor.SkillLoadout.Skills.Clear();
        actor.SkillLoadout.Skills.Add(selfSkill.Id);

        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        Assert.Null(PlayerActionBuilder.TryCreate(battle, simulator, actor, 0, enemy));
        var selfCast = PlayerActionBuilder.TryCreate(battle, simulator, actor, 0, actor);
        Assert.NotNull(selfCast);
        Assert.Same(actor, selfCast!.Target);
    }

    [Fact]
    public void ChanceToNotEndTurn_GrantsBonusActionAndRetainsTurn()
    {
        var extraActionSkill = CreateFixedDamageSkill(
            "phase_g_bonus_action",
            NeutralSkillDamage,
            chanceToNotEndTurn: 1.0);
        var battle = CreateNeutralBattle(extraActionSkill);
        var actor = battle.Allies[0];
        var enemy = battle.Enemies[0];

        var simulator = new BattleSimulator(new AlwaysZeroRandomSource(), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = enemy,
                Skill = extraActionSkill,
                ActionType = ActionType.Skill,
            });

        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.BonusAction));
        Assert.True(BattleSimulator.ShouldActorRetainTurn(actor));
    }

    [Fact]
    public void FireHasAdvantageOverMetal_AndDisadvantageIntoAnomaly()
    {
        var fireSkill = CreateFixedDamageSkill(
            "phase_g_fire_smack",
            NeutralSkillDamage,
            element: ElementType.Fire);
        var battle = CreateNeutralBattle(fireSkill);
        var actor = battle.Allies[0];
        var metalTarget = battle.Enemies[0];
        metalTarget.ElementAffinity = new ElementAffinityComponent { Element = ElementType.Metal };

        Assert.Equal(ElementMatchupKind.Advantage, CombatDamageCalculator.GetElementMatchup(actor, metalTarget, fireSkill));
        Assert.Equal(1.5, CombatDamageCalculator.GetElementalMultiplier(battle, actor, metalTarget, fireSkill));

        metalTarget.ElementAffinity = new ElementAffinityComponent { Element = ElementType.Anomaly };
        Assert.Equal(ElementMatchupKind.Disadvantage, CombatDamageCalculator.GetElementMatchup(actor, metalTarget, fireSkill));
        Assert.Equal(0.5, CombatDamageCalculator.GetElementalMultiplier(battle, actor, metalTarget, fireSkill));
    }

    private static int ComputePlayerOutgoingDamage(SkillDefinition skill, double corruptionValue)
    {
        var battle = CreateNeutralBattle(skill, corruptionValue: corruptionValue);
        return CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            skill,
            NeutralSkillDamage,
            isCriticalStrike: false,
            consumeMitigationTokens: false);
    }

    private static int ComputePlayerIncomingDamage(SkillDefinition skill, double corruptionValue)
    {
        var battle = CreateNeutralBattle(skill, corruptionValue: corruptionValue);
        return CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            battle.Enemies[0],
            battle.Allies[0],
            skill,
            NeutralSkillDamage,
            isCriticalStrike: false,
            consumeMitigationTokens: false);
    }

    private static int ComputeEnemyCriticalDamageAgainstPlayer(SkillDefinition skill, double corruptionValue)
    {
        var battle = CreateNeutralBattle(skill, corruptionValue: corruptionValue);
        return CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            battle.Enemies[0],
            battle.Allies[0],
            skill,
            NeutralSkillDamage,
            isCriticalStrike: true,
            consumeMitigationTokens: false);
    }

    private static double ComputeEnemyHitChance(SkillDefinition skill, double corruptionValue)
    {
        var battle = CreateNeutralBattle(skill, corruptionValue: corruptionValue);
        return CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            battle,
            battle.Enemies[0],
            battle.Allies[0],
            skill);
    }

    private static double ComputePlayerHitChance(SkillDefinition skill, double corruptionValue)
    {
        var battle = CreateNeutralBattle(skill, corruptionValue: corruptionValue);
        return CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            skill);
    }

    private static SkillDefinition CreateFixedDamageSkill(
        string skillId,
        int damage,
        double accuracy = 1.0,
        SkillTargetKind targetKind = SkillTargetKind.OneEnemy,
        double chanceToNotEndTurn = 0,
        ElementType element = ElementType.None) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = element,
            Type = "Active",
            BaseDamage = new DamageRange { Min = damage, Max = damage },
            BaseCritChance = 0,
            Accuracy = accuracy,
            TargetKind = targetKind,
            ChanceToNotEndTurn = chanceToNotEndTurn,
            EffectsOnHit = [],
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
        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
            combatant.Stats = new StatsComponent
            {
                Speed = combatant.Identity.Faction == Faction.Player ? 6 : 4,
                Accuracy = 1.0,
                CritChance = 0,
                DefenseChance = 0,
            };
            combatant.SkillLoadout.Skills.Clear();
            combatant.SkillLoadout.Skills.Add(skill.Id);
            battle.SkillsById[skill.Id] = skill;
        }

        return battle;
    }

    private sealed class AlwaysZeroRandomSource : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;

        public double NextDouble() => 0.0;
    }
}
