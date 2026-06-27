using System;
using System.Text.RegularExpressions;
using Game.Core.Domain;
using UnityEngine;

namespace Erumperem.UI
{
    /// <summary>
    /// Converte marcas de autor (colchetes) em rich text do TextMeshPro e em links especiais
    /// animados por TmpAuthoredTextEffectDriver.
    /// </summary>
    public static class PlayerGameRichText
    {
        public const string LinkIdRainbow = "fx_rainbow";
        public const string LinkIdShake = "fx_shake";
        public const string LinkIdWobble = "fx_wobble";

        private static PlayerGameRichTextSettings _settingsOverride;

        public static void SetSettingsOverride(PlayerGameRichTextSettings settings) =>
            _settingsOverride = settings;

        public static string ExpandAuthoringMarkupToTextMeshPro(string sourceText)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                return string.Empty;
            }

            var settings = ResolveSettings();
            var working = sourceText;
            working = ExpandSelfClosingIconTags(working, settings);
            working = ExpandColorSpans(working);
            working = ExpandSimpleEffectSpan(working, "rainbow", LinkIdRainbow);
            working = ExpandSimpleEffectSpan(working, "shake", LinkIdShake);
            working = ExpandSimpleEffectSpan(working, "wobble", LinkIdWobble);
            return working;
        }

        private static PlayerGameRichTextSettings ResolveSettings() =>
            _settingsOverride != null
                ? _settingsOverride
                : Resources.Load<PlayerGameRichTextSettings>("PlayerGameRichTextSettings");

        private static string ExpandSelfClosingIconTags(string text, PlayerGameRichTextSettings settings)
        {
            text = Regex.Replace(
                text,
                @"\[dot\s+(\w+)\]",
                match => ReplaceDotTag(match.Groups[1].Value, settings),
                RegexOptions.IgnoreCase);

            text = Regex.Replace(
                text,
                @"\[elem\s+(\w+)\]",
                match => ReplaceElementTag(match.Groups[1].Value, settings),
                RegexOptions.IgnoreCase);

            return Regex.Replace(
                text,
                @"\[token\s+(\w+)\]",
                match => ReplaceTokenTag(match.Groups[1].Value, settings),
                RegexOptions.IgnoreCase);
        }

        private static string ReplaceDotTag(string raw, PlayerGameRichTextSettings settings)
        {
            if (!Enum.TryParse<DotType>(raw, ignoreCase: true, out var dot))
            {
                return raw;
            }

            var label = DotTypeDisplayName(dot);
            var color = DotTypeAccentColorHex(dot);
            var icon = TrySpritePrefixDot(dot, settings);
            return $"{icon}<color=#{color}>{label}</color>";
        }

        private static string ReplaceElementTag(string raw, PlayerGameRichTextSettings settings)
        {
            if (!Enum.TryParse<ElementType>(raw, ignoreCase: true, out var element))
            {
                return raw;
            }

            var label = ElementTypeDisplayName(element);
            var color = ElementTypeAccentColorHex(element);
            var icon = TrySpritePrefixElement(element, settings);
            return $"{icon}<color=#{color}>{label}</color>";
        }

        private static string ReplaceTokenTag(string raw, PlayerGameRichTextSettings settings)
        {
            if (!Enum.TryParse<TokenType>(raw, ignoreCase: true, out var token))
            {
                return raw;
            }

            var label = TokenTypeDisplayName(token);
            const string color = "EEEEEE";
            var icon = TrySpritePrefixToken(token, settings);
            return $"{icon}<color=#{color}>{label}</color>";
        }

        private static string TrySpritePrefixDot(DotType dot, PlayerGameRichTextSettings settings)
        {
            if (settings == null || !settings.EmitSpriteTags)
            {
                return string.Empty;
            }

            var name = settings.BuildSpriteNameForDot(dot);
            return string.IsNullOrEmpty(name) ? string.Empty : $"<sprite name=\"{name}\"> ";
        }

        private static string TrySpritePrefixElement(ElementType element, PlayerGameRichTextSettings settings)
        {
            if (settings == null || !settings.EmitSpriteTags)
            {
                return string.Empty;
            }

            var name = settings.BuildSpriteNameForElement(element);
            return string.IsNullOrEmpty(name) ? string.Empty : $"<sprite name=\"{name}\"> ";
        }

        private static string TrySpritePrefixToken(TokenType token, PlayerGameRichTextSettings settings)
        {
            if (settings == null || !settings.EmitSpriteTags)
            {
                return string.Empty;
            }

            var name = settings.BuildSpriteNameForToken(token);
            return string.IsNullOrEmpty(name) ? string.Empty : $"<sprite name=\"{name}\"> ";
        }

        private static string ExpandColorSpans(string text)
        {
            const string tag = "c";
            while (true)
            {
                var openIdx = text.IndexOf("[c ", StringComparison.OrdinalIgnoreCase);
                if (openIdx < 0)
                {
                    return text;
                }

                var closeBracket = text.IndexOf(']', openIdx);
                if (closeBracket <= openIdx + 3)
                {
                    return text;
                }

                var argument = text.Substring(openIdx + 3, closeBracket - (openIdx + 3)).Trim();
                var colorOpen = OpenColorArgumentToTmp(argument);
                var innerStart = closeBracket + 1;
                var closeIdx = FindBalancedEndTag(text, innerStart, tag);
                if (closeIdx < 0)
                {
                    return text;
                }

                var inner = text.Substring(innerStart, closeIdx - innerStart);
                var replacement = $"<color={colorOpen}>{inner}</color>";
                const string closeLiteral = "[/c]";
                text = text.Substring(0, openIdx) + replacement + text.Substring(closeIdx + closeLiteral.Length);
            }
        }

        private static string ExpandSimpleEffectSpan(string text, string tagName, string linkId)
        {
            var openTag = "[" + tagName + "]";
            var closeTag = "[/" + tagName + "]";
            while (true)
            {
                var openIdx = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
                if (openIdx < 0)
                {
                    return text;
                }

                var innerStart = openIdx + openTag.Length;
                var closeIdx = FindBalancedEndTag(text, innerStart, tagName);
                if (closeIdx < 0)
                {
                    return text;
                }

                var inner = text.Substring(innerStart, closeIdx - innerStart);
                var wrap = $"<link=\"{linkId}\">{inner}</link>";
                text = text.Substring(0, openIdx) + wrap + text.Substring(closeIdx + closeTag.Length);
            }
        }

        private static int FindBalancedEndTag(string text, int contentStart, string tagName)
        {
            var closeTag = "[/" + tagName + "]";
            var isColorTag = tagName.Equals("c", StringComparison.OrdinalIgnoreCase);
            var openPrefix = isColorTag ? "[c " : "[" + tagName;
            var depth = 1;
            var scan = contentStart;
            while (scan < text.Length && depth > 0)
            {
                var nextOpen = text.IndexOf(openPrefix, scan, StringComparison.OrdinalIgnoreCase);
                var nextClose = text.IndexOf(closeTag, scan, StringComparison.OrdinalIgnoreCase);
                if (nextClose < 0)
                {
                    return -1;
                }

                var validNestedOpen = false;
                if (nextOpen >= 0 && nextOpen < nextClose)
                {
                    var bracketClose = text.IndexOf(']', nextOpen);
                    if (bracketClose > nextOpen)
                    {
                        if (isColorTag)
                        {
                            validNestedOpen = text.Length - nextOpen >= 3 &&
                                string.Compare(text, nextOpen, "[c ", 0, 3, StringComparison.OrdinalIgnoreCase) == 0;
                        }
                        else
                        {
                            validNestedOpen = bracketClose == nextOpen + openPrefix.Length;
                        }
                    }
                }

                if (validNestedOpen)
                {
                    depth++;
                    scan = text.IndexOf(']', nextOpen) + 1;
                }
                else
                {
                    depth--;
                    if (depth == 0)
                    {
                        return nextClose;
                    }

                    scan = nextClose + closeTag.Length;
                }
            }

            return -1;
        }

        private static string OpenColorArgumentToTmp(string argument)
        {
            var t = argument.Trim();
            if (t.StartsWith("#", StringComparison.Ordinal))
            {
                return t;
            }

            return NamedColorToHex(t);
        }

        private static string NamedColorToHex(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "fire" => "#E85D4C",
                "metal" => "#9CA6C4",
                "anomaly" => "#C084FC",
                "none" => "#DDDDDD",
                "damage" => "#CC3333",
                "heal" => "#4ADE80",
                "buff" => "#38BDF8",
                "debuff" => "#F97316",
                _ => "#FFFFFF",
            };
        }

        private static string DotTypeDisplayName(DotType dotType) =>
            dotType switch
            {
                DotType.Bleed => "Bleed",
                DotType.Blight => "Blight",
                DotType.Burn => "Burn",
                _ => dotType.ToString(),
            };

        private static string ElementTypeDisplayName(ElementType elementType) =>
            elementType switch
            {
                ElementType.None => "Neutral",
                ElementType.Fire => "Fire",
                ElementType.Metal => "Metal",
                ElementType.Anomaly => "Anomaly",
                _ => elementType.ToString(),
            };

        private static string TokenTypeDisplayName(TokenType tokenType) =>
            tokenType switch
            {
                TokenType.Block => "Block",
                TokenType.BlockPlus => "Block Plus",
                TokenType.Dodge => "Dodge",
                TokenType.Blind => "Blind",
                TokenType.Taunt => "Taunt",
                TokenType.Stealth => "Stealth",
                TokenType.Combo => "Combo",
                TokenType.Stun => "Stun",
                _ => tokenType.ToString(),
            };

        private static string DotTypeAccentColorHex(DotType dotType) =>
            dotType switch
            {
                DotType.Bleed => "FF1F1F",
                DotType.Blight => "1F6B22",
                DotType.Burn => "FF8C00",
                _ => "FFFFFF",
            };

        private static string ElementTypeAccentColorHex(ElementType elementType) =>
            elementType switch
            {
                ElementType.Fire => "E85D4C",
                ElementType.Metal => "9CA6C4",
                ElementType.Anomaly => "C084FC",
                ElementType.None => "DDDDDD",
                _ => "FFFFFF",
            };
    }
}