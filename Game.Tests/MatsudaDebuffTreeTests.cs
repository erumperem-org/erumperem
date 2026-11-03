using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Tests;

/// <summary>Deafening Scream (tree 3) actives and passives, driven through the real catalog and simulator.</summary>
public sealed class MatsudaDebuffTreeTests
{
    private const string SoundStrike = "matsuda_innate_active1";
    private const string HealingVoice = "matsuda_innate_active2";
    private const string AmplifiedScream = "matsuda_innate_active3";
    private const string InspirationalSong = "matsuda_innate_active4";
    private const string Screech = "matsuda_tree3_tier1_active";
    private const string Piercing = "matsuda_tree3_tier2_active";
    private const string Chaos = "matsuda_tree3_tier3_active";

    [Fact]
    public void TreeActives_MatchDesign()
    {
        var skills = SampleCombatData.CreateSkills().ToDictionary(s => s.Id, StringComparer.Ordinal);
        AssertActive(skills[Screech], 4, SkillTargetKind.OneEnemy, 10, 12, 0.8, TokenType.Clumsy, 2);
        AssertActive(skills[Piercing], 6, SkillTargetKind.AllEnemies, 7, 9, 0.7, TokenType.Clumsy, 3);
        AssertActive(skills[Chaos], 8, SkillTargetKind.AllEnemies, 2, 6, 0.7, TokenType.Confusion, 2);
    }

    [Fact]
    public void TreeActives_AreReachableViaTreeNodes()
    {
        var trees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        var ids = trees.Where(t => t.CharacterId == "matsuda")
            .SelectMany(t => t.Trees).SelectMany(t => t.Tiers).SelectMany(t => t.Nodes)
            .Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(Screech, ids);
        Assert.Contains(Piercing, ids);
        Assert.Contains(Chaos, ids);
    }

    [Fact]
    public void ChaosMelody_Applies2ConfusionToAllEnemiesHit()
    {
        var battle = CreateBattle();
        Resolve(battle, Chaos, battle.Allies[0], battle.Enemies[0], new FixedRandom(0.0));
        foreach (var enemy in battle.Enemies)
        {
            Assert.Equal(2, enemy.Tokens.GetStacks(TokenType.Confusion));
        }
    }

    [Fact]
    public void T1P1_ApplyingDebuff_DealsDamageEqualToDebuffsApplied()
    {
        var withPassive = CreateBattle("matsuda_tree3_tier1_passive1");
        var baseline = CreateBattle();
        Resolve(withPassive, Screech, withPassive.Allies[0], withPassive.Enemies[0]);
        Resolve(baseline, Screech, baseline.Allies[0], baseline.Enemies[0]);
        Assert.Equal(baseline.Enemies[0].Health.CurrentHp - 2, withPassive.Enemies[0].Health.CurrentHp);
    }

    [Fact]
    public void T1P2_HittingDebuffedEnemy_Heals6At50Percent()
    {
        var hit = CreateBattle("matsuda_tree3_tier1_passive2");
        hit.PassiveTriggerRandom = new FixedRandom(0.1);
        hit.Allies[0].Health.CurrentHp = 10;
        hit.Enemies[0].Tokens.Add(TokenType.Weaken, 1);
        Resolve(hit, SoundStrike, hit.Allies[0], hit.Enemies[0]);
        Assert.Equal(16, hit.Allies[0].Health.CurrentHp);

        var miss = CreateBattle("matsuda_tree3_tier1_passive2");
        miss.PassiveTriggerRandom = new FixedRandom(0.9);
        miss.Allies[0].Health.CurrentHp = 10;
        miss.Enemies[0].Tokens.Add(TokenType.Weaken, 1);
        Resolve(miss, SoundStrike, miss.Allies[0], miss.Enemies[0]);
        Assert.Equal(10, miss.Allies[0].Health.CurrentHp);

        var clean = CreateBattle("matsuda_tree3_tier1_passive2");
        clean.PassiveTriggerRandom = new FixedRandom(0.1);
        clean.Allies[0].Health.CurrentHp = 10;
        Resolve(clean, SoundStrike, clean.Allies[0], clean.Enemies[0]);
        Assert.Equal(10, clean.Allies[0].Health.CurrentHp);
    }

