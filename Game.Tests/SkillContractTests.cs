using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;

namespace Game.Tests;

public sealed class SkillTargetResolverTests
{
    [Fact]
    public void Self_ResolvesActor_EvenWhenEnemyIsSelected()
    {
        var selfSkill = CreateTokenSkill("self_buff", SkillTargetKind.Self, EffectScope.Default, TokenType.Block);
        var battle = CreateBattle(selfSkill);
        var actor = battle.Allies[0];
        var selectedEnemy = battle.Enemies[0];

        var primaryTargets = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfSkill, selectedEnemy);

        Assert.Single(primaryTargets);
        Assert.Same(actor, primaryTargets[0]);

        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        var chosenAction = PlayerActionBuilder.TryCreate(battle, simulator, actor, 0, selectedEnemy);
        Assert.NotNull(chosenAction);
        Assert.Same(actor, chosenAction!.Target);
    }

    [Fact]
    public void SelfOrAlly_AcceptsSelfOrLivingAlly_AndRejectsEnemy()
    {
        var selfOrAllySkill = CreateTokenSkill("self_or_ally", SkillTargetKind.SelfOrAlly, EffectScope.Default, TokenType.Block);
        var battle = CreateBattle(selfOrAllySkill);
        var actor = battle.Allies[0];
        var livingAlly = battle.Allies[1];
        var enemy = battle.Enemies[0];

        Assert.Equal(
            [actor.Identity.Id],
            SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfOrAllySkill, actor)
                .Select(combatant => combatant.Identity.Id));
        Assert.Equal(
            [livingAlly.Identity.Id],
            SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfOrAllySkill, livingAlly)
                .Select(combatant => combatant.Identity.Id));
        Assert.Empty(SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfOrAllySkill, enemy));

        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        Assert.NotNull(PlayerActionBuilder.TryCreate(battle, simulator, actor, 0, actor));
        Assert.NotNull(PlayerActionBuilder.TryCreate(battle, simulator, actor, 0, livingAlly));
        Assert.Null(PlayerActionBuilder.TryCreate(battle, simulator, actor, 0, enemy));
    }

    [Fact]
    public void SelfAndAlly_AppliesToBothLivingCombatantsOnTheSameSide()
    {
        var selfAndAllySkill = CreateTokenSkill("self_and_ally", SkillTargetKind.SelfAndAlly, EffectScope.Default, TokenType.Block);
        var battle = CreateBattle(selfAndAllySkill);
        var actor = battle.Allies[0];
        var companion = battle.Allies[1];

        var bothLiving = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfAndAllySkill, selectedCombatant: null);
        Assert.Equal([actor.Identity.Id, companion.Identity.Id], bothLiving.Select(combatant => combatant.Identity.Id));

        companion.Health.CurrentHp = 0;
        companion.Health.IsDead = true;
        var onlyActor = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, selfAndAllySkill, selectedCombatant: null);
        Assert.Equal([actor.Identity.Id], onlyActor.Select(combatant => combatant.Identity.Id));
    }

    [Fact]
    public void UpToThreeEnemies_RespectsTauntStealthAndDead()
    {
        var areaSkill = CreateDamageSkill("iron_maiden", SkillTargetKind.UpToThreeEnemies);
        var battle = CreateBattle(areaSkill, allyCount: 1, enemyCount: 4);
        var actor = battle.Allies[0];
        var enemyOne = battle.Enemies[0];
        var enemyTwo = battle.Enemies[1];
        var enemyThree = battle.Enemies[2];
        var enemyFour = battle.Enemies[3];

        var withoutFilters = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, areaSkill, enemyTwo);
        Assert.Equal(3, withoutFilters.Count);
        Assert.Same(enemyTwo, withoutFilters[0]);
        Assert.Contains(enemyOne, withoutFilters);
        Assert.Contains(enemyThree, withoutFilters);
        Assert.DoesNotContain(enemyFour, withoutFilters);

        enemyFour.Health.IsDead = true;
        enemyThree.Tokens.Add(TokenType.Stealth, 1);
        var afterStealthAndDeath = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, areaSkill, enemyTwo);
        Assert.Equal([enemyTwo.Identity.Id, enemyOne.Identity.Id], afterStealthAndDeath.Select(combatant => combatant.Identity.Id));

        enemyOne.Tokens.Add(TokenType.Taunt, 1);
        Assert.Empty(SkillTargetResolver.ResolvePrimaryTargets(battle, actor, areaSkill, enemyTwo));
        var tauntOnly = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, areaSkill, enemyOne);
        Assert.Equal([enemyOne.Identity.Id], tauntOnly.Select(combatant => combatant.Identity.Id));
    }

    [Fact]
    public void AllEnemies_RespectsTauntStealthAndDead()
    {
        var allEnemiesSkill = CreateDamageSkill("sweep", SkillTargetKind.AllEnemies);
        var battle = CreateBattle(allEnemiesSkill, allyCount: 1, enemyCount: 4);
        var actor = battle.Allies[0];
        battle.Enemies[3].Health.IsDead = true;
        battle.Enemies[2].Tokens.Add(TokenType.Stealth, 1);

        var withoutTaunt = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, allEnemiesSkill, selectedCombatant: null);
        Assert.Equal(
            [battle.Enemies[0].Identity.Id, battle.Enemies[1].Identity.Id],
            withoutTaunt.Select(combatant => combatant.Identity.Id));

        battle.Enemies[1].Tokens.Add(TokenType.Taunt, 1);
        var withTaunt = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, allEnemiesSkill, selectedCombatant: null);
        Assert.Equal([battle.Enemies[1].Identity.Id], withTaunt.Select(combatant => combatant.Identity.Id));
    }

    [Fact]
    public void EffectScope_AllAllies_AppliesToParty_Default_AppliesOnlyToPrimaryHit()
    {
        var defaultScopeSkill = CreateTokenSkill("default_block", SkillTargetKind.Self, EffectScope.Default, TokenType.Block);
        var allAlliesSkill = CreateTokenSkill("allies_block", SkillTargetKind.Self, EffectScope.AllAllies, TokenType.Block);

        var defaultBattle = CreateBattle(defaultScopeSkill);
        ResolveSkill(defaultBattle, defaultScopeSkill, defaultBattle.Allies[0]);
        Assert.Equal(1, defaultBattle.Allies[0].Tokens.GetStacks(TokenType.Block));
        Assert.Equal(0, defaultBattle.Allies[1].Tokens.GetStacks(TokenType.Block));

        var allAlliesBattle = CreateBattle(allAlliesSkill);
        ResolveSkill(allAlliesBattle, allAlliesSkill, allAlliesBattle.Allies[0]);
        Assert.Equal(1, allAlliesBattle.Allies[0].Tokens.GetStacks(TokenType.Block));
        Assert.Equal(1, allAlliesBattle.Allies[1].Tokens.GetStacks(TokenType.Block));
    }

    [Fact]
    public void AreaResolution_IsDeterministic_ForTheSameSeed()
    {
        var areaSkill = CreateDamageSkill("area_strike", SkillTargetKind.UpToThreeEnemies, damageMin: 4, damageMax: 4);
        var firstHpByCombatantId = RunAreaStrike(areaSkill, seed: 17);
        var secondHpByCombatantId = RunAreaStrike(areaSkill, seed: 17);

        Assert.Equal(firstHpByCombatantId, secondHpByCombatantId);
    }

    [Fact]
    public void IsSkillUsable_IgnoresChanceToUse_AndKeepsSelfHpPercentBelowGate()
    {
        var gatedSkill = new SkillDefinition
        {
            Id = "gated_special",
            Name = "Gated",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 1, Max = 1 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = SkillTargetKind.OneEnemy,
            ChanceToUse = 0.01,
            SelfHpPercentBelow = 0.5,
        };
        var battle = CreateBattle(gatedSkill, allyCount: 1, enemyCount: 1);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 20;
        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());

        Assert.False(simulator.IsSkillUsable(actor, gatedSkill));
        actor.Health.CurrentHp = 19;
        Assert.True(simulator.IsSkillUsable(actor, gatedSkill));
    }

    private static Dictionary<string, int> RunAreaStrike(SkillDefinition areaSkill, int seed)
    {
        var battle = CreateBattle(areaSkill, allyCount: 1, enemyCount: 4);
        var simulator = new BattleSimulator(new SeededRandomSource(seed), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = battle.Allies[0],
                Target = battle.Enemies[1],
                Skill = areaSkill,
                ActionType = ActionType.Skill,
            });

        return battle.Enemies.ToDictionary(
            enemy => enemy.Identity.Id,
            enemy => enemy.Health.CurrentHp);
    }

    private static void ResolveSkill(BattleState battle, SkillDefinition skill, Combatant actor)
    {
        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = actor,
                Skill = skill,
                ActionType = ActionType.Skill,
            });
    }

    private static BattleState CreateBattle(SkillDefinition skill, int allyCount = 2, int enemyCount = 4) =>
        BattleFactory.CreateSampleBattle(
            [skill],
            allyCount: allyCount,
            enemyCount: enemyCount,
            allySkillIds: [skill.Id]);

    private static SkillDefinition CreateDamageSkill(
        string skillId,
        SkillTargetKind targetKind,
        int damageMin = 5,
        int damageMax = 5) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = damageMin, Max = damageMax },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = targetKind,
        };

    private static SkillDefinition CreateTokenSkill(
        string skillId,
        SkillTargetKind targetKind,
        EffectScope effectScope,
        TokenType tokenType) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = targetKind,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.ApplyToken,
                    Token = tokenType,
                    Stacks = 1,
                    Chance = 1.0,
                    EffectScope = effectScope,
                },
            ],
        };
}

