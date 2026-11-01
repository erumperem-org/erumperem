using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Tests;

/// <summary>Base-kit passive-engine capabilities (owner token potency, basic-attack follow-up, apply/heal triggers) over the shipped catalog.</summary>
public sealed class MatsudaBaseKitPassiveEngineTests
{
    private const string SoundStrike = "matsuda_innate_active1";
    private const string HealingVoice = "matsuda_innate_active2";
    private const string AmplifiedScream = "matsuda_innate_active3";
    private const string InspirationalSong = "matsuda_innate_active4";
    private static readonly string[] CorruptionPassives =
    [
        "matsuda_corruption_tier1_passive1",
        "matsuda_corruption_tier2_passive1",
        "matsuda_corruption_tier3_passive1",
    ];

    [Fact]
    public void InnateKit_MatchesDesign()
    {
        var skills = SampleCombatData.CreateSkills().ToDictionary(skill => skill.Id, StringComparer.Ordinal);
        AssertSkill(skills[SoundStrike], 0, ElementType.Anomaly, SkillTargetKind.OneEnemy, 4, 8, 0.01, 0.9);
        AssertSkill(skills[HealingVoice], 4, ElementType.Anomaly, SkillTargetKind.SelfOrAlly, 0, 0, 0, 0.8);
        AssertSkill(skills[AmplifiedScream], 3, ElementType.Anomaly, SkillTargetKind.AllEnemies, 3, 6, 0, 0.7);
        AssertSkill(skills[InspirationalSong], 4, ElementType.Anomaly, SkillTargetKind.SelfAndAlly, 0, 0, 0, 0.8);
        Assert.Contains(skills[HealingVoice].EffectsOnHit, e => e.Type == EffectType.HealHp && e.Potency == 5 && e.AmountMax == 10);
        Assert.Contains(skills[AmplifiedScream].EffectsOnHit, e => e.Token == TokenType.Vulnerability && e.Stacks == 2);
        Assert.Contains(skills[InspirationalSong].EffectsOnHit, e => e.Token == TokenType.Strength && e.Stacks == 3);
    }

