namespace Game.Core.Presentation;

/// <summary>
/// Picks which skill slot the combat HUD description area shows. Priority: locked selection,
/// hovered slot, last shown slot for the hero, then the first slot. Never blank while the hero has skills.
/// </summary>
public static class CombatSkillDescriptionSlotResolver
{
    public static int? Resolve(int skillCount, int? selectedSlot, int? hoveredSlot, int? lastShownSlot)
    {
        if (skillCount <= 0)
        {
            return null;
        }

        foreach (var candidate in new[] { selectedSlot, hoveredSlot, lastShownSlot })
        {
            if (candidate.HasValue && candidate.Value >= 0 && candidate.Value < skillCount)
            {
                return candidate.Value;
            }
        }

        return 0;
    }
}
