using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Tests;

public sealed class CombatCatalogLocationTests
{
    [Fact]
    public void ResolveDefaultCatalogDirectory_UsesStreamingAssetsData()
    {
        var catalogDirectory = CombatDataLoader.ResolveDefaultCatalogDirectory();
        Assert.Contains("StreamingAssets", catalogDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(catalogDirectory), catalogDirectory);
        Assert.True(File.Exists(Path.Combine(catalogDirectory, "skills.json")));
        Assert.True(File.Exists(Path.Combine(catalogDirectory, "passives.json")));
        Assert.True(File.Exists(Path.Combine(catalogDirectory, "skill_trees.json")));
        Assert.True(File.Exists(Path.Combine(catalogDirectory, "enemies.json")));
        Assert.True(File.Exists(Path.Combine(catalogDirectory, "items.json")));
    }

    [Fact]
    public void CatalogLoadsFromStreamingAssets_IncludesHorseBossAndHeroKits()
    {
        var skillsPath = CombatDataLoader.ResolveDefaultSkillsPath();
        var passivesPath = CombatDataLoader.ResolveDefaultPassivesPath();
        Assert.Contains("StreamingAssets", skillsPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("StreamingAssets", passivesPath, StringComparison.OrdinalIgnoreCase);

        var skills = CombatDataLoader.LoadSkills(skillsPath);
        var passives = CombatDataLoader.LoadPassives(passivesPath);
        var skillTrees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        var enemies = CombatDataLoader.LoadEnemies(CombatDataLoader.ResolveDefaultEnemiesPath());

        Assert.Contains(skills, skill => skill.Id == "wulfric_innate_active1");
        Assert.Contains(skills, skill => skill.Id == "phase_a_pipeline_test_active");
        Assert.Contains(skills, skill => skill.Id == "horse_boss_painful_bite");
        Assert.Contains(passives, passive => passive.Id == "horse_boss_summon_fairy_on_hp_tier");
        Assert.Contains(skillTrees, character => character.CharacterId == "wulfric");
        Assert.Contains(enemies, enemy => enemy.Id == "horse_boss");
    }

    [Fact]
    public void SkillDefinition_JsonRoundTrip_PreservesOptionalCombatFields()
    {
        var skill = new SkillDefinition
        {
            Id = "phase_a_pipeline_test_active",
            Name = "Phase A Pipeline Test",
            Element = ElementType.Metal,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 2, Max = 9 },
            BaseCritChance = 0.05,
            Accuracy = 2.0,
            TargetKind = SkillTargetKind.UpToThreeEnemies,
            ChanceToUse = 0.4,
            SelfHpPercentBelow = 0.5,
            CorruptionCost = 3,
            HitCount = 3,
            ChanceToNotEndTurn = 0.75,
            FollowUpSkillIds = ["buck_innate_active2", "buck_innate_active4"],
            GrantsBonusActionsToAllies = true,
            AccuracyPenaltyPerLivingEnemy = 0.1,
            BonusDamagePerOwnToken = TokenType.Defense,
            BonusDamagePerOwnTokenStacks = 2,
            ComputeFromDebuffTypesOnTarget = true,
            DamagePerDistinctDebuffType = 10,
            CritChancePerDistinctDebuffType = 0.02,
            AccuracyPerDistinctDebuffType = 0.05,
            CanTargetDeadAllies = true,
            EffectsOnHit =
            [
                new EffectSpec
                {
                    Type = EffectType.HealHp,
                    Potency = 5,
                    AmountMax = 10,
                    Chance = 1.0,
                    EffectScope = EffectScope.Self,
                    ScaleFromToken = TokenType.ControlledInstability,
                    ScaleStacksPerSourceStack = 2,
                    ScaleStacksSourceDivisor = 2,
                    Token = TokenType.Defense,
                    Stacks = 1,
                    Duration = 3,
                    Steps = 1,
                    Dot = DotType.Bleed,
                },
            ],
        };

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"erumperem-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var skillsPath = Path.Combine(tempDirectory, "skills.json");
        try
        {
            CombatCatalogWriter.WriteSkills(skillsPath, [skill]);
            var loaded = Assert.Single(CombatDataLoader.LoadSkills(skillsPath));

            Assert.Equal(skill.Id, loaded.Id);
            Assert.Equal(skill.Name, loaded.Name);
            Assert.Equal(skill.Element, loaded.Element);
            Assert.Equal(skill.Type, loaded.Type);
            Assert.Equal(skill.BaseDamage.Min, loaded.BaseDamage.Min);
            Assert.Equal(skill.BaseDamage.Max, loaded.BaseDamage.Max);
            Assert.Equal(skill.BaseCritChance, loaded.BaseCritChance);
            Assert.Equal(skill.Accuracy, loaded.Accuracy);
            Assert.Equal(skill.TargetKind, loaded.TargetKind);
            Assert.Equal(skill.ChanceToUse, loaded.ChanceToUse);
            Assert.Equal(skill.SelfHpPercentBelow, loaded.SelfHpPercentBelow);
            Assert.Equal(skill.CorruptionCost, loaded.CorruptionCost);
            Assert.Equal(skill.HitCount, loaded.HitCount);
            Assert.Equal(skill.ChanceToNotEndTurn, loaded.ChanceToNotEndTurn);
            Assert.Equal(skill.FollowUpSkillIds, loaded.FollowUpSkillIds);
            Assert.Equal(skill.GrantsBonusActionsToAllies, loaded.GrantsBonusActionsToAllies);
            Assert.Equal(skill.AccuracyPenaltyPerLivingEnemy, loaded.AccuracyPenaltyPerLivingEnemy);
            Assert.Equal(skill.BonusDamagePerOwnToken, loaded.BonusDamagePerOwnToken);
            Assert.Equal(skill.BonusDamagePerOwnTokenStacks, loaded.BonusDamagePerOwnTokenStacks);
            Assert.Equal(skill.ComputeFromDebuffTypesOnTarget, loaded.ComputeFromDebuffTypesOnTarget);
            Assert.Equal(skill.DamagePerDistinctDebuffType, loaded.DamagePerDistinctDebuffType);
            Assert.Equal(skill.CritChancePerDistinctDebuffType, loaded.CritChancePerDistinctDebuffType);
            Assert.Equal(skill.AccuracyPerDistinctDebuffType, loaded.AccuracyPerDistinctDebuffType);
            Assert.Equal(skill.CanTargetDeadAllies, loaded.CanTargetDeadAllies);

            var loadedEffect = Assert.Single(loaded.EffectsOnHit);
            Assert.Equal(EffectType.HealHp, loadedEffect.Type);
            Assert.Equal(5, loadedEffect.Potency);
            Assert.Equal(10, loadedEffect.AmountMax);
            Assert.Equal(EffectScope.Self, loadedEffect.EffectScope);
            Assert.Equal(TokenType.ControlledInstability, loadedEffect.ScaleFromToken);
            Assert.Equal(2, loadedEffect.ScaleStacksPerSourceStack);
            Assert.Equal(2, loadedEffect.ScaleStacksSourceDivisor);
            Assert.Equal(TokenType.Defense, loadedEffect.Token);
            Assert.Equal(1, loadedEffect.Stacks);
            Assert.Equal(3, loadedEffect.Duration);
            Assert.Equal(1, loadedEffect.Steps);
            Assert.Equal(DotType.Bleed, loadedEffect.Dot);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void PassiveDefinition_JsonRoundTrip_PreservesRoleConditionsAndEffects()
    {
        var passive = new PassiveDefinition
        {
            Id = "wulfric_leader_passive1",
            EffectKind = PassiveEffectKind.DamageCausedVsSkillId,
            SkillId = "wulfric_innate_active1",
            Additive = 0.1,
            RequiredPartyRole = PassiveRequiredPartyRole.Leader,
            ChanceToTrigger = 0.5,
            MaxTriggersPerBattle = 3,
            MaxTriggersPerTurn = 1,
            Conditions =
            [
                new PassiveConditionDefinition
                {
                    Activation = PassiveActivationKind.UponKill,
                    RequiredStatus = TokenType.Taunt,
                    HitPointsLostPerTrigger = 10,
                    StatThresholdFraction = 0.25,
                    SkillId = "wulfric_innate_active2",
                },
            ],
            Effects =
            [
                new PassiveEffectDefinition
                {
                    Operation = PassiveEffectOperationKind.Heal,
                    Magnitude = 15,
                    Token = TokenType.Defense,
                    SkillId = "wulfric_innate_active1",
                    SummonEnemyId = "corrupted_fairy",
                    Stacks = 2,
                },
            ],
        };

        var tempDirectory = Path.Combine(Path.GetTempPath(), $"erumperem-passives-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var passivesPath = Path.Combine(tempDirectory, "passives.json");
        try
        {
            CombatCatalogWriter.WritePassives(passivesPath, [passive]);
            var loaded = Assert.Single(CombatDataLoader.LoadPassives(passivesPath));

            Assert.Equal(passive.Id, loaded.Id);
            Assert.Equal(passive.EffectKind, loaded.EffectKind);
            Assert.Equal(passive.SkillId, loaded.SkillId);
            Assert.Equal(passive.Additive, loaded.Additive);
            Assert.Equal(PassiveRequiredPartyRole.Leader, loaded.RequiredPartyRole);
            Assert.Equal(0.5, loaded.ChanceToTrigger);
            Assert.Equal(3, loaded.MaxTriggersPerBattle);
            Assert.Equal(1, loaded.MaxTriggersPerTurn);

            var loadedCondition = Assert.Single(loaded.Conditions);
            Assert.Equal(PassiveActivationKind.UponKill, loadedCondition.Activation);
            Assert.Equal(TokenType.Taunt, loadedCondition.RequiredStatus);
            Assert.Equal(10, loadedCondition.HitPointsLostPerTrigger);
            Assert.Equal(0.25, loadedCondition.StatThresholdFraction);
            Assert.Equal("wulfric_innate_active2", loadedCondition.SkillId);

            var loadedEffect = Assert.Single(loaded.Effects);
            Assert.Equal(PassiveEffectOperationKind.Heal, loadedEffect.Operation);
            Assert.Equal(15, loadedEffect.Magnitude);
            Assert.Equal(TokenType.Defense, loadedEffect.Token);
            Assert.Equal("wulfric_innate_active1", loadedEffect.SkillId);
            Assert.Equal("corrupted_fairy", loadedEffect.SummonEnemyId);
            Assert.Equal(2, loadedEffect.Stacks);
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void UpsertSkill_ReplacesMatchingId_AndKeepsUnrelatedSkills()
    {
        var existing = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath());
        var horseBossBite = existing.First(skill => skill.Id == "horse_boss_painful_bite");
        var replacement = new SkillDefinition
        {
            Id = "wulfric_innate_active1",
            Name = "Replaced For Test",
            Element = ElementType.Fire,
            Type = "Active",
            BaseDamage = new DamageRange { Min = 1, Max = 1 },
            BaseCritChance = 0,
            Accuracy = 1,
            TargetKind = SkillTargetKind.OneEnemy,
            HitCount = 5,
            FollowUpSkillIds = ["wulfric_innate_active2"],
            CanTargetDeadAllies = true,
        };

        var merged = CombatCatalogWriter.UpsertSkill(existing, replacement);
        Assert.Equal(existing.Count, merged.Count);
        var loadedReplacement = merged.First(skill => skill.Id == "wulfric_innate_active1");
        Assert.Equal("Replaced For Test", loadedReplacement.Name);
        Assert.Equal(5, loadedReplacement.HitCount);
        Assert.Equal(["wulfric_innate_active2"], loadedReplacement.FollowUpSkillIds);
        Assert.True(loadedReplacement.CanTargetDeadAllies);
        Assert.Contains(merged, skill => skill.Id == horseBossBite.Id && skill.Name == horseBossBite.Name);
    }
}
