using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Progression;

namespace Game.Tests;

public sealed class HeroKitSkillTests
{
    private static readonly string[] RequiredHeroSkillIds =
    [
        "wulfricBasicHit", "wulfricTaunt", "wulfricAreaAttack", "wulfricRaiseShield",
        "wulfricUnstable", "wulfricStabilize", "wulfricNocontrol",
        "wulfricWhip", "wulfricBigSword", "wulfricForceExplosion",
        "wulfricShieldAttack", "wulfricDefendAlly", "wulfricFrenzy",
        "buckBasicHit", "buckPistol", "buckRevolver", "buckRifle",
        "buckSpiderHands", "buckAllGuns", "buckJuggle",
        "buckSnakeVision", "buckSnakeBite", "buckSnakeTail",
        "buckMark", "buckPistolHeadShot", "buckLuckManipulation",
        "mariaBasicHit", "mariaHealVoice", "mariaScreamAttack", "mariaDamageBuff",
        "mariaEchoHeal", "mariaCleanse", "mariaResurrection",
        "mariaChanceBuff", "mariaDefenseBuff", "mariaShow",
        "mariaScreechNoise", "mariaPiercingYell", "mariaChaosMelody",
    ];

    [Fact]
    public void CanonicalSkillsJson_LoadsAllHeroKitSkillIds()
    {
        var skillsById = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
            .ToDictionary(skill => skill.Id, StringComparer.Ordinal);

        foreach (var skillId in RequiredHeroSkillIds)
        {
            Assert.True(skillsById.ContainsKey(skillId), $"Missing skill id: {skillId}");
        }

        Assert.DoesNotContain(
            CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath()),
            skill => skill.Id.Contains("comboBonus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CanonicalSkillsJson_HasNoComboBonusProperty()
    {
        var skillsJson = File.ReadAllText(CombatDataLoader.ResolveDefaultSkillsPath());
        Assert.DoesNotContain("comboBonus", skillsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WulfricIronMaiden_ResolvesUpToThreeEnemies()
    {
        var skills = SampleCombatData.CreateSkills();
        var ironMaiden = skills.First(skill => skill.Id == "wulfricAreaAttack");
        Assert.Equal(SkillTargetKind.UpToThreeEnemies, ironMaiden.TargetKind);

        var battle = BattleFactory.CreateSampleBattle(
            skills,
            allyCount: 1,
            enemyCount: 4,
            allySkillIds: [ironMaiden.Id]);
        var actor = battle.Allies[0];
        var selectedEnemy = battle.Enemies[1];
        var primaryTargets = SkillTargetResolver.ResolvePrimaryTargets(battle, actor, ironMaiden, selectedEnemy);
        Assert.Equal(3, primaryTargets.Count);
        Assert.Contains(primaryTargets, combatant => combatant.Identity.Id == selectedEnemy.Identity.Id);
    }

    [Fact]
    public void WulfricTaunt_AppliesTauntAndControlledInstability()
    {
        var skills = SampleCombatData.CreateSkills();
        var tauntSkill = skills.First(skill => skill.Id == "wulfricTaunt");
        var battle = BattleFactory.CreateSampleBattle(
            skills,
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [tauntSkill.Id]);
        var actor = battle.Allies[0];
        var simulator = new BattleSimulator(new SeededRandomSource(7), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = actor,
                Skill = tauntSkill,
                ActionType = ActionType.Skill,
            });

        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.Taunt));
        Assert.Equal(2, actor.Tokens.GetStacks(TokenType.ControlledInstability));
    }

    [Fact]
    public void BuckUnload_HasHitCountThree()
    {
        var unload = SampleCombatData.CreateSkills().First(skill => skill.Id == "buckRevolver");
        Assert.Equal(3, unload.HitCount);
    }

    [Fact]
    public void MariaHealVoice_HealsWhenCombatHealingUnlocked()
    {
        Assert.True(CombatHealUnlock.IsCombatHealingUnlocked);
        var skills = SampleCombatData.CreateSkills();
        var healSkill = skills.First(skill => skill.Id == "mariaHealVoice");
        var battle = BattleFactory.CreateSampleBattle(
            skills,
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [healSkill.Id]);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 10;
        var hpBefore = actor.Health.CurrentHp;
        var simulator = new BattleSimulator(new SeededRandomSource(11), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = actor,
                Skill = healSkill,
                ActionType = ActionType.Skill,
            });

        Assert.True(actor.Health.CurrentHp > hpBefore);
        Assert.InRange(actor.Health.CurrentHp - hpBefore, 5, 10);
    }

    [Fact]
    public void LossOfControl_ConsumesControlledInstabilityAndDamagesEnemies()
    {
        var skills = SampleCombatData.CreateSkills();
        var lossOfControl = skills.First(skill => skill.Id == "wulfricNocontrol");
        var battle = BattleFactory.CreateSampleBattle(
            skills,
            allyCount: 1,
            enemyCount: 2,
            allySkillIds: [lossOfControl.Id]);
        var actor = battle.Allies[0];
        actor.Tokens.Add(TokenType.ControlledInstability, 3);
        var enemyHpBefore = battle.Enemies.Select(enemy => enemy.Health.CurrentHp).ToArray();
        var simulator = new BattleSimulator(new SeededRandomSource(3), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = battle.Enemies[0],
                Skill = lossOfControl,
                ActionType = ActionType.Skill,
            });

