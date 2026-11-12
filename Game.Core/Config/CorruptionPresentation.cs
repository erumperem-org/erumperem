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
            markupBuilder.Append("The world is calm. No accuracy or critical bonus from corruption.");
            return markupBuilder.ToString();
        }

        markupBuilder.Append('\n');
        markupBuilder.Append("All skills gain ");
        markupBuilder.Append(FormatSignedPercent(tierModifiers.GlobalAccuracyBonus));
        markupBuilder.Append(" accuracy and ");
        markupBuilder.Append(FormatSignedPercent(tierModifiers.GlobalCritBonus));
        markupBuilder.Append(" critical chance.");

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
            markupBuilder.Append("<color=#FF4444>Enemies have guaranteed hits and always crit!</color>");
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