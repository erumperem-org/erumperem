using Game.Core.Data;
using Game.Core.Presentation;

namespace Game.Tests;

public sealed class CombatSkillDescriptionSlotResolverTests
{
    [Fact]
    public void Resolve_PrefersSelectionThenHoverThenLastThenFirst()
    {
        Assert.Equal(2, CombatSkillDescriptionSlotResolver.Resolve(4, 2, 1, 3));
        Assert.Equal(1, CombatSkillDescriptionSlotResolver.Resolve(4, null, 1, 3));
        Assert.Equal(3, CombatSkillDescriptionSlotResolver.Resolve(4, null, null, 3));
        Assert.Equal(0, CombatSkillDescriptionSlotResolver.Resolve(4, null, null, null));
    }

    [Fact]
    public void Resolve_IgnoresOutOfRangeAndEmpty()
    {
        Assert.Equal(0, CombatSkillDescriptionSlotResolver.Resolve(2, 5, -1, 9));
        Assert.Null(CombatSkillDescriptionSlotResolver.Resolve(0, 0, 0, 0));
    }

    [Fact]
    public void EveryCatalogSkill_IncludingMatsuda_HasNonEmptyDescription()
    {
        var skills = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath());
        Assert.Contains(skills, skill => skill.Id.StartsWith("matsuda_", StringComparison.Ordinal));
        foreach (var skill in skills)
        {
            var line = SkillPlayerDescriptionBuilder.BuildSummaryLine(skill);
            Assert.False(string.IsNullOrWhiteSpace(line), skill.Id);
            Assert.Contains(": ", line, StringComparison.Ordinal);
        }
    }
}
