using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Tests;

/// <summary>Life Symphony (tree 1) and Battle Hymn (tree 2): tree actives and the 18 passives over the shipped catalog.</summary>
public sealed class MatsudaSupportTreePassiveTests
{
    private const string SoundStrike = "matsuda_innate_active1";
    private const string HealingVoice = "matsuda_innate_active2";
    private const string AmplifiedScream = "matsuda_innate_active3";
    private const string InspirationalSong = "matsuda_innate_active4";
    private const string ProtectionSong = "matsuda_tree2_tier2_active";

    // ---------- tree actives ----------

    [Theory]
    [InlineData("matsuda_tree1_tier1_active", 6, SkillTargetKind.SelfAndAlly, 0.75)]
    [InlineData("matsuda_tree1_tier2_active", 6, SkillTargetKind.SelfAndAlly, 0.60)]
    [InlineData("matsuda_tree1_tier3_active", 20, SkillTargetKind.SelfAndAlly, 1.0)]
    [InlineData("matsuda_tree2_tier1_active", 4, SkillTargetKind.SelfOrAlly, 0.85)]
    [InlineData("matsuda_tree2_tier2_active", 5, SkillTargetKind.SelfAndAlly, 0.75)]
    [InlineData("matsuda_tree2_tier3_active", 9, SkillTargetKind.SelfAndAlly, 0.85)]
    public void TreeActive_MatchesDesign_AndIsReachableFromTreeNode(string skillId, int cost, SkillTargetKind kind, double accuracy)
    {
        var skill = SampleCombatData.CreateSkills().Single(s => s.Id == skillId);
        Assert.Equal(cost, skill.CorruptionCost);
        Assert.Equal(kind, skill.TargetKind);
        Assert.Equal(accuracy, skill.Accuracy, 5);
        var catalogText = File.ReadAllText(Path.Combine(CombatDataLoader.ResolveDefaultCatalogDirectory(), "skill_trees.json"));
        Assert.Contains($"\"{skillId}\"", catalogText, StringComparison.Ordinal);
    }

    [Fact]
    public void ResurrectionHymn_RevivesDeadAlly()
    {
        var battle = CreateBattle();
        var matsuda = battle.Allies[0];
        var fallen = battle.Allies[1];
        fallen.Health.CurrentHp = 0;
        fallen.Health.IsDead = true;

        Resolve(battle, "matsuda_tree1_tier3_active", matsuda, fallen);

        Assert.False(fallen.Health.IsDead);
        Assert.True(fallen.Health.CurrentHp > 0);
    }

    // ---------- Life Symphony ----------

    [Fact]
    public void LifeSymphony_T1P1_HealingVoiceFiftyPercentMoreEffective()
    {
        var battle = CreateBattle("matsuda_tree1_tier1_passive1");
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(10 + 8, battle.Allies[1].Health.CurrentHp); // roll 5 * 1.5
    }

    [Fact]
    public void LifeSymphony_T1P2_HealingGrantsMatsudaTwoStrength()
    {
        var battle = CreateBattle("matsuda_tree1_tier1_passive2");
        battle.Allies[1].Health.CurrentHp = 10;
        battle.Allies[0].Tokens.Add(TokenType.BonusAction, 1); // keep the turn open so end-of-turn decay does not eat a stack
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(2, battle.Allies[0].Tokens.GetStacks(TokenType.Strength));
    }

    [Fact]
    public void LifeSymphony_T1P3_HealedAllyMayGainTwoDefense()
    {
        var battle = CreateBattle("matsuda_tree1_tier1_passive3");
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(2, battle.Allies[1].Tokens.GetStacks(TokenType.Defense));

        battle = CreateBattle("matsuda_tree1_tier1_passive3");
        battle.PassiveTriggerRandom = new ConstantRandom(0.5);
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(0, battle.Allies[1].Tokens.GetStacks(TokenType.Defense));
    }

    [Fact]
    public void LifeSymphony_T2P1_ApplyingBuffHealsRecipientThree()
    {
        var battle = CreateBattle("matsuda_tree1_tier2_passive1");
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, InspirationalSong, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(13, battle.Allies[1].Health.CurrentHp);
    }