public sealed class SkillContractValidationTests
{
    [Fact]
    public void ComboBonus_InJson_FailsLoad()
    {
        var skillsPath = Path.Combine(Path.GetTempPath(), $"skills-combo-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            skillsPath,
            """
            [
              {
                "id": "illegal_combo",
                "name": "Illegal Combo",
                "element": "None",
                "type": "Active",
                "targetKind": "OneEnemy",
                "baseDamage": { "min": 1, "max": 1 },
                "baseCritChance": 0,
                "accuracy": 1.0,
                "effectsOnHit": [],
                "comboBonus": []
              }
            ]
            """);

        try
        {
            var exception = Assert.Throws<InvalidDataException>(() => CombatDataLoader.LoadSkills(skillsPath));
            Assert.Contains("comboBonus", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(skillsPath);
        }
    }

    [Fact]
    public void CanonicalSkillsJson_LoadsWithoutComboBonus()
    {
        var skills = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath());
        Assert.Contains(skills, skill => skill.Id == "wulfricBasicHit" && skill.TargetKind == SkillTargetKind.OneEnemy);
        Assert.Contains(skills, skill => skill.Id == "wulfricAreaAttack" && skill.TargetKind == SkillTargetKind.UpToThreeEnemies);
        Assert.Contains(skills, skill => skill.Id == "mariaHealVoice" && skill.TargetKind == SkillTargetKind.SelfOrAlly);
        Assert.Contains(skills, skill => skill.Id == "wulfric_innate_cleave" && skill.TargetKind == SkillTargetKind.OneEnemy);
    }
}

public sealed class CombatHealUnlockTests
{
    [Fact]
    public void HealHp_AppliesInCombat_WhenUnlocked()
    {
        Assert.True(CombatHealUnlock.IsCombatHealingUnlocked);

        var healSkill = new SkillDefinition
        {
            Id = "test_heal",
            Name = "Test Heal",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = SkillTargetKind.Self,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.HealHp,
                    Potency = 10,
                    Chance = 1.0,
                    EffectScope = EffectScope.Default,
                },
            ],
        };
        var battle = BattleFactory.CreateSampleBattle(
            [healSkill],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [healSkill.Id]);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 10;
        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = actor,
                Skill = healSkill,
                ActionType = ActionType.Skill,
            });

        Assert.Equal(20, actor.Health.CurrentHp);
    }
}