    [Fact]
    public void T1P3_TakesLessDamageFromDebuffedEnemies()
    {
        var plain = EnemyHitsMatsuda("matsuda_tree3_tier1_passive3", debuffedEnemy: false);
        var debuffed = EnemyHitsMatsuda("matsuda_tree3_tier1_passive3", debuffedEnemy: true);
        var noPassive = EnemyHitsMatsuda(null, debuffedEnemy: true);
        Assert.Equal(plain, noPassive);
        Assert.True(debuffed < plain, $"debuffed {debuffed} plain {plain}");
    }

    [Fact]
    public void T2P1_BasicHit_40Percent_Applies2Weaken()
    {
        var hit = CreateBattle("matsuda_tree3_tier2_passive1");
        hit.PassiveTriggerRandom = new FixedRandom(0.3);
        Resolve(hit, SoundStrike, hit.Allies[0], hit.Enemies[0]);
        Assert.Equal(2, hit.Enemies[0].Tokens.GetStacks(TokenType.Weaken));

        var miss = CreateBattle("matsuda_tree3_tier2_passive1");
        miss.PassiveTriggerRandom = new FixedRandom(0.5);
        Resolve(miss, SoundStrike, miss.Allies[0], miss.Enemies[0]);
        Assert.Equal(0, miss.Enemies[0].Tokens.GetStacks(TokenType.Weaken));

        var other = CreateBattle("matsuda_tree3_tier2_passive1");
        other.PassiveTriggerRandom = new FixedRandom(0.0);
        Resolve(other, AmplifiedScream, other.Allies[0], other.Enemies[0]);
        Assert.Equal(0, other.Enemies[0].Tokens.GetStacks(TokenType.Weaken));
    }

    [Fact]
    public void T2P2_HealingSkill_40Percent_Weakens1OnAllEnemies()
    {
        var hit = CreateBattle("matsuda_tree3_tier2_passive2");
        hit.PassiveTriggerRandom = new FixedRandom(0.3);
        Resolve(hit, HealingVoice, hit.Allies[0], hit.Allies[0]);
        Assert.All(hit.Enemies, e => Assert.Equal(1, e.Tokens.GetStacks(TokenType.Weaken)));

        var miss = CreateBattle("matsuda_tree3_tier2_passive2");
        miss.PassiveTriggerRandom = new FixedRandom(0.5);
        Resolve(miss, HealingVoice, miss.Allies[0], miss.Allies[0]);
        Assert.All(miss.Enemies, e => Assert.Equal(0, e.Tokens.GetStacks(TokenType.Weaken)));
    }

    [Fact]
    public void T2P3_BuffSkill_40Percent_Vulnerability1OnAllEnemies()
    {
        var hit = CreateBattle("matsuda_tree3_tier2_passive3");
        hit.PassiveTriggerRandom = new FixedRandom(0.3);
        Resolve(hit, InspirationalSong, hit.Allies[0], hit.Allies[0]);
        Assert.All(hit.Enemies, e => Assert.Equal(1, e.Tokens.GetStacks(TokenType.Vulnerability)));

        var miss = CreateBattle("matsuda_tree3_tier2_passive3");
        miss.PassiveTriggerRandom = new FixedRandom(0.5);
        Resolve(miss, InspirationalSong, miss.Allies[0], miss.Allies[0]);
        Assert.All(miss.Enemies, e => Assert.Equal(0, e.Tokens.GetStacks(TokenType.Vulnerability)));
    }

    [Theory]
    [InlineData("matsuda_tree3_tier3_passive1", TokenType.Weaken)]
    [InlineData("matsuda_tree3_tier3_passive3", TokenType.Vulnerability)]
    public void T3_TeamHit_DebuffsTheAttacker(string passive, TokenType token)
    {
        foreach (var victimIndex in new[] { 0, 1 })
        {
            var battle = CreateBattle(passive);
            var attacker = battle.Enemies[0];
            Resolve(battle, SoundStrike, attacker, battle.Allies[victimIndex]);
            Assert.Equal(1, attacker.Tokens.GetStacks(token));
            Assert.Equal(0, battle.Enemies[1].Tokens.GetStacks(token));
        }
    }

