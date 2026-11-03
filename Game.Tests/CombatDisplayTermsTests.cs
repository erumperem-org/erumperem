using Game.Core.Domain;
using Game.Core.Presentation;

namespace Game.Tests;

public sealed class CombatDisplayTermsTests
{
    [Fact]
    public void TranslateExactName_MapsKnownSkillTitle()
    {
        Assert.Equal("Wolf Stance", CombatDisplayTerms.TranslateExactName("Postura de lobo"));
    }

    [Fact]
    public void TranslateEmbeddedTerms_ReplacesEnemyTechnicalId()
    {
        Assert.Equal("Corrupted Enemy spawned.", CombatDisplayTerms.TranslateEmbeddedTerms("enemy_01 spawned."));
    }

    [Fact]
    public void TokenDisplayName_MatchesSkillBuilderFacade()
    {
        const string expected = "Controlled Instability";
        Assert.Equal(expected, CombatDisplayTerms.FormatTokenDisplayName(TokenType.ControlledInstability));
        Assert.Equal(expected, SkillPlayerDescriptionBuilder.FormatStatusDisplayName(TokenType.ControlledInstability));
    }
}