        Assert.Equal(0, actor.Tokens.GetStacks(TokenType.ControlledInstability));
        Assert.Equal(18, enemyHpBefore[0] - battle.Enemies[0].Health.CurrentHp);
        Assert.Equal(18, enemyHpBefore[1] - battle.Enemies[1].Health.CurrentHp);
        // Self damage: steps=1 per consumed stack => 3 HP from MaxHp 40
        Assert.Equal(37, actor.Health.CurrentHp);
    }

    [Fact]
    public void DefenseToken_ReducesIncomingDamage()
    {
        var smack = new SkillDefinition
        {
            Id = "test_smack_defense",
            Name = "Smack",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 20, Max = 20 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };
        var battle = BattleFactory.CreateSampleBattle(
            [smack],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [smack.Id],
            enemySkillIds: [smack.Id]);
        var attacker = battle.Enemies[0];
        var defender = battle.Allies[0];
        attacker.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        defender.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        defender.Tokens.Add(TokenType.Defense, 1);
        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = attacker,
                Target = defender,
                Skill = smack,
                ActionType = ActionType.Skill,
            });

        // 20 * 0.75 Defense = 15
        Assert.Equal(25, defender.Health.CurrentHp);
    }

    [Fact]
    public void StrengthToken_IncreasesOutgoingDamage()
    {
        var smack = new SkillDefinition
        {
            Id = "test_smack_strength",
            Name = "Smack",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 20, Max = 20 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };
        var battle = BattleFactory.CreateSampleBattle(
            [smack],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [smack.Id]);
        var attacker = battle.Allies[0];
        attacker.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        attacker.Tokens.Add(TokenType.Strength, 1);
        var defender = battle.Enemies[0];
        defender.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = attacker,
                Target = defender,
                Skill = smack,
                ActionType = ActionType.Skill,
            });

        // 20 * 1.25 Strength = 25 vs MaxHp 20
        Assert.Equal(0, defender.Health.CurrentHp);
        Assert.True(defender.Health.IsDead);
    }

    [Fact]
    public void Destabilization_TriggersOnDeath_DamagesAllOtherLiving()
    {
        var smack = new SkillDefinition
        {
            Id = "test_smack_destab",
            Name = "Smack",
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 100, Max = 100 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };
        var battle = BattleFactory.CreateSampleBattle(
            [smack],
            allyCount: 1,
            enemyCount: 2,
            allySkillIds: [smack.Id]);
        var actor = battle.Allies[0];
        var explodingEnemy = battle.Enemies[0];
        var otherEnemy = battle.Enemies[1];
        explodingEnemy.Tokens.Add(TokenType.Destabilization, 2);
        var otherEnemyHpBefore = otherEnemy.Health.CurrentHp;
        var allyHpBefore = actor.Health.CurrentHp;
        var simulator = new BattleSimulator(new SeededRandomSource(1), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = explodingEnemy,
                Skill = smack,
                ActionType = ActionType.Skill,
            });

        Assert.True(explodingEnemy.Health.IsDead);
        Assert.Equal(0, explodingEnemy.Tokens.GetStacks(TokenType.Destabilization));
        Assert.Equal(6, otherEnemyHpBefore - otherEnemy.Health.CurrentHp);
        Assert.Equal(6, allyHpBefore - actor.Health.CurrentHp);
    }

    [Fact]
    public void MultiTargetSkill_IsDeterministicWithSameSeed()
    {
        var skills = SampleCombatData.CreateSkills();
        var ironMaiden = skills.First(skill => skill.Id == "wulfricAreaAttack");

        int RunOnce(int seed)
        {
            var battle = BattleFactory.CreateSampleBattle(
                skills,
                allyCount: 1,
                enemyCount: 3,
                allySkillIds: [ironMaiden.Id]);
            var actor = battle.Allies[0];
            var simulator = new BattleSimulator(new SeededRandomSource(seed), new CombatEventCollector());
            simulator.ResolveChosenAction(
                battle,
                new ChosenAction
                {
                    Actor = actor,
                    Target = battle.Enemies[0],
                    Skill = ironMaiden,
                    ActionType = ActionType.Skill,
                });
            return battle.Enemies.Sum(enemy => enemy.Health.CurrentHp);
        }

        Assert.Equal(RunOnce(42), RunOnce(42));
    }

    [Fact]
    public void SkillTrees_IncludeMariaAndNewActiveNodeIds()
    {
        var trees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        Assert.Contains(trees, character => character.CharacterId == "maria");
        var wulfric = SkillTreeLookup.FindCharacterTrees(trees, "wulfric");
        Assert.NotNull(wulfric);
        Assert.True(SkillTreeLookup.TryFindNode(wulfric!, "wulfricUnstable", out _, out _));
        var buck = SkillTreeLookup.FindCharacterTrees(trees, "buck");
        Assert.NotNull(buck);
        Assert.True(SkillTreeLookup.TryFindNode(buck!, "buckAllGuns", out _, out _));
        var maria = SkillTreeLookup.FindCharacterTrees(trees, "maria");
        Assert.NotNull(maria);
        Assert.True(SkillTreeLookup.TryFindNode(maria!, "mariaHealVoice", out _, out _) == false);
        Assert.True(SkillTreeLookup.TryFindNode(maria!, "mariaEchoHeal", out _, out _));
    }
}
