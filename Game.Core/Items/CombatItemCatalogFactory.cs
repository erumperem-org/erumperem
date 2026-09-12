using Game.Core.Domain;

namespace Game.Core.Items;

/// <summary>Canonical 37-item catalog from ReferenceForRework.md. StreamingAssets JSON is the runtime copy.</summary>
public static class CombatItemCatalogFactory
{
    public const int ExpectedItemCount = 37;

    public static IReadOnlyList<CombatItemDefinition> CreateDefaultCatalog()
    {
        var catalog = new List<CombatItemDefinition>(ExpectedItemCount);
        catalog.AddRange(CreateIndividualStatusItems());
        catalog.AddRange(CreateThematicItems());
        catalog.AddRange(CreateUtilityItems());
        return catalog;
    }

    public static IReadOnlyDictionary<string, CombatItemDefinition> CreateDefaultCatalogById() =>
        CreateDefaultCatalog().ToDictionary(item => item.Id, StringComparer.Ordinal);

    private static IEnumerable<CombatItemDefinition> CreateIndividualStatusItems()
    {
        yield return CreateIndividual(
            "health_potion_small",
            "Health Potion (Small)",
            "health_potion",
            CombatItemStatKind.Health,
            CombatItemModifierScale.Absolute,
            intensityRank: 1);
        yield return CreateIndividual(
            "health_potion_medium",
            "Health Potion (Medium)",
            "health_potion",
            CombatItemStatKind.Health,
            CombatItemModifierScale.Absolute,
            intensityRank: 2);
        yield return CreateIndividual(
            "health_potion_big",
            "Health Potion (Big)",
            "health_potion",
            CombatItemStatKind.Health,
            CombatItemModifierScale.Absolute,
            intensityRank: 3);

        yield return CreateIndividual(
            "vitality_charm_small",
            "Vitality Charm (Small)",
            "vitality_charm",
            CombatItemStatKind.Health,
            CombatItemModifierScale.Relative,
            intensityRank: 1);
        yield return CreateIndividual(
            "vitality_charm_medium",
            "Vitality Charm (Medium)",
            "vitality_charm",
            CombatItemStatKind.Health,
            CombatItemModifierScale.Relative,
            intensityRank: 2);
        yield return CreateIndividual(
            "vitality_charm_big",
            "Vitality Charm (Big)",
            "vitality_charm",
            CombatItemStatKind.Health,
            CombatItemModifierScale.Relative,
            intensityRank: 3);

        yield return CreateIndividual(
            "iron_plate_small",
            "Iron Plate (Small)",
            "iron_plate",
            CombatItemStatKind.Defense,
            CombatItemModifierScale.Absolute,
            intensityRank: 1);
        yield return CreateIndividual(
            "iron_plate_medium",
            "Iron Plate (Medium)",
            "iron_plate",
            CombatItemStatKind.Defense,
            CombatItemModifierScale.Absolute,
            intensityRank: 2);
        yield return CreateIndividual(
            "iron_plate_big",
            "Iron Plate (Big)",
            "iron_plate",
            CombatItemStatKind.Defense,
            CombatItemModifierScale.Absolute,
            intensityRank: 3);

        yield return CreateIndividual(
            "ward_sigil_small",
            "Ward Sigil (Small)",
            "ward_sigil",
            CombatItemStatKind.Defense,
            CombatItemModifierScale.Relative,
            intensityRank: 1);
        yield return CreateIndividual(
            "ward_sigil_medium",
            "Ward Sigil (Medium)",
            "ward_sigil",
            CombatItemStatKind.Defense,
            CombatItemModifierScale.Relative,
            intensityRank: 2);
        yield return CreateIndividual(
            "ward_sigil_big",
            "Ward Sigil (Big)",
            "ward_sigil",
            CombatItemStatKind.Defense,
            CombatItemModifierScale.Relative,
            intensityRank: 3);

        yield return CreateIndividual(
            "sharp_edge_small",
            "Sharp Edge (Small)",
            "sharp_edge",
            CombatItemStatKind.CriticalChance,
            CombatItemModifierScale.Absolute,
            intensityRank: 1);
        yield return CreateIndividual(
            "sharp_edge_medium",
            "Sharp Edge (Medium)",
            "sharp_edge",
            CombatItemStatKind.CriticalChance,
            CombatItemModifierScale.Absolute,
            intensityRank: 2);
        yield return CreateIndividual(
            "sharp_edge_big",
            "Sharp Edge (Big)",
            "sharp_edge",
            CombatItemStatKind.CriticalChance,
            CombatItemModifierScale.Absolute,
            intensityRank: 3);

        yield return CreateIndividual(
            "focus_lens_small",
            "Focus Lens (Small)",
            "focus_lens",
            CombatItemStatKind.CriticalChance,
            CombatItemModifierScale.Relative,
            intensityRank: 1);
        yield return CreateIndividual(
            "focus_lens_medium",
            "Focus Lens (Medium)",
            "focus_lens",
            CombatItemStatKind.CriticalChance,
            CombatItemModifierScale.Relative,
            intensityRank: 2);
        yield return CreateIndividual(
            "focus_lens_big",
            "Focus Lens (Big)",
            "focus_lens",
            CombatItemStatKind.CriticalChance,
            CombatItemModifierScale.Relative,
            intensityRank: 3);
    }

