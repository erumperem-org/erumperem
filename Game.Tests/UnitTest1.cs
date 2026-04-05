using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;
using Game.Core.Progression;
using Game.Simulations;

namespace Game.Tests;

public class UnitTest1
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(32.9, 0)]
    [InlineData(33, 1)]
    [InlineData(66, 2)]
    [InlineData(99, 3)]
    public void CorruptionTierCalculator_MatchesThresholds(double corruption, int expectedTier)
    {
        var tier = CorruptionTierCalculator.GetTier(corruption);
        Assert.Equal(expectedTier, tier);
    }

    [Fact]
    public void ElementTriangle_UsesAdvantageAndDisadvantage()
    {
        Assert.True(ElementTriangle.HasAdvantage(ElementType.Fire, ElementType.Metal));
        Assert.True(ElementTriangle.HasAdvantage(ElementType.Metal, ElementType.Anomaly));
        Assert.True(ElementTriangle.HasAdvantage(ElementType.Anomaly, ElementType.Fire));
        Assert.False(ElementTriangle.HasAdvantage(ElementType.Metal, ElementType.Fire));
    }

    [Fact]
    public void TokenComponent_ConsumesBlockAndBlockPlus()
    {
        var tokens = new TokenComponent();
        tokens.Add(TokenType.Block, 1);
        tokens.Add(TokenType.BlockPlus, 2);

        Assert.True(tokens.ConsumeOne(TokenType.BlockPlus));
        Assert.Equal(1, tokens.GetStacks(TokenType.BlockPlus));
        Assert.True(tokens.ConsumeOne(TokenType.Block));
        Assert.Equal(0, tokens.GetStacks(TokenType.Block));
    }

    [Fact]
    public void BlindAndDodge_CanCauseMisses()
    {
        var random = new SeededRandomSource(5);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var skills = SampleCombatData.CreateSkills();
        var battle = BattleFactory.CreateSampleBattle(skills, allyCount: 1, enemyCount: 1, corruptionValue: 0);

        var ally = battle.Allies[0];
        var enemy = battle.Enemies[0];
        ally.Tokens.Add(TokenType.Blind, 2);
        enemy.Tokens.Add(TokenType.Dodge, 2);

        simulator.Simulate(battle, maxTurns: 4);
        var hitEvents = collector.Events.Where(combatEvent => combatEvent.EventType == BattleEventType.HitResolved).ToList();
        Assert.NotEmpty(hitEvents);
        Assert.Contains(hitEvents, hitEvent => hitEvent.IsHit == false);
    }

    [Fact]
    public void Blind_IsConsumedWhenChecked_EvenIfAttackHits()
    {
        var random = new SeededRandomSource(9);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var skills = SampleCombatData.CreateSkills();
        var battle = BattleFactory.CreateSampleBattle(skills, allyCount: 1, enemyCount: 1, corruptionValue: 0);

        var ally = battle.Allies[0];
        ally.Stats = new StatsComponent { Speed = 100, Accuracy = 1.0, CritChance = 0.0 };
        ally.Tokens.Add(TokenType.Blind, 1);

        simulator.Simulate(battle, maxTurns: 1);

        Assert.Equal(0, ally.Tokens.GetStacks(TokenType.Blind));
    }

    [Fact]
    public void ApplyStun_UsesStunResistance_AndSkipsTurn()
    {
        var random = new SeededRandomSource(11);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var stunSkill = new SkillDefinition
        {
            Id = "stun_blow",
            Name = "Stun Blow",
            Element = ElementType.Anomaly,
            Type = "Active",
            AllowedCasterRanks = [1, 2, 3, 4],
            AllowedTargetRanks = [1, 2, 3, 4],
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            Cooldown = 0,
            EffectsOnHit = [new EffectSpec { Type = EffectType.ApplyStun, Chance = 1.0, Stacks = 1 }],
        };
        var battle = BattleFactory.CreateSampleBattle([stunSkill], allyCount: 1, enemyCount: 1, corruptionValue: 0);
        var ally = battle.Allies[0];
        var enemy = battle.Enemies[0];
        ally.Stats = new StatsComponent { Speed = 100, Accuracy = 1.0, CritChance = 0.0 };
        ally.SkillLoadout.Skills.Clear();
        ally.SkillLoadout.Skills.Add(stunSkill.Id);
        enemy.Resistances = new ResistanceComponent
        {
            BurnRes = 0,
            BlightRes = 0,
            MoveRes = 0,
            StunRes = 0,
            DeathblowRes = 0,
        };

        simulator.Simulate(battle, maxTurns: 2);

        Assert.Contains(
            collector.Events,
            e => e.EventType == BattleEventType.TokenApplied &&
                 e.TargetId == enemy.Identity.Id &&
                 e.TokenType == TokenType.Stun.ToString());
        Assert.DoesNotContain(
            collector.Events,
            e => e.EventType == BattleEventType.ActionUsed && e.ActorId == enemy.Identity.Id);
    }

    [Fact]
    public void DotTicksAtTurnStart_AndExpires()
    {
        var random = new SeededRandomSource(1);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var skills = SampleCombatData.CreateSkills();
        var battle = BattleFactory.CreateSampleBattle(skills, allyCount: 1, enemyCount: 1, corruptionValue: 0);

        var enemy = battle.Enemies[0];
        enemy.Dots.ActiveDots.Add(new DotInstance
        {
            Type = DotType.Blight,
            Potency = 2,
            RemainingTurns = 1,
            AppliedById = battle.Allies[0].Identity.Id,
        });

        var hpBefore = enemy.Health.CurrentHp;
        simulator.Simulate(battle, maxTurns: 1);
        Assert.True(enemy.Health.CurrentHp <= hpBefore - 2);
        Assert.Empty(enemy.Dots.ActiveDots);
    }

    [Fact]
    public void CorpseIsCreatedOnlyForNonCritDirectKill()
    {
        var random = new SeededRandomSource(123);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var skill = new SkillDefinition
        {
            Id = "guaranteed_hit",
            Name = "Guaranteed Hit",
            Element = ElementType.Fire,
            Type = "Active",
            AllowedCasterRanks = [1, 2, 3, 4],
            AllowedTargetRanks = [1, 2, 3, 4],
            BaseDamage = new DamageRange { Min = 4, Max = 4 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            Cooldown = 0,
            EffectsOnHit = [],
        };
        var battle = BattleFactory.CreateSampleBattle([skill], allyCount: 1, enemyCount: 1, corruptionValue: 0);
        battle.Allies[0].Stats = new StatsComponent { Speed = 100, Accuracy = 1.0, CritChance = 0.0 };
        battle.Allies[0].SkillLoadout.Skills.Clear();
        battle.Allies[0].SkillLoadout.Skills.Add(skill.Id);
        battle.Enemies[0].Health.CurrentHp = 3;

        simulator.Simulate(battle, maxTurns: 2);
        Assert.Contains(battle.Enemies, e => e.Identity.Faction == Faction.Corpse);
    }

    [Fact]
    public void PushAndPull_RespectRankBoundaries()
    {
        var random = new SeededRandomSource(7);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var shove = new SkillDefinition
        {
            Id = "shove",
            Name = "Shove",
            Element = ElementType.Metal,
            Type = "Active",
            AllowedCasterRanks = [1, 2, 3, 4],
            AllowedTargetRanks = [1, 2, 3, 4],
            BaseDamage = new DamageRange { Min = 0, Max = 0 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            Cooldown = 0,
            EffectsOnHit = [new EffectSpec { Type = EffectType.Push, Steps = 10 }],
        };

        var battle = BattleFactory.CreateSampleBattle([shove], allyCount: 1, enemyCount: 1, corruptionValue: 0);
        battle.Allies[0].SkillLoadout.Skills.Clear();
        battle.Allies[0].SkillLoadout.Skills.Add(shove.Id);
        battle.Allies[0].Stats = new StatsComponent { Speed = 100, Accuracy = 1.0, CritChance = 0.0 };

        simulator.Simulate(battle, maxTurns: 1);
        Assert.InRange(battle.Enemies[0].Position.FrontRank, 1, 4);
    }

    [Fact]
    public void SkillTreeBlocksNextTierWithoutPreviousTierUnlocked()
    {
        var tree = new CharacterSkillTreesDefinition
        {
            CharacterId = "wulfric",
            Trees =
            [
                new SkillTreeDefinition
                {
                    Element = ElementType.Fire,
                    Tiers =
                    [
                        new SkillTreeTierDefinition
                        {
                            Tier = 1,
                            Nodes =
                            [
                                new SkillTreeNodeDefinition { Id = "f_t1_p1", Type = "Passive", Cost = 1, Requires = [] },
                                new SkillTreeNodeDefinition { Id = "f_t1_p2", Type = "Passive", Cost = 1, Requires = [] },
                                new SkillTreeNodeDefinition { Id = "f_t1_p3", Type = "Passive", Cost = 1, Requires = [] },
                                new SkillTreeNodeDefinition { Id = "f_t1_a1", Type = "Active", Cost = 1, Requires = ["f_t1_p1", "f_t1_p2", "f_t1_p3"] },
                            ],
                        },
                        new SkillTreeTierDefinition
                        {
                            Tier = 2,
                            Nodes =
                            [
                                new SkillTreeNodeDefinition { Id = "f_t2_p1", Type = "Passive", Cost = 1, Requires = [] },
                                new SkillTreeNodeDefinition { Id = "f_t2_p2", Type = "Passive", Cost = 1, Requires = [] },
                                new SkillTreeNodeDefinition { Id = "f_t2_p3", Type = "Passive", Cost = 1, Requires = [] },
                                new SkillTreeNodeDefinition { Id = "f_t2_a1", Type = "Active", Cost = 1, Requires = ["f_t2_p1", "f_t2_p2", "f_t2_p3"] },
                            ],
                        },
                    ],
                },
            ],
        };

        var unlockedNodes = new Dictionary<string, bool>
        {
            ["f_t1_p1"] = true,
            ["f_t1_p2"] = false,
            ["f_t1_p3"] = true,
            ["f_t1_a1"] = false,
        };

        var canUnlock = SkillTreeRules.CanUnlockNode(tree, "Fire", "f_t2_p1", unlockedNodes);
        Assert.False(canUnlock);
    }

    [Fact]
    public void InvariantsHoldAcrossMultipleSeeds()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var random = new SeededRandomSource(seed);
            var collector = new CombatEventCollector();
            var simulator = new BattleSimulator(random, collector);
            var battle = BattleFactory.CreateSampleBattle(SampleCombatData.CreateSkills(), corruptionValue: seed * 2);
            simulator.Simulate(battle, maxTurns: 50);

            Assert.All(
                battle.GetAllCombatants(),
                combatant =>
                {
                    Assert.True(combatant.Health.CurrentHp <= combatant.Health.MaxHp);
                    Assert.True(combatant.Tokens.Entries.All(tokenEntry => tokenEntry.Stacks >= 0));
                    Assert.All(combatant.Position.OccupiedRanks, rank => Assert.InRange(rank, 1, 4));
                });
            Assert.InRange(battle.CorruptionValue, 0, 100);
        }
    }

    [Fact]
    public void DeterministicSeed_ProducesStableEventStream()
    {
        var runA = RunBattleAndSignatures(321);
        var runB = RunBattleAndSignatures(321);
        Assert.Equal(runA, runB);
    }

    private static List<string> RunBattleAndSignatures(int seed)
    {
        var random = new SeededRandomSource(seed);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var battle = BattleFactory.CreateSampleBattle(SampleCombatData.CreateSkills(), corruptionValue: 40);
        simulator.Simulate(battle, maxTurns: 30);
        return collector.Events
            .Select(combatEvent => $"{combatEvent.Turn}|{combatEvent.EventType}|{combatEvent.ActorId}|{combatEvent.TargetId}|{combatEvent.SkillId}|{combatEvent.DamageAmount}|{combatEvent.IsHit}|{combatEvent.IsCrit}|{combatEvent.CorruptionTier}")
            .ToList();
    }

    [Fact]
    public void Passive_OutgoingDamageVsSkillId_IncreasesDamageDealt()
    {
        var smack = new SkillDefinition
        {
            Id = "test_smack",
            Name = "Test Smack",
            Element = ElementType.None,
            Type = "Active",
            AllowedCasterRanks = [1, 2, 3, 4],
            AllowedTargetRanks = [1, 2, 3, 4],
            BaseDamage = new DamageRange { Min = 100, Max = 100 },
            BaseCritChance = 0,
            Accuracy = 1.0,
            Cooldown = 0,
            TargetKind = SkillTargetKind.Enemy,
        };
        var passive = new PassiveDefinition
        {
            Id = "p_damage_bonus",
            EffectKind = PassiveEffectKind.OutgoingDamageVsSkillId,
            SkillId = smack.Id,
            Additive = 0.15,
        };
        var byId = new Dictionary<string, PassiveDefinition> { [passive.Id] = passive };

        var random = new SeededRandomSource(42);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var battle = BattleFactory.CreateSampleBattle(
            [smack],
            allyCount: 1,
            enemyCount: 1,
            corruptionValue: 0,
            passivesById: byId,
            unlockAllPassiveNodesForAllies: true);
        battle.Allies[0].Stats = new StatsComponent { Speed = 100, Accuracy = 1.0, CritChance = 0.0 };
        battle.Allies[0].ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        battle.Allies[0].SkillLoadout.Skills.Clear();
        battle.Allies[0].SkillLoadout.Skills.Add(smack.Id);
        battle.Enemies[0].Stats = new StatsComponent { Speed = 1, Accuracy = 1.0, CritChance = 0.0 };
        battle.Enemies[0].ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };

        simulator.Simulate(battle, maxTurns: 2);

        var damageAmounts = collector.Events
            .Where(combatEvent => combatEvent.EventType == BattleEventType.DamageApplied && combatEvent.SkillId == smack.Id)
            .Select(combatEvent => combatEvent.DamageAmount)
            .ToList();
        Assert.NotEmpty(damageAmounts);
        Assert.All(damageAmounts, damageAmount => Assert.Equal(115, damageAmount));
    }

    [Fact]
    public void Passive_ApplyExtraDotAfterShove_WhenTargetHasBleed()
    {
        var skills = SampleCombatData.CreateSkills();
        var shove = skills.First(skill => skill.Id == "wulfric_innate_shove");
        var passive = new PassiveDefinition
        {
            Id = "f_t1_p3",
            EffectKind = PassiveEffectKind.ApplyExtraDotAfterSkillIfTargetHasDot,
            SkillId = shove.Id,
            DotType = DotType.Bleed,
            IntValue = 4,
            IntValue2 = 2,
        };
        var byId = new Dictionary<string, PassiveDefinition> { [passive.Id] = passive };
        var random = new SeededRandomSource(7);
        var collector = new CombatEventCollector();
        var simulator = new BattleSimulator(random, collector);
        var battle = BattleFactory.CreateSampleBattle(
            skills.ToList(),
            allyCount: 1,
            enemyCount: 1,
            corruptionValue: 0,
            allySkillIds: [shove.Id],
            passivesById: byId,
            unlockAllPassiveNodesForAllies: true);

        var ally = battle.Allies[0];
        var enemy = battle.Enemies[0];
        ally.Stats = new StatsComponent { Speed = 100, Accuracy = 1.0, CritChance = 0.0 };
        enemy.Stats = new StatsComponent { Speed = 1, Accuracy = 1.0, CritChance = 0.0 };
        enemy.Dots.ActiveDots.Add(new DotInstance
        {
            Type = DotType.Bleed,
            Potency = 1,
            RemainingTurns = 3,
            AppliedById = ally.Identity.Id,
        });

        simulator.Simulate(battle, maxTurns: 1);

        var bleedDots = enemy.Dots.ActiveDots.Count(dotInstance => dotInstance.Type == DotType.Bleed);
        Assert.True(bleedDots >= 2);
    }

    [Fact]
    public void LoadPassivesJson_ParsesDefaultFile()
    {
        var list = SampleCombatData.CreatePassives();
        Assert.NotEmpty(list);
        Assert.Contains(list, passive => passive.Id == "f_t1_p1" && passive.EffectKind == PassiveEffectKind.OutgoingDamageVsSkillId);
    }

    [Fact]
    public void BuildPassiveAggregates_MatchesBattlesAndWins()
    {
        var sharedTimestampUtc = DateTime.UtcNow;
        var events = new List<CombatEvent>
        {
            new()
            {
                EventId = "e1",
                BattleId = "b1",
                Turn = 0,
                TimestampUtc = sharedTimestampUtc,
                EventType = BattleEventType.BattleStarted,
                PassiveLoadoutCsv = "f_t1_p1,f_t1_p2",
            },
            new()
            {
                EventId = "e2",
                BattleId = "b1",
                Turn = 1,
                TimestampUtc = sharedTimestampUtc,
                EventType = BattleEventType.BattleEnded,
                BattleResult = Side.Allies.ToString(),
            },
            new()
            {
                EventId = "e3",
                BattleId = "b2",
                Turn = 0,
                TimestampUtc = sharedTimestampUtc,
                EventType = BattleEventType.BattleStarted,
                PassiveLoadoutCsv = "f_t1_p1",
            },
            new()
            {
                EventId = "e4",
                BattleId = "b2",
                Turn = 1,
                TimestampUtc = sharedTimestampUtc,
                EventType = BattleEventType.BattleEnded,
                BattleResult = Side.Enemies.ToString(),
            },
        };

        var rows = CombatAnalyticsExporter.BuildPassiveAggregates(events, allPassiveIdsInCatalog: null)
            .ToDictionary(row => row.PassiveId);
        Assert.Equal(2, rows["f_t1_p1"].BattlesWithPassive);
        Assert.Equal(1, rows["f_t1_p1"].Wins);
        Assert.Equal(0.5, rows["f_t1_p1"].WinRate);
        Assert.Equal(1, rows["f_t1_p2"].BattlesWithPassive);
        Assert.Equal(1, rows["f_t1_p2"].Wins);
    }

    [Fact]
    public void BuildPassiveAggregates_IncludesCatalogIdsWithZeroBattles()
    {
        var sharedTimestampUtc = DateTime.UtcNow;
        var events = new List<CombatEvent>
        {
            new()
            {
                EventId = "e1",
                BattleId = "b1",
                Turn = 0,
                TimestampUtc = sharedTimestampUtc,
                EventType = BattleEventType.BattleStarted,
                PassiveLoadoutCsv = "f_t1_p1",
            },
            new()
            {
                EventId = "e2",
                BattleId = "b1",
                Turn = 1,
                TimestampUtc = sharedTimestampUtc,
                EventType = BattleEventType.BattleEnded,
                BattleResult = Side.Allies.ToString(),
            },
        };

        var rows = CombatAnalyticsExporter.BuildPassiveAggregates(events, ["f_t1_p1", "f_t3_p1"]).ToDictionary(row => row.PassiveId);
        Assert.Equal(0, rows["f_t3_p1"].BattlesWithPassive);
        Assert.Equal(0, rows["f_t3_p1"].WinRate);
    }

    [Fact]
    public void SimulationSkillTreeSetup_Tree1Tier3_IncludesFireActives()
    {
        var trees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        var wulfric = SimulationSkillTreeSetup.GetCharacter(trees);
        var ids = SimulationSkillTreeSetup.GetNodeIdsForTreeMaxTier(wulfric, treeIndex1Based: 1, maxTierInclusive: 3);
        Assert.Contains("f_t3_a1", ids);
        Assert.Contains("f_t1_p1", ids);
        Assert.DoesNotContain("m_t1_p1", ids);
    }
}