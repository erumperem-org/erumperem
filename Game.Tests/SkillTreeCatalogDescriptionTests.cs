using Game.Core.Data;
using Game.Core.Models;
using Game.Core.Presentation;

namespace Game.Tests;

public sealed class SkillTreeCatalogDescriptionTests
{
    [Fact]
    public void FocusedInstability_DescribesControlledInstabilityStacks()
    {
        var focusedInstability = LoadSkill("wulfric_tree1_tier1_active");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(focusedInstability);

        Assert.Contains("Focused Instability", summary, StringComparison.Ordinal);
        Assert.Contains("Controlled Instability", summary, StringComparison.Ordinal);
        Assert.Contains("+6", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReStabilization_DescribesConsumeHeal()
    {
        var reStabilization = LoadSkill("wulfric_tree1_tier2_active");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(reStabilization);

        Assert.Contains("consumes all Controlled Instability", summary, StringComparison.Ordinal);
        Assert.Contains("heals", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TreePassive_BasicVsDestabilization_DescribesCatalogEffect()
    {
        var skillsById = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
            .ToDictionary(skill => skill.Id, StringComparer.Ordinal);
        var passive = LoadPassive("wulfric_tree1_tier1_passive1");

        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passive, skillsById);

        Assert.Contains("Destabilization", summary, StringComparison.Ordinal);
        Assert.Contains("Controlled Instability", summary, StringComparison.Ordinal);
        Assert.Contains("Sword cleave", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TreePassive_DefenseTokenEffectiveness_UsesPercent()
    {
        var passive = LoadPassive("wulfric_tree1_tier1_passive2");

        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passive);

        Assert.Contains("Defense tokens are +25% more effective", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_DefenseSkipEndOfTurn_DescribesOwnDefenseStacks()
    {
        var passive = LoadPassive("wulfric_tree3_tier3_passive1");

        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passive);

        Assert.Contains("Defense", summary, StringComparison.Ordinal);
        Assert.Contains("end of your turn", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("200%", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_ControlledInstabilityWhenHitByDestabilization_DescribesAttackerStatus()
    {
        var passive = LoadPassive("wulfric_tree2_tier1_passive1");

        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passive);

        Assert.Contains("Destabilization", summary, StringComparison.Ordinal);
        Assert.Contains("hits you", summary, StringComparison.Ordinal);
        Assert.Contains("Controlled Instability", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_ShieldChargePerOwnControlledInstability_DescribesFlatSkillDamage()
    {
        var skillsById = LoadSkillsById();
        var passive = LoadPassive("wulfric_tree3_tier3_passive3");

        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passive, skillsById);

        Assert.Contains("Shield Charge", summary, StringComparison.Ordinal);
        Assert.Contains("+1 damage", summary, StringComparison.Ordinal);
        Assert.Contains("Controlled Instability", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("100%", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_RevolverFlatDamage_IsNotPercent()
    {
        var skillsById = LoadSkillsById();
        var plusTwo = PassivePlayerDescriptionBuilder.BuildSummaryLine(LoadPassive("buck_tree1_tier1_passive2"), skillsById);
        var plusThree = PassivePlayerDescriptionBuilder.BuildSummaryLine(LoadPassive("buck_tree1_tier2_passive3"), skillsById);

        Assert.Contains("revolver shot", plusTwo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("+2 damage", plusTwo, StringComparison.Ordinal);
        Assert.Contains("+3 damage", plusThree, StringComparison.Ordinal);
        Assert.DoesNotContain("200%", plusTwo, StringComparison.Ordinal);
        Assert.DoesNotContain("300%", plusThree, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_StrengthOnVulnerability_RequiresDamagingMarkedStatus()
    {
        var passive = LoadPassive("buck_tree1_tier2_passive1");

        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passive);

        Assert.Contains("deal damage", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vulnerability", summary, StringComparison.Ordinal);
        Assert.Contains("Strength", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_RevolverAndRifleVsMark_MentionTheWeapon()
    {
        var skillsById = LoadSkillsById();
        var revolver = PassivePlayerDescriptionBuilder.BuildSummaryLine(LoadPassive("buck_tree3_tier2_passive1"), skillsById);
        var rifle = PassivePlayerDescriptionBuilder.BuildSummaryLine(LoadPassive("buck_tree3_tier2_passive2"), skillsById);

        Assert.Contains("revolver shot", revolver, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unload ammo", revolver, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mark", revolver, StringComparison.Ordinal);
        Assert.DoesNotContain("While the target has Mark, revolver shot and While the target has Mark", revolver, StringComparison.Ordinal);
        Assert.Contains("Hunting Rifle", rifle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mark", rifle, StringComparison.Ordinal);
    }

    [Fact]
    public void TreePassive_PistolDamagePerDefeat_MentionsPistolSkills()
    {
        var skillsById = LoadSkillsById();
        var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(LoadPassive("buck_tree3_tier3_passive2"), skillsById);

        Assert.Contains("Pistol Draw", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Aim for the head", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("defeated", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShapeShiftingSwordsmanship_DescribesIndependentHits()
    {
        var skill = LoadSkill("wulfric_tree3_tier3_active");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(skill);

        Assert.Contains("5 hits", summary, StringComparison.Ordinal);
        Assert.Contains("per hit", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void GunsForAll_DescribesWeaponFollowUps()
    {
        var skillsById = LoadSkillsById();
        var skill = skillsById["buck_tree1_tier2_active"];

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(
            skill,
            new SkillPlayerDescriptionBuilder.SkillDescriptionContext { SkillsById = skillsById });

        Assert.Contains("Pistol Draw", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unload ammo", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hunting Rifle", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Juggling_DescribesExtraTurnForSelfAndAlly()
    {
        var skill = LoadSkill("buck_tree1_tier3_active");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(skill);

        Assert.Contains("extra turn for self and ally", summary, StringComparison.Ordinal);
        Assert.Contains("20%", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Strangle_DescribesStatusScaling()
    {
        var skill = LoadSkill("buck_tree2_tier3_active");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(skill);

        Assert.Contains("per distinct status on the enemy", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void AimForTheHead_DescribesMultiHitAndExtraTurn()
    {
        var skill = LoadSkill("buck_tree3_tier2_active");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(skill);

        Assert.Contains("3 hits", summary, StringComparison.Ordinal);
        Assert.Contains("chance of extra turn", summary, StringComparison.Ordinal);
        Assert.Contains("per hit", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void PistolDraw_DescribesExtraTurnChance()
    {
        var skill = LoadSkill("buck_innate_active2");

        var summary = SkillPlayerDescriptionBuilder.BuildSummaryLine(skill);

        Assert.Contains("75% chance of extra turn", summary, StringComparison.Ordinal);
    }

    private static SkillDefinition LoadSkill(string skillId)
    {
        var skill = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
            .Single(definition => definition.Id == skillId);
        return skill;
    }

    private static IReadOnlyDictionary<string, SkillDefinition> LoadSkillsById() =>
        CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
            .ToDictionary(skill => skill.Id, StringComparer.Ordinal);

    private static PassiveDefinition LoadPassive(string passiveId)
    {
        return CombatDataLoader.LoadPassives(CombatDataLoader.ResolveDefaultPassivesPath())
            .Single(definition => definition.Id == passiveId);
    }
}
