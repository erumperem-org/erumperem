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

    private static SkillDefinition LoadSkill(string skillId)
    {
        var skill = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
            .Single(definition => definition.Id == skillId);
        return skill;
    }

    private static PassiveDefinition LoadPassive(string passiveId)
    {
        return CombatDataLoader.LoadPassives(CombatDataLoader.ResolveDefaultPassivesPath())
            .Single(definition => definition.Id == passiveId);
    }
}