    private static IEnumerable<CombatItemDefinition> CreateThematicItems()
    {
        yield return CreateThematic(
            "paladin_small",
            "Paladino (Small)",
            "paladin",
            sizeRank: 1,
            healthRank: 1,
            defenseRank: 1,
            critRank: 1);
        yield return CreateThematic(
            "paladin_big",
            "Paladino (Big)",
            "paladin",
            sizeRank: 2,
            healthRank: 2,
            defenseRank: 2,
            critRank: 2);
        yield return CreateThematic(
            "paladin_extreme",
            "Paladino (Extreme)",
            "paladin",
            sizeRank: 3,
            healthRank: 3,
            defenseRank: 3,
            critRank: 3);

        yield return CreateThematic(
            "vampiric_small",
            "Vampírico (Small)",
            "vampiric",
            sizeRank: 1,
            healthRank: 2,
            defenseRank: -1,
            critRank: 2);
        yield return CreateThematic(
            "vampiric_big",
            "Vampírico (Big)",
            "vampiric",
            sizeRank: 2,
            healthRank: 3,
            defenseRank: -2,
            critRank: 3);
        yield return CreateThematic(
            "vampiric_extreme",
            "Vampírico (Extreme)",
            "vampiric",
            sizeRank: 3,
            healthRank: 4,
            defenseRank: -3,
            critRank: 4);

        yield return CreateThematic(
            "living_shield_small",
            "Escudo Vivo (Small)",
            "living_shield",
            sizeRank: 1,
            healthRank: 2,
            defenseRank: 2,
            critRank: -3);
        yield return CreateThematic(
            "living_shield_big",
            "Escudo Vivo (Big)",
            "living_shield",
            sizeRank: 2,
            healthRank: 3,
            defenseRank: 3,
            critRank: -4);
        yield return CreateThematic(
            "living_shield_extreme",
            "Escudo Vivo (Extreme)",
            "living_shield",
            sizeRank: 3,
            healthRank: 4,
            defenseRank: 4,
            critRank: -4);

        yield return CreateThematic(
            "cold_blood_small",
            "Sangue Frio (Small)",
            "cold_blood",
            sizeRank: 1,
            healthRank: 0,
            defenseRank: -1,
            critRank: 2);
        yield return CreateThematic(
            "cold_blood_big",
            "Sangue Frio (Big)",
            "cold_blood",
            sizeRank: 2,
            healthRank: 0,
            defenseRank: -2,
            critRank: 3);
        yield return CreateThematic(
            "cold_blood_extreme",
            "Sangue Frio (Extreme)",
            "cold_blood",
            sizeRank: 3,
            healthRank: 0,
            defenseRank: -3,
            critRank: 4);

        yield return CreateThematic(
            "berserker_small",
            "Berserker (Small)",
            "berserker",
            sizeRank: 1,
            healthRank: -2,
            defenseRank: 2,
            critRank: 3);
        yield return CreateThematic(
            "berserker_big",
            "Berserker (Big)",
            "berserker",
            sizeRank: 2,
            healthRank: -3,
            defenseRank: 3,
            critRank: 4);
        yield return CreateThematic(
            "berserker_extreme",
            "Berserker (Extreme)",
            "berserker",
            sizeRank: 3,
            healthRank: -4,
            defenseRank: 4,
            critRank: 4);
    }

