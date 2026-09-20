using System.Text.Json;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Tests;

public sealed class PhaseBCombatRulesTests
{
    private const double WulfricBaseDefenseChance = 0.25;
    private const int IncomingDamageAfterOtherModifiers = 100;
    private const int ExpectedDamageAfterWulfricBaseDefense = 75;

    [Fact]
    public void WulfricBaseDefenseChance_ReducesOneHundredIncomingToSeventyFive()
    {
        var smack = CreateFixedDamageSkill("phase_b_defense_smack", IncomingDamageAfterOtherModifiers);
        var battle = CreateNeutralBattle(smack);
        var attacker = battle.Allies[0];
        var defender = battle.Enemies[0];
        defender.Stats = new StatsComponent
        {
            Speed = 4,
            Accuracy = 1.0,
            CritChance = 0,
            DefenseChance = WulfricBaseDefenseChance,
        };

        var damageOnHit = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            attacker,
            defender,
            smack,
            IncomingDamageAfterOtherModifiers,
            isCriticalStrike: false,
            consumeMitigationTokens: false);

        Assert.Equal(ExpectedDamageAfterWulfricBaseDefense, damageOnHit);
    }

    [Fact]
    public void ConnectedHit_FloorsDamageToOne_WhenMitigationWouldRoundToZero()
    {
        var smack = CreateFixedDamageSkill("phase_b_min_damage_smack", 1);
        var battle = CreateNeutralBattle(smack);
        var attacker = battle.Allies[0];
        var defender = battle.Enemies[0];
        defender.Stats = new StatsComponent
        {
            Speed = 4,
            Accuracy = 1.0,
            CritChance = 0,
            DefenseChance = 0.99,
        };

        var damageOnHit = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            attacker,
            defender,
            smack,
            baseRollDamage: 1,
            isCriticalStrike: false,
            consumeMitigationTokens: false);

        Assert.Equal(1, damageOnHit);
    }

    [Fact]
    public void Miss_StaysZeroDamage()
    {
        Assert.Equal(0, CombatDamageCalculator.FloorConnectedHitDamage(0, unroundedDamage: 0));
    }

    [Fact]
    public void HitChance_ClampsToFivePercentFloorAfterTokens()
    {
        var probe = CreateFixedDamageSkill("phase_b_accuracy_probe", 5, accuracy: 0.01);
        var battle = CreateNeutralBattle(probe);
        var attacker = battle.Allies[0];
        attacker.Stats = new StatsComponent
        {
            Speed = 6,
            Accuracy = 1.0,
            CritChance = 0,
        };
        var defender = battle.Enemies[0];

        var hitChance = CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            battle,
            attacker,
            defender,
            probe);

        Assert.Equal(CombatStatusRules.MinimumHitChanceFraction, hitChance);
    }

    [Fact]
    public void HitChance_KeepsZeroWhenSkillAccuracyIsZero()
    {
        var wrapperSkill = CreateFixedDamageSkill("phase_b_accuracy_wrapper", 0, accuracy: 0);
        var battle = CreateNeutralBattle(wrapperSkill);

        var hitChance = CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            wrapperSkill);

        Assert.Equal(0, hitChance);
    }

    [Fact]
    public void HitChance_ClampsToOneAfterTokensWhenSkillAccuracyExceedsOneHundredPercent()
    {
        var probe = CreateFixedDamageSkill("phase_b_accuracy_overcap", 5, accuracy: 2.0);
        var battle = CreateNeutralBattle(probe);

        var hitChance = CombatDamageCalculator.ComputeEffectiveHitChanceFraction(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            probe);

        Assert.Equal(CombatStatusRules.MaximumHitChanceFraction, hitChance);
    }

    [Fact]
    public void LeaderPassive_DoesNotApplyToCompanion()
    {
        var smack = CreateFixedDamageSkill("phase_b_role_smack", 20);
        var leaderPassive = new PassiveDefinition
        {
            Id = "phase_b_leader_only_passive",
            EffectKind = PassiveEffectKind.DamageCausedVsSkillId,
            SkillId = smack.Id,
            Additive = 1.0,
            RequiredPartyRole = PassiveRequiredPartyRole.Leader,
        };
        var passivesById = new Dictionary<string, PassiveDefinition>
        {
            [leaderPassive.Id] = leaderPassive,
        };
        var battle = BattleFactory.CreateSampleBattle(
            [smack],
            allyCount: 2,
            enemyCount: 1,
            allySkillIds: [smack.Id],
            passivesById: passivesById,
            unlockAllPassiveNodesForAllies: true);
        NeutralizeCombatantElements(battle);

        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        var enemy = battle.Enemies[0];

        Assert.Equal(CombatantPartyRole.Leader, leader.PartyRole);
        Assert.Equal(CombatantPartyRole.Companion, companion.PartyRole);
        Assert.Contains(leaderPassive, PassiveRuleApplier.EnumerateActivePassives(leader, battle));
        Assert.DoesNotContain(leaderPassive, PassiveRuleApplier.EnumerateActivePassives(companion, battle));

        var leaderDamage = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            leader,
            enemy,
            smack,
            baseRollDamage: 20,
            isCriticalStrike: false,
            consumeMitigationTokens: false);
        var companionDamage = CombatDamageCalculator.ComputeDirectDamageOnHit(
            battle,
            companion,
            enemy,
            smack,
            baseRollDamage: 20,
            isCriticalStrike: false,
            consumeMitigationTokens: false);

        Assert.Equal(40, leaderDamage);
        Assert.Equal(20, companionDamage);
    }

    [Fact]
    public void BattleFactory_AssignsLeaderAndCompanionFromPartyIndex()
    {
        var battle = BattleFactory.CreateSampleBattle(SampleCombatData.CreateSkills(), allyCount: 2, enemyCount: 1);

        Assert.Equal(CombatantPartyRole.Leader, battle.Allies[0].PartyRole);
        Assert.Equal(CombatantPartyRole.Companion, battle.Allies[1].PartyRole);
        Assert.Equal(CombatantPartyRole.None, battle.Enemies[0].PartyRole);
    }

    [Fact]
    public void CombatPartyRoleRules_BuckMainWulfricCompanion_PreservesOverworldOrder()
    {
        var partyCharacterNames = CombatPartyRoleRules.NormalizeOverworldCombatParty(["Buck", "Wulfric"]);

        Assert.Equal(["Buck", "Wulfric"], partyCharacterNames);
        Assert.Equal(CombatantPartyRole.Leader, CombatPartyRoleRules.FromAllyPartyIndex(0));
        Assert.Equal(CombatantPartyRole.Companion, CombatPartyRoleRules.FromAllyPartyIndex(1));
    }

    [Fact]
    public void CombatPartyRoleRules_DoesNotForceWulfricToLeaderWhenListedSecond()
    {
        var wulfricForcedAsLeader = CombatPartyRoleRules.NormalizeOverworldCombatParty(["Wulfric", "Buck"]);
        var matsudaSkipped = CombatPartyRoleRules.NormalizeOverworldCombatParty(["Matsuda", "Buck", "Wulfric"]);

        Assert.Equal(["Wulfric", "Buck"], wulfricForcedAsLeader);
        Assert.Equal(["Buck", "Wulfric"], matsudaSkipped);
    }

    [Fact]
    public void PassiveEffectKindJsonConverter_ReadsLegacyOutgoingDamageStrings()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"erumperem-phase-b-passives-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var passivesPath = Path.Combine(tempDirectory, "passives.json");
        try
        {
            File.WriteAllText(
                passivesPath,
                """
                [
                  { "id": "legacy_outgoing_vs_skill", "effectKind": "OutgoingDamageVsSkillId", "additive": 0.1 },
                  { "id": "legacy_outgoing_vs_dot", "effectKind": "OutgoingDamageVsDotOnTarget" },
                  { "id": "legacy_outgoing_penalty", "effectKind": "OutgoingDamagePenaltyWhenToken" },
                  { "id": "legacy_outgoing_after_prereq", "effectKind": "OutgoingDamageAfterPrerequisiteSkill" },
                  { "id": "legacy_outgoing_vs_skill_if_dot", "effectKind": "OutgoingDamageVsSkillIfTargetHasDot" }
                ]
                """);

            var loadedPassives = CombatDataLoader.LoadPassives(passivesPath)
                .ToDictionary(passive => passive.Id, StringComparer.Ordinal);

            Assert.Equal(PassiveEffectKind.DamageCausedVsSkillId, loadedPassives["legacy_outgoing_vs_skill"].EffectKind);
            Assert.Equal(PassiveEffectKind.DamageCausedVsDotOnTarget, loadedPassives["legacy_outgoing_vs_dot"].EffectKind);
            Assert.Equal(PassiveEffectKind.DamageCausedPenaltyWhenToken, loadedPassives["legacy_outgoing_penalty"].EffectKind);
            Assert.Equal(PassiveEffectKind.DamageCausedAfterPrerequisiteSkill, loadedPassives["legacy_outgoing_after_prereq"].EffectKind);
            Assert.Equal(PassiveEffectKind.DamageCausedVsSkillIfTargetHasDot, loadedPassives["legacy_outgoing_vs_skill_if_dot"].EffectKind);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void PassiveEffectKindJsonConverter_WritesDamageCausedNames()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new PassiveEffectKindJsonConverter() },
        };

        var written = JsonSerializer.Serialize(PassiveEffectKind.DamageCausedVsSkillId, options);

        Assert.Equal("\"DamageCausedVsSkillId\"", written);
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

    private static BattleState CreateNeutralBattle(SkillDefinition skill)
    {
        var battle = BattleFactory.CreateSampleBattle(
            [skill],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [skill.Id]);
        NeutralizeCombatantElements(battle);
        battle.Allies[0].Stats = new StatsComponent
        {
            Speed = 6,
            Accuracy = 1.0,
            CritChance = 0,
        };
        battle.Enemies[0].Stats = new StatsComponent
        {
            Speed = 4,
            Accuracy = 1.0,
            CritChance = 0,
        };
        return battle;
    }

    private static void NeutralizeCombatantElements(BattleState battle)
    {
        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
        }
    }
}