    [Theory]
    [InlineData(1, 0.25)]
    [InlineData(2, 0.50)]
    [InlineData(3, 1.00)]
    public void CorruptionTier_PotencyOnlyAffectsTokensMatsudaApplied_AndDoesNotStackAcrossTiers(int tier, double bonus)
    {
        var battle = CreateBattle(tier);
        var matsuda = battle.Allies[0];
        var enemy = battle.Enemies[0];

        Resolve(battle, AmplifiedScream, matsuda, enemy);
        Assert.Equal(1.0 + bonus, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, enemy, TokenType.Vulnerability, matsuda), 5);
        Assert.Equal(1.0 + bonus, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, enemy, TokenType.Vulnerability), 5);

        // Same token applied by someone else is not boosted.
        var foreign = battle.Enemies[1];
        foreign.Tokens.Add(TokenType.Vulnerability, 2, "not_matsuda");
        Assert.Equal(1.0, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, foreign, TokenType.Vulnerability), 5);

        // Tokens placed on Matsuda by an enemy are not boosted either.
        matsuda.Tokens.Add(TokenType.Weaken, 2, enemy.Identity.Id);
        Assert.Equal(1.0, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, matsuda, TokenType.Weaken), 5);
    }

    [Fact]
    public void CorruptionTier_BuffsMatsudaGivesAreBoosted()
    {
        var battle = CreateBattle(2);
        var matsuda = battle.Allies[0];
        var ally = battle.Allies[1];
        matsuda.Tokens.Add(TokenType.BonusAction, 1);
        Resolve(battle, InspirationalSong, matsuda, matsuda);
        Assert.Equal(1.5, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, ally, TokenType.Strength), 5);
        Assert.Equal(1.5, PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, matsuda, TokenType.Strength), 5);
    }

    [Fact]
    public void CorruptionTier_BasicAttackFollowUp_FiresOnEnemyTokenTarget_AtStatedChance()
    {
        var battle = CreateBattle(3);
        battle.PassiveTriggerRandom = new FixedRandom(0.10); // < 0.15
        var matsuda = battle.Allies[0];
        var enemy = battle.Enemies[0];
        var events = new CombatEventCollector();
        Resolve(battle, AmplifiedScream, matsuda, enemy, events);
        Assert.Contains(events.Events, e => e.EventType == BattleEventType.ActionUsed && e.SkillId == SoundStrike && e.TargetId == enemy.Identity.Id);
    }

    [Fact]
    public void CorruptionTier_BasicAttackFollowUp_DoesNotFireAboveChance_OrOnAllies()
    {
        var battle = CreateBattle(3);
        battle.PassiveTriggerRandom = new FixedRandom(0.20); // >= 0.15
        var matsuda = battle.Allies[0];
        var events = new CombatEventCollector();
        Resolve(battle, AmplifiedScream, matsuda, battle.Enemies[0], events);
        Assert.DoesNotContain(events.Events, e => e.SkillId == SoundStrike);

        battle = CreateBattle(3);
        battle.PassiveTriggerRandom = new FixedRandom(0.0);
        matsuda = battle.Allies[0];
        events = new CombatEventCollector();
        matsuda.Tokens.Add(TokenType.BonusAction, 1);
        var hpBefore = battle.Allies[1].Health.CurrentHp;
        Resolve(battle, InspirationalSong, matsuda, matsuda, events);
        Assert.DoesNotContain(events.Events, e => e.SkillId == SoundStrike);
        Assert.Equal(hpBefore, battle.Allies[1].Health.CurrentHp);
    }

    [Fact]
    public void CorruptionTier_FollowUpChanceDoesNotStackAcrossTiers()
    {
        var battle = CreateBattle(3);
        battle.PassiveTriggerRandom = new FixedRandom(0.0);
        var events = new CombatEventCollector();
        Resolve(battle, AmplifiedScream, battle.Allies[0], battle.Enemies[0], events);
        var casts = events.Events.Count(e => e.EventType == BattleEventType.ActionUsed && e.SkillId == SoundStrike);
        Assert.Equal(1, casts);
    }

    [Fact]
    public void LeaderAndCompanion_ExtraStackOnlyOnMatchingKindAndRole()
    {
        var battle = CreateBattle(0);
        var leader = battle.Allies[0];
        var enemy = battle.Enemies[0];

        Resolve(battle, AmplifiedScream, leader, enemy);
        Assert.Equal(3, enemy.Tokens.GetStacks(TokenType.Vulnerability));

        leader.Tokens.Add(TokenType.BonusAction, 1);
        Resolve(battle, InspirationalSong, leader, leader);
        Assert.Equal(3, leader.Tokens.GetStacks(TokenType.Strength));

        var battle2 = CreateBattle(0);
        var companion = battle2.Allies[1];
        Resolve(battle2, AmplifiedScream, companion, battle2.Enemies[0]);
        Assert.Equal(2, battle2.Enemies[0].Tokens.GetStacks(TokenType.Vulnerability));
        companion.Tokens.Add(TokenType.BonusAction, 1);
        Resolve(battle2, InspirationalSong, companion, companion);
        Assert.Equal(4, companion.Tokens.GetStacks(TokenType.Strength));
    }

    [Fact]
    public void HealTrigger_FiresOnHealingVoice()
    {
        var battle = CreateBattle(0);
        var matsuda = battle.Allies[0];
        var healed = 0;
        battle.PassiveBus.Subscribe((trigger, _, _) =>
        {
            if (trigger == PassiveTrigger.HealingDealt)
            {
                healed++;
            }
        });
        matsuda.Health.CurrentHp = 10;
        Resolve(battle, HealingVoice, matsuda, matsuda);
        Assert.Equal(1, healed);
    }

    private static void AssertSkill(SkillDefinition skill, int cost, ElementType element, SkillTargetKind kind, int min, int max, double crit, double acc)
    {
        Assert.Equal(cost, skill.CorruptionCost);
        Assert.Equal(element, skill.Element);
        Assert.Equal(kind, skill.TargetKind);
        Assert.Equal(min, skill.BaseDamage.Min);
        Assert.Equal(max, skill.BaseDamage.Max);
        Assert.Equal(crit, skill.BaseCritChance, 5);
        Assert.Equal(acc, skill.Accuracy, 5);
    }

    private static BattleState CreateBattle(int corruptionTier)
    {
        var skills = SampleCombatData.CreateSkills();
        var passives = SampleCombatData.CreatePassives().ToDictionary(p => p.Id, StringComparer.Ordinal);
        var corruption = corruptionTier switch
        {
            0 => 0.0,
            1 => CorruptionRules.Tier0UpperInclusive + 1,
            2 => CorruptionRules.Tier1UpperInclusive + 1,
            _ => CorruptionRules.Tier2UpperInclusive + 1,
        };
        var battle = BattleFactory.CreateSampleBattle(
            skills, 2, 2, corruption,
            [SoundStrike, HealingVoice, AmplifiedScream, InspirationalSong], null, passives);
        Assert.Equal(corruptionTier, battle.CorruptionTier);
        foreach (var ally in battle.Allies)
        {
            ally.Progression.UnlockedNodes["matsuda_leader_passive1"] = true;
            ally.Progression.UnlockedNodes["matsuda_companion_passive1"] = true;
            foreach (var id in CorruptionPassives)
            {
                ally.Progression.UnlockedNodes[id] = true;
            }
        }

        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
            combatant.Stats = new StatsComponent { Speed = combatant.Identity.Faction == Faction.Player ? 6 : 4, Accuracy = 1.0, CritChance = 0 };
        }

        battle.PassiveTriggerRandom = new FixedRandom(0.99);
        return battle;
    }

    private static void Resolve(BattleState battle, string skillId, Combatant actor, Combatant target, CombatEventCollector? events = null)
    {
        var simulator = new BattleSimulator(new HitNoCritRandom(), events ?? new CombatEventCollector());
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