    [Fact]
    public void LifeSymphony_T2P2_ApplyingDebuffHealsMatsudaThree()
    {
        var battle = CreateBattle("matsuda_tree1_tier2_passive2");
        battle.Allies[0].Health.CurrentHp = 10;
        Resolve(battle, AmplifiedScream, battle.Allies[0], battle.Enemies[0]); // AllEnemies: one debuff application per enemy, 2 enemies
        Assert.Equal(16, battle.Allies[0].Health.CurrentHp);
    }

    [Fact]
    public void LifeSymphony_T2P3_FirstTurnStartGivesTeamThreeRegenerationOnce()
    {
        var battle = CreateBattle("matsuda_tree1_tier2_passive3");
        battle.PassiveBus.RaiseTurnStarted(battle, battle.Allies[0], null);
        Assert.Equal(3, battle.Allies[0].Tokens.GetStacks(TokenType.Regeneration));
        Assert.Equal(3, battle.Allies[1].Tokens.GetStacks(TokenType.Regeneration));

        battle.Allies[0].Tokens.ConsumeOne(TokenType.Regeneration);
        battle.PassiveBus.RaiseTurnStarted(battle, battle.Allies[0], null);
        Assert.Equal(2, battle.Allies[0].Tokens.GetStacks(TokenType.Regeneration));
    }

    [Fact]
    public void LifeSymphony_T3P1_Lifesteal_HealsHalfOfDamageDealt()
    {
        var battle = CreateBattle("matsuda_tree1_tier3_passive1");
        var matsuda = battle.Allies[0];
        matsuda.Health.CurrentHp = 20;
        var enemy = battle.Enemies[0];
        var before = enemy.Health.CurrentHp;
        Resolve(battle, SoundStrike, matsuda, enemy, new HitNoCritRandom());
        var dealt = before - enemy.Health.CurrentHp;
        Assert.Equal(4, dealt);
        Assert.Equal(22, matsuda.Health.CurrentHp);
    }

    [Fact]
    public void LifeSymphony_T3P2_BelowQuarterHpAtTurnStart_RegenerationOncePerBattle()
    {
        var battle = CreateBattle("matsuda_tree1_tier3_passive2");
        var matsuda = battle.Allies[0];
        battle.PassiveBus.RaiseTurnStarted(battle, matsuda, null);
        Assert.Equal(0, matsuda.Tokens.GetStacks(TokenType.Regeneration));

        matsuda.Health.CurrentHp = 10; // 10/70 < 25%
        battle.PassiveBus.RaiseTurnStarted(battle, matsuda, null);
        Assert.Equal(5, matsuda.Tokens.GetStacks(TokenType.Regeneration));

        matsuda.Tokens.ConsumeOne(TokenType.Regeneration);
        battle.PassiveBus.RaiseTurnStarted(battle, matsuda, null);
        Assert.Equal(4, matsuda.Tokens.GetStacks(TokenType.Regeneration));
    }

    [Fact]
    public void LifeSymphony_T3P3_HealingSkillsMayDouble()
    {
        var battle = CreateBattle("matsuda_tree1_tier3_passive3");
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(20, battle.Allies[1].Health.CurrentHp); // 5 doubled

        battle = CreateBattle("matsuda_tree1_tier3_passive3");
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1], new ConstantRandom(0.5));
        Assert.Equal(15, battle.Allies[1].Health.CurrentHp);
    }

    // ---------- Battle Hymn ----------

    [Fact]
    public void BattleHymn_T1P1_InspirationalSongGainsTenPercentAccuracy()
    {
        var battle = CreateBattle("matsuda_tree2_tier1_passive1");
        AssertSkillAccuracyBonus(battle, InspirationalSong, 0.1);
        AssertSkillAccuracyBonus(battle, AmplifiedScream, 0);
    }

    [Fact]
    public void BattleHymn_T1P2_BuffedHitterHealsWholeTeam()
    {
        var battle = CreateBattle("matsuda_tree2_tier1_passive2");
        battle.Allies[0].Health.CurrentHp = 10;
        battle.Allies[1].Health.CurrentHp = 10;
        battle.Allies[1].Tokens.Add(TokenType.Dexterity, 1);
        Resolve(battle, SoundStrike, battle.Allies[1], battle.Enemies[0]);
        Assert.Equal(13, battle.Allies[0].Health.CurrentHp);
        Assert.Equal(13, battle.Allies[1].Health.CurrentHp);
    }

    [Fact]
    public void BattleHymn_T1P2_UnbuffedHitterDoesNotHeal()
    {
        var battle = CreateBattle("matsuda_tree2_tier1_passive2");
        battle.Allies[0].Health.CurrentHp = 10;
        Resolve(battle, SoundStrike, battle.Allies[1], battle.Enemies[0]);
        Assert.Equal(10, battle.Allies[0].Health.CurrentHp);
    }

    [Fact]
    public void BattleHymn_T1P3_TokenOnAllyAlsoGrantsOneStrength()
    {
        var battle = CreateBattle("matsuda_tree2_tier1_passive3");
        Resolve(battle, ProtectionSong, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(1, battle.Allies[1].Tokens.GetStacks(TokenType.Strength));
        Assert.Equal(0, battle.Allies[0].Tokens.GetStacks(TokenType.Strength));
    }

    [Fact]
    public void BattleHymn_T2P1_HealingAllyGrantsOneStrength()
    {
        var battle = CreateBattle("matsuda_tree2_tier2_passive1");
        battle.Allies[1].Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(1, battle.Allies[1].Tokens.GetStacks(TokenType.Strength));
    }

    [Fact]
    public void BattleHymn_T2P2_TokenOnAllyMayGrantOneDefense()
    {
        var battle = CreateBattle("matsuda_tree2_tier2_passive2");
        Resolve(battle, InspirationalSong, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(1, battle.Allies[1].Tokens.GetStacks(TokenType.Defense));

        battle = CreateBattle("matsuda_tree2_tier2_passive2");
        battle.PassiveTriggerRandom = new ConstantRandom(0.9);
        Resolve(battle, InspirationalSong, battle.Allies[0], battle.Allies[1]);
        Assert.Equal(0, battle.Allies[1].Tokens.GetStacks(TokenType.Defense));
    }

    [Fact]
    public void BattleHymn_T2P3_ScreamGainsTwentyPercentAccuracy()
    {
        var battle = CreateBattle("matsuda_tree2_tier2_passive3");
        AssertSkillAccuracyBonus(battle, AmplifiedScream, 0.2);
        AssertSkillAccuracyBonus(battle, InspirationalSong, 0);
    }

    [Fact]
    public void BattleHymn_T3P1_EnemyHitGivesTeamOneDexterity()
    {
        var battle = CreateBattle("matsuda_tree2_tier3_passive1");
        battle.Allies[1].Tokens.Add(TokenType.BonusAction, 1); // keep the hitter's turn open (no end-of-turn decay)
        Resolve(battle, SoundStrike, battle.Allies[1], battle.Enemies[0]);
        Assert.Equal(1, battle.Allies[0].Tokens.GetStacks(TokenType.Dexterity));
        Assert.Equal(1, battle.Allies[1].Tokens.GetStacks(TokenType.Dexterity));
    }

    [Fact]
    public void BattleHymn_T3P2_BuffedTeamTakesTenPercentLessDamage()
    {
        var hit = CreateFixedDamageSkill("test_enemy_hit", 20);

        var battle = CreateBattle("matsuda_tree2_tier3_passive2");
        battle.Allies[1].Tokens.Add(TokenType.Dexterity, 1);
        ResolveSkill(battle, hit, battle.Enemies[0], battle.Allies[1]);
        Assert.Equal(18, battle.Allies[1].Health.MaxHp - battle.Allies[1].Health.CurrentHp);

        var unbuffed = CreateBattle("matsuda_tree2_tier3_passive2");
        ResolveSkill(unbuffed, hit, unbuffed.Enemies[0], unbuffed.Allies[1]);
        Assert.Equal(20, unbuffed.Allies[1].Health.MaxHp - unbuffed.Allies[1].Health.CurrentHp);
    }

    [Fact]
    public void BattleHymn_T3P3_BuffedTeamCritsDealTriple()
    {
        var strike = CreateFixedDamageSkill("test_ally_hit", 4);

        var battle = CreateBattle("matsuda_tree2_tier3_passive3");
        var hitter = battle.Allies[1];
        hitter.Stats = new StatsComponent { Speed = 6, Accuracy = 1.0, CritChance = 1.0 };
        hitter.Tokens.Add(TokenType.Dexterity, 1);
        var enemy = battle.Enemies[0];
        var before = enemy.Health.CurrentHp;
        ResolveSkill(battle, strike, hitter, enemy);
        Assert.Equal(12, before - enemy.Health.CurrentHp);

        var plain = CreateBattle();
        plain.Allies[1].Stats = new StatsComponent { Speed = 6, Accuracy = 1.0, CritChance = 1.0 };
        plain.Allies[1].Tokens.Add(TokenType.Dexterity, 1);
        var plainEnemy = plain.Enemies[0];
        before = plainEnemy.Health.CurrentHp;
        ResolveSkill(plain, strike, plain.Allies[1], plainEnemy);
        Assert.Equal(8, before - plainEnemy.Health.CurrentHp);
    }

    // ---------- helpers ----------

    private static void AssertSkillAccuracyBonus(BattleState battle, string skillId, double expected)
    {
        var modifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(
            battle, battle.Allies[0], battle.Enemies[0], battle.SkillsById[skillId]);
        Assert.Equal(expected, modifiers.SkillAccuracyAdditive, 5);
    }

    private static BattleState CreateBattle(params string[] matsudaPassiveIds)
    {
        var skills = SampleCombatData.CreateSkills();
        var passives = SampleCombatData.CreatePassives().ToDictionary(p => p.Id, StringComparer.Ordinal);
        var battle = BattleFactory.CreateSampleBattle(
            skills, 2, 2, 0,
            [
                SoundStrike, HealingVoice, AmplifiedScream, InspirationalSong,
                "matsuda_tree1_tier1_active", "matsuda_tree1_tier2_active", "matsuda_tree1_tier3_active",
                "matsuda_tree2_tier1_active", ProtectionSong, "matsuda_tree2_tier3_active",
            ],
            null, passives);
        foreach (var passiveId in matsudaPassiveIds)
        {
            Assert.True(passives.ContainsKey(passiveId), passiveId);
            battle.Allies[0].Progression.UnlockedNodes[passiveId] = true;
        }

        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
            combatant.Stats = new StatsComponent
            {
                Speed = combatant.Identity.Faction == Faction.Player ? 6 : 4,
                Accuracy = 1.0,
                CritChance = 0,
            };
        }

        battle.PassiveTriggerRandom = new ConstantRandom(0.0);
        return battle;
    }

    private static void Resolve(
        BattleState battle, string skillId, Combatant actor, Combatant target, IRandomSource? random = null) =>
        ResolveSkill(battle, battle.SkillsById[skillId], actor, target, random);

    private static void ResolveSkill(
        BattleState battle, SkillDefinition skill, Combatant actor, Combatant target, IRandomSource? random = null)
    {
        var simulator = new BattleSimulator(random ?? new ConstantRandom(0.0), new CombatEventCollector());
        simulator.ResolveChosenAction(battle, new ChosenAction
        {
            Actor = actor,
            Target = target,
            Skill = skill,
            ActionType = ActionType.Skill,
        });
    }

    private static SkillDefinition CreateFixedDamageSkill(string id, int damage) =>
        new()
        {
            Id = id,
            Name = id,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = damage, Max = damage },
            BaseCritChance = 0,
            Accuracy = 1,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };

    private sealed class HitNoCritRandom : IRandomSource
    {
        private bool _hit = true;
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble()
        {
            _hit = !_hit;
            return _hit ? 0.99 : 0.0;
        }
    }

    /// <summary>Returns a constant from NextDouble: 0.0 always hits and (with 0 crit chance) never crits.</summary>
    private sealed class ConstantRandom(double value) : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble() => value;
    }
}
