using System.Globalization;
using System.Text;

namespace Game.Core.Config;

/// <summary>
/// Player-facing corruption presentation. The real value may exceed 200; the slider is shown as 0–100.
/// Thresholds stay in <see cref="CorruptionRules"/> (off-by-one vs the GDD, locked).
/// </summary>
public static class CorruptionPresentation
{
    public const double PresentedSliderMaximum = 100;

    public static double ClampToPresentedSliderValue(double worldCorruptionValue) =>
        Math.Clamp(
            worldCorruptionValue,
            CorruptionRules.MinCorruptionValue,
            PresentedSliderMaximum);

    public static float ToPresentedSliderNormalizedValue(double worldCorruptionValue) =>
        (float)(ClampToPresentedSliderValue(worldCorruptionValue) / PresentedSliderMaximum);

    public static string BuildTierHoverAuthoredMarkup(
        int corruptionTier,
        CorruptionTierModifiers tierModifiers)
    {
        var markupBuilder = new StringBuilder();
        markupBuilder.Append("[c danger]Corruption Tier ");
        markupBuilder.Append(corruptionTier.ToString(CultureInfo.InvariantCulture));
        markupBuilder.Append("[/c]");

        if (corruptionTier <= 0)
        {
            markupBuilder.Append('\n');
            markupBuilder.Append("The world is relatively calm. Monsters have no accuracy bonus from corruption.");
            return markupBuilder.ToString();
        }

        markupBuilder.Append('\n');
        markupBuilder.Append("Monsters have ");
        markupBuilder.Append(FormatSignedPercent(tierModifiers.EnemyAccuracyBonus));
        markupBuilder.Append(" accuracy.");

        if (corruptionTier >= 1 && corruptionTier <= 3)
        {
            markupBuilder.Append('\n');
            markupBuilder.Append("Hero corruption passives through this tier are active.");
            markupBuilder.Append('\n');
            markupBuilder.Append("Player damage caused and taken are unchanged by this bar.");
        }

        if (corruptionTier >= 4)
        {
            markupBuilder.Append('\n');
            markupBuilder.Append("Enemies are considerably stronger. Player damage caused is unchanged.");
            markupBuilder.Append('\n');
            markupBuilder.Append("You take ");
            markupBuilder.Append(FormatSignedPercent(tierModifiers.PlayerDamageTakenMultiplier - 1.0));
            markupBuilder.Append(" damage.");
            markupBuilder.Append('\n');
            markupBuilder.Append("Enemy critical hits against you deal ×");
            markupBuilder.Append(
                tierModifiers.EnemyCritDamageMultiplierAgainstPlayer.ToString("0.##", CultureInfo.InvariantCulture));
            markupBuilder.Append(" damage.");
            markupBuilder.Append('\n');
            markupBuilder.Append("Hero corruption passives remain at tier 3 strength.");
        }

        return markupBuilder.ToString();
    }

    private static string FormatSignedPercent(double fraction)
    {
        var percentPoints = fraction * 100.0;
        var formatted = percentPoints.ToString("0.#", CultureInfo.InvariantCulture);
        return percentPoints >= 0 ? $"+{formatted}%" : $"{formatted}%";
    }
}
