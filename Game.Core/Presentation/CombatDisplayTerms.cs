using System.Globalization;
using System.Text.RegularExpressions;
using Game.Core.Domain;

namespace Game.Core.Presentation;

/// <summary>
/// Single source for English combat UI terms: skill name aliases, token/DoT labels, and inline translation.
/// </summary>
public static class CombatDisplayTerms
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> PortugueseToEnglishNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wulfric"] = "Splintered Knight",
        ["Buck"] = "The Gunslinger",
        ["Talho direto"] = "Direct Slash",
        ["Empurrão brutal"] = "Brutal Shove",
        ["Postura de lobo"] = "Wolf Stance",
        ["Rasgar tendão"] = "Tear Tendon",
        ["Fio candente"] = "Searing Thread",
        ["Execução de leilão"] = "Auction Execution",
        ["Remendar couraça"] = "Mend Cuirass",
        ["Muralha"] = "Bulwark",
        ["Salvaguarda"] = "Safeguard",
        ["Fio da anomalia"] = "Anomaly Thread",
        ["Puxar o veio"] = "Pull the Veil",
        ["Puxar o véu"] = "Pull the Veil",
        ["Abrir o vão"] = "Open the Gap",
        ["Soco Normal"] = "Normal Punch",
        ["Lança Pedra"] = "Rock Throw",
        ["Para-raio"] = "Lightning Rod",
        ["Laser Distorcido"] = "Distorted Laser",
        ["Giro Turbina (Ametista)"] = "Turbine Spin (Amethyst)",
        ["Giro Turbina (Ágata)"] = "Turbine Spin (Agate)",
        ["Giro Turbina (Citrino)"] = "Turbine Spin (Citrine)",
        ["Giro Turbina (Esmeralda)"] = "Turbine Spin (Emerald)",
        ["Açoite Metálico"] = "Iron Lash",
        ["Brilho Hipnótico"] = "Hypnotic Glare",
        ["Sussurro do Vício"] = "Whisper of Vice",
        ["Colapso da Armadura"] = "Armor Collapse",
        ["Garra"] = "Claw",
        ["Garras afiadas"] = "Sharp Claws",
        ["Mordida dilacerante"] = "Painful Bite",
        ["Uivo gelado"] = "Chilling Howl",
        ["Vómito cegante"] = "Blinding Vomit",
        ["Vomito cegante"] = "Blinding Vomit",
        ["Disparo rápido"] = "Quick Shot",
        ["Empurrão do coldre"] = "Holster Shove",
        ["Postura do duelista"] = "Duelist Stance",
        ["Tiro incendiário"] = "Incendiary Shot",
        ["Rajada flamejante"] = "Flaming Volley",
        ["Execução do pistoleiro"] = "Gunslinger Execution",
        ["Reforço de couro"] = "Leather Reinforcement",
        ["Barricada"] = "Barricade",
        ["Último recurso"] = "Last Resort",
        ["Fio do revólver"] = "Revolver Thread",
        ["Puxar o gatilho"] = "Pull the Trigger",
        ["Abrir fogo"] = "Open Fire",
        ["Aranha Stunadora"] = "Stunner Spider",
    };

    public static string TranslateExactName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (PortugueseToEnglishNames.TryGetValue(name.Trim(), out var translated))
        {
            return translated;
        }

        return name;
    }

    /// <summary>
    /// Replaces known Portuguese terms and technical ids embedded in a longer English string.
    /// </summary>
    public static string TranslateEmbeddedTerms(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var working = text;
        foreach (var nameEntry in PortugueseToEnglishNames)
        {
            working = Regex.Replace(
                working,
                @"\b" + Regex.Escape(nameEntry.Key) + @"\b",
                nameEntry.Value,
                RegexOptions.IgnoreCase);
        }

        working = Regex.Replace(working, @"\benemy_?\d+\b", "Corrupted Enemy", RegexOptions.IgnoreCase);
        return working;
    }

    public static string FormatPercentFromFraction(double fraction) =>
        (fraction * 100.0).ToString("0.##", EnglishCulture) + "%";

    public static string FormatPlainNumber(double value) =>
        value.ToString("0.##", EnglishCulture);

    public static string FormatTokenDisplayName(TokenType tokenType) =>
        tokenType switch
        {
            TokenType.Taunt => "Taunt",
            TokenType.Stealth => "Stealth",
            TokenType.Stun => "Stun",
            TokenType.ControlledInstability => "Controlled Instability",
            TokenType.Destabilization => "Destabilization",
            TokenType.Strength => "Strength",
            TokenType.Defense => "Defense",
            TokenType.Weaken => "Weaken",
            TokenType.Vulnerability => "Vulnerability",
            TokenType.Confusion => "Confusion",
            TokenType.Bleeding => "Bleeding",
            TokenType.LuckyShot => "Lucky Shot",
            TokenType.Dexterity => "Dexterity",
            TokenType.Exposition => "Exposition",
            TokenType.Corrosion => "Corrosion",
            TokenType.Mark => "Mark",
            TokenType.Regeneration => "Regeneration",
            TokenType.Clumsy => "Clumsy",
            TokenType.BonusAction => "Bonus Action",
            TokenType.Hypnosis => "Hypnosis",
            TokenType.Dizzy => "Dizzy",
            TokenType.Burn => "Burn",
            TokenType.PermaStrength => "Perma Strength",
            TokenType.PermaDefense => "Perma Defense",
            TokenType.PermaWeaken => "Perma Weaken",
            TokenType.PermaVulnerability => "Perma Vulnerability",
            TokenType.PermaDexterity => "Perma Dexterity",
            TokenType.PermaClumsy => "Perma Clumsy",
            TokenType.PermaExposition => "Perma Exposition",
            TokenType.PermaStealth => "Perma Stealth",
            _ => tokenType.ToString(),
        };

    public static string FormatDotDisplayName(DotType dotType) =>
        dotType switch
        {
            DotType.Bleed => "Bleed",
            DotType.Blight => "Blight",
            DotType.Burn => "Burn",
            _ => dotType.ToString(),
        };

    public static string FormatTurnCount(int turns) =>
        turns == 1 ? "1 turn" : $"{turns} turns";

    public static string FormatDamageCausedBonus(double additiveFraction)
    {
        if (additiveFraction >= 0)
        {
            return $"deal {FormatPercentFromFraction(additiveFraction)} more damage";
        }

        return $"deal {FormatPercentFromFraction(-additiveFraction)} less damage";
    }
}