    private static IEnumerable<CombatItemDefinition> CreateUtilityItems()
    {
        yield return new CombatItemDefinition
        {
            Id = "reset_skill_tree_all",
            DisplayName = "Reset Skill Tree (All)",
            Description = "Resets invested skill-tree points for Wulfric, Buck Wyatt, and Maria.",
            Rarity = CombatItemRarity.Legendary,
            Kind = CombatItemKind.Utility,
            IconFamilyId = "reset_skill_tree_all",
            UtilityKind = CombatItemUtilityKind.ResetAllSkillTrees,
        };
        yield return new CombatItemDefinition
        {
            Id = "reset_skill_tree_buck",
            DisplayName = "Reset Skill Tree — Buck Wyatt",
            Description = "Resets invested skill-tree points for Buck Wyatt.",
            Rarity = CombatItemRarity.Epic,
            Kind = CombatItemKind.Utility,
            IconFamilyId = "reset_skill_tree_buck",
            UtilityKind = CombatItemUtilityKind.ResetBuckSkillTree,
        };
        yield return new CombatItemDefinition
        {
            Id = "reset_skill_tree_wulfric",
            DisplayName = "Reset Skill Tree — Wulfric",
            Description = "Resets invested skill-tree points for Wulfric.",
            Rarity = CombatItemRarity.Epic,
            Kind = CombatItemKind.Utility,
            IconFamilyId = "reset_skill_tree_wulfric",
            UtilityKind = CombatItemUtilityKind.ResetWulfricSkillTree,
        };
        yield return new CombatItemDefinition
        {
            Id = "reset_skill_tree_maria",
            DisplayName = "Reset Skill Tree — Maria",
            Description =
                "Resets invested skill-tree points for Maria (The Star). Spec name Matsuda maps to characterId maria; there is no Matsuda combat kit.",
            Rarity = CombatItemRarity.Epic,
            Kind = CombatItemKind.Utility,
            IconFamilyId = "reset_skill_tree_maria",
            UtilityKind = CombatItemUtilityKind.ResetMariaSkillTree,
        };
    }

    private static CombatItemDefinition CreateIndividual(
        string itemId,
        string displayName,
        string iconFamilyId,
        CombatItemStatKind stat,
        CombatItemModifierScale scale,
        int intensityRank)
    {
        var rarity = CombatItemIntensityLegend.ResolveIndividualStatusRarity(intensityRank);
        var description = BuildIndividualDescription(stat, scale, intensityRank);
        return new CombatItemDefinition
        {
            Id = itemId,
            DisplayName = displayName,
            Description = description,
            Rarity = rarity,
            Kind = CombatItemKind.IndividualStatus,
            IconFamilyId = iconFamilyId,
            StatModifiers =
            [
                new CombatItemStatModifier
                {
                    Stat = stat,
                    Scale = scale,
                    IntensityRank = intensityRank,
                },
            ],
        };
    }

    private static CombatItemDefinition CreateThematic(
        string itemId,
        string displayName,
        string iconFamilyId,
        int sizeRank,
        int healthRank,
        int defenseRank,
        int critRank)
    {
        var modifiers = new List<CombatItemStatModifier>();
        if (healthRank != 0)
        {
            modifiers.Add(new CombatItemStatModifier
            {
                Stat = CombatItemStatKind.Health,
                Scale = CombatItemModifierScale.Absolute,
                IntensityRank = healthRank,
            });
        }

        if (defenseRank != 0)
        {
            modifiers.Add(new CombatItemStatModifier
            {
                Stat = CombatItemStatKind.Defense,
                Scale = CombatItemModifierScale.Relative,
                IntensityRank = defenseRank,
            });
        }

        if (critRank != 0)
        {
            modifiers.Add(new CombatItemStatModifier
            {
                Stat = CombatItemStatKind.CriticalChance,
                Scale = CombatItemModifierScale.Relative,
                IntensityRank = critRank,
            });
        }

        return new CombatItemDefinition
        {
            Id = itemId,
            DisplayName = displayName,
            Description = BuildThematicDescription(displayName, modifiers),
            Rarity = CombatItemIntensityLegend.ResolveThematicRarity(sizeRank),
            Kind = CombatItemKind.Thematic,
            IconFamilyId = iconFamilyId,
            StatModifiers = modifiers,
        };
    }

    private static string BuildIndividualDescription(
        CombatItemStatKind stat,
        CombatItemModifierScale scale,
        int intensityRank)
    {
        var signedLabel = FormatIntensityLabel(intensityRank);
        var scaleLabel = scale == CombatItemModifierScale.Absolute ? "absolute" : "relative";
        return $"Permanently changes {stat} ({scaleLabel} {signedLabel}).";
    }

    private static string BuildThematicDescription(
        string displayName,
        IReadOnlyList<CombatItemStatModifier> modifiers)
    {
        var parts = modifiers.Select(modifier =>
            $"{modifier.Stat} {FormatIntensityLabel(modifier.IntensityRank)}");
        return $"{displayName}: {string.Join(", ", parts)}.";
    }

    private static string FormatIntensityLabel(int intensityRank)
    {
        var plusOrMinus = intensityRank < 0 ? "-" : "+";
        return new string(plusOrMinus[0], Math.Abs(intensityRank));
    }
}