    [Fact]
    public void T3P2_DebuffTokensAre50PercentMoreEffective()
    {
        var battle = CreateBattle("matsuda_tree3_tier3_passive2");
        var matsuda = battle.Allies[0];
        var enemy = battle.Enemies[0];
        Resolve(battle, Screech, matsuda, enemy);
        Assert.Equal(1.5, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, enemy, TokenType.Clumsy, matsuda), 5);
        var plain = CreateBattle();
        Resolve(plain, Screech, plain.Allies[0], plain.Enemies[0]);
        Assert.Equal(1.0, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(plain, plain.Enemies[0], TokenType.Clumsy, plain.Allies[0]), 5);
    }

    private static int EnemyHitsMatsuda(string? passive, bool debuffedEnemy)
    {
        var battle = CreateBattle(passive);
        var enemy = battle.Enemies[0];
        enemy.Tokens.Add(TokenType.Strength, 4);
        if (debuffedEnemy)
        {
            enemy.Tokens.Add(TokenType.Clumsy, 1);
        }

        var victim = battle.Allies[0];
        var before = victim.Health.CurrentHp;
        Resolve(battle, SoundStrike, enemy, victim);
        return before - victim.Health.CurrentHp;
    }

    private static void AssertActive(SkillDefinition s, int cost, SkillTargetKind kind, int min, int max, double acc, TokenType token, int stacks)
    {
        Assert.Equal(cost, s.CorruptionCost);
        Assert.Equal(ElementType.Anomaly, s.Element);
        Assert.Equal(kind, s.TargetKind);
        Assert.Equal(min, s.BaseDamage.Min);
        Assert.Equal(max, s.BaseDamage.Max);
        Assert.Equal(0, s.BaseCritChance, 5);
        Assert.Equal(acc, s.Accuracy, 5);
        Assert.Contains(s.EffectsOnHit, e => e.Type == EffectType.ApplyToken && e.Token == token && e.Stacks == stacks);
    }

    private static BattleState CreateBattle(params string?[] unlocked)
    {
        var skills = SampleCombatData.CreateSkills();
        var passives = SampleCombatData.CreatePassives().ToDictionary(p => p.Id, StringComparer.Ordinal);
        var battle = BattleFactory.CreateSampleBattle(
            skills, 2, 2, 0.0,
            [SoundStrike, HealingVoice, AmplifiedScream, InspirationalSong, Screech, Piercing, Chaos], null, passives);
        foreach (var ally in battle.Allies.Take(1))
        {
            foreach (var id in unlocked)
            {
                if (id is not null)
                {
                    ally.Progression.UnlockedNodes[id] = true;
                }
            }
        }

        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
            combatant.Stats = new StatsComponent { Speed = combatant.Identity.Faction == Faction.Player ? 6 : 4, Accuracy = 1.0, CritChance = 0 };
            combatant.Tokens.Add(TokenType.BonusAction, 1);
        }

        battle.PassiveTriggerRandom = new FixedRandom(0.99);
        return battle;
    }

    private static void Resolve(BattleState battle, string skillId, Combatant actor, Combatant target, IRandomSource? random = null)
    {
        var simulator = new BattleSimulator(random ?? new HitNoCritRandom(), new CombatEventCollector());
        simulator.ResolveChosenAction(battle, new ChosenAction
        {
            Actor = actor,
            Target = target,
            Skill = battle.SkillsById[skillId],
            ActionType = ActionType.Skill,
        });
    }

    private sealed class FixedRandom(double value) : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble() => value;
    }

    private sealed class HitNoCritRandom : IRandomSource
    {
        private bool _crit;
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble()
        {
            _crit = !_crit;
            return _crit ? 0.0 : 0.99;
        }
    }
}
