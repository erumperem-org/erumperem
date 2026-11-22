using System;
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

    /// <summary>Compact tooltip for a corruption tier: one short line per effect the engine applies.</summary>
    public static string BuildTierHoverAuthoredMarkup(
        int corruptionTier,
        CorruptionTierModifiers tierModifiers)
    {
        var markupBuilder = new StringBuilder();
        markupBuilder.Append("[c danger]Tier ");
        markupBuilder.Append(corruptionTier.ToString(CultureInfo.InvariantCulture));
        markupBuilder.Append("[/c]");

        if (corruptionTier <= 0)
        {
            markupBuilder.Append("\nNo effects.");
            return markupBuilder.ToString();
        }

        markupBuilder.Append("\nEveryone: ");
        markupBuilder.Append(FormatSignedPercent(tierModifiers.GlobalAccuracyBonus));
        markupBuilder.Append(" hit, ");
        markupBuilder.Append(FormatSignedPercent(tierModifiers.GlobalCritBonus));
        markupBuilder.Append(" crit");

        if (corruptionTier <= 3)
        {
            markupBuilder.Append("\nHero corruption passives active");
            return markupBuilder.ToString();
        }

        markupBuilder.Append("\n<color=#FF4444>Enemies always hit and crit</color>");
        markupBuilder.Append("\nYou take ");
        markupBuilder.Append(FormatSignedPercent(tierModifiers.PlayerDamageTakenMultiplier - 1.0));
        markupBuilder.Append(" damage");
        markupBuilder.Append("\nEnemy crits ×");
        markupBuilder.Append(
            tierModifiers.EnemyCritDamageMultiplierAgainstPlayer.ToString("0.##", CultureInfo.InvariantCulture));
        markupBuilder.Append("\nHero passives capped at tier 3");
        return markupBuilder.ToString();
    }

    private static string FormatSignedPercent(double fraction)
    {
        var percentPoints = fraction * 100.0;
        var formatted = percentPoints.ToString("0.#", CultureInfo.InvariantCulture);
        return percentPoints >= 0 ? $"+{formatted}%" : $"{formatted}%";
    }
}