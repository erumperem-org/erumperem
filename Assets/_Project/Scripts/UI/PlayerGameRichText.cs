using System;
using System.Text.RegularExpressions;
using Game.Core.Domain;
using TMPro;
using UnityEngine;

namespace Erumperem.UI
{
    /// <summary>
    /// Converte marcas de autor em Rich Text do TextMeshPro.
    /// DOTs e Tokens podem possuir ícones inline.
    /// Skills permanecem apenas como texto em Rich Text.
    /// </summary>
    public static class PlayerGameRichText
    {
        public const string LinkIdRainbow = "fx_rainbow";
        public const string LinkIdShake = "fx_shake";
        public const string LinkIdWobble = "fx_wobble";

        private const int InlineSpriteSizePercent = 115;
        private const string InlineSpriteVerticalOffset = "0.08em";
        private const string InlineSpriteLeftSpacing = "2px";
        private const string InlineSpriteRightSpacing = "1px";

        private static PlayerGameRichTextSettings _settingsOverride;

        public static void SetSettingsOverride(PlayerGameRichTextSettings settings)
        {
            _settingsOverride = settings;
        }

        public static void ConfigureTextComponent(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            var settings = ResolveSettings();

            if (settings == null)
            {
                Debug.LogError("PlayerGameRichText: nenhum PlayerGameRichTextSettings válido foi encontrado.");
                return;
            }

            if (settings.SpriteAtlas == null)
            {
                Debug.LogError($"PlayerGameRichText: '{settings.name}' não possui TMP Sprite Asset configurado.");
                return;
            }

            settings.SpriteAtlas.UpdateLookupTables();
            text.richText = true;
            text.spriteAsset = settings.SpriteAtlas;
        }

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

        private static PlayerGameRichTextSettings ResolveSettings()
        {
            if (_settingsOverride != null)
            {
                return _settingsOverride;
            }

            var settingsAssets = Resources.LoadAll<PlayerGameRichTextSettings>(string.Empty);

            foreach (var settings in settingsAssets)
            {
                if (settings == null || settings.SpriteAtlas == null || !settings.EmitSpriteTags)
                {
                    continue;
                }

                settings.SpriteAtlas.UpdateLookupTables();

                if (settings.SpriteAtlas.GetSpriteIndexFromName("dot_bleed") >= 0)
                {
                    return settings;
                }
            }

            return null;
        }

        private static string ExpandSelfClosingIconTags(string text, PlayerGameRichTextSettings settings)
        {
            text = Regex.Replace(text, @"\[dot\s+(\w+)\]", match => ReplaceDotTag(match.Groups[1].Value, settings), RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\[elem\s+(\w+)\]", match => ReplaceElementTag(match.Groups[1].Value, settings), RegexOptions.IgnoreCase);
            return Regex.Replace(text, @"\[token\s+(\w+)\]", match => ReplaceTokenTag(match.Groups[1].Value, settings), RegexOptions.IgnoreCase);
        }

        private static string ReplaceDotTag(string raw, PlayerGameRichTextSettings settings)
        {
            if (!Enum.TryParse<DotType>(raw, true, out var dot))
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
            if (!Enum.TryParse<ElementType>(raw, true, out var element))
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
            if (!Enum.TryParse<TokenType>(raw, true, out var token))
            {
                return raw;
            }

            var label = TokenTypeDisplayName(token);
            var icon = TrySpritePrefixToken(token, settings);
            return $"{icon}<color=#EEEEEE>{label}</color>";
        }

        private static string BuildInlineSpriteTag(int spriteIndex)
        {
            return $"<space={InlineSpriteLeftSpacing}><voffset={InlineSpriteVerticalOffset}><size={InlineSpriteSizePercent}%><sprite index={spriteIndex}></size></voffset><space={InlineSpriteRightSpacing}>";
        }

        private static string TrySpritePrefixDot(DotType dot, PlayerGameRichTextSettings settings)
        {
            if (settings == null || !settings.EmitSpriteTags || settings.SpriteAtlas == null)
            {
                return string.Empty;
            }

            settings.SpriteAtlas.UpdateLookupTables();
            var spriteName = settings.BuildSpriteNameForDot(dot);
            var spriteIndex = settings.SpriteAtlas.GetSpriteIndexFromName(spriteName);

            if (spriteIndex < 0)
            {
                Debug.LogError($"PlayerGameRichText: sprite '{spriteName}' não encontrado em '{settings.SpriteAtlas.name}'.");
                return string.Empty;
            }

            return BuildInlineSpriteTag(spriteIndex);
        }

        private static string TrySpritePrefixElement(ElementType element, PlayerGameRichTextSettings settings)
        {
            if (settings == null || !settings.EmitSpriteTags || settings.SpriteAtlas == null)
            {
                return string.Empty;
            }

            settings.SpriteAtlas.UpdateLookupTables();
            var spriteName = settings.BuildSpriteNameForElement(element);
            var spriteIndex = settings.SpriteAtlas.GetSpriteIndexFromName(spriteName);

            if (spriteIndex < 0)
            {
                Debug.LogError($"PlayerGameRichText: sprite '{spriteName}' não encontrado em '{settings.SpriteAtlas.name}'.");
                return string.Empty;
            }

            return BuildInlineSpriteTag(spriteIndex);
        }

        private static string TrySpritePrefixToken(TokenType token, PlayerGameRichTextSettings settings)
        {
            if (settings == null || !settings.EmitSpriteTags || settings.SpriteAtlas == null)
            {
                return string.Empty;
            }

            settings.SpriteAtlas.UpdateLookupTables();
            var spriteName = settings.BuildSpriteNameForToken(token);
            var spriteIndex = settings.SpriteAtlas.GetSpriteIndexFromName(spriteName);

            if (spriteIndex < 0)
            {
                Debug.LogError($"PlayerGameRichText: sprite '{spriteName}' não encontrado em '{settings.SpriteAtlas.name}'.");
                return string.Empty;
            }

            return BuildInlineSpriteTag(spriteIndex);
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

                var argument = text.Substring(openIdx + 3, closeBracket - openIdx - 3).Trim();
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
                var replacement = $"<link=\"{linkId}\">{inner}</link>";
                text = text.Substring(0, openIdx) + replacement + text.Substring(closeIdx + closeTag.Length);
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
                            validNestedOpen = text.Length - nextOpen >= 3 && string.Compare(text, nextOpen, "[c ", 0, 3, StringComparison.OrdinalIgnoreCase) == 0;
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
            var value = argument.Trim();

            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                return value;
            }

            return NamedColorToHex(value);
        }

        private static string NamedColorToHex(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "fire":
                {
                    return "#E85D4C";
                }

                case "metal":
                {
                    return "#9CA6C4";
                }

                case "anomaly":
                {
                    return "#C084FC";
                }

                case "none":
                {
                    return "#DDDDDD";
                }

                case "damage":
                {
                    return "#CC3333";
                }

                case "heal":
                {
                    return "#4ADE80";
                }

                case "buff":
                {
                    return "#38BDF8";
                }

                case "debuff":
                {
                    return "#F97316";
                }

                default:
                {
                    return "#FFFFFF";
                }
            }
        }

        private static string DotTypeDisplayName(DotType dotType)
        {
            switch (dotType)
            {
                case DotType.Bleed:
                {
                    return "Bleed";
                }

                case DotType.Blight:
                {
                    return "Blight";
                }

                case DotType.Burn:
                {
                    return "Burn";
                }

                default:
                {
                    return dotType.ToString();
                }
            }
        }

        private static string ElementTypeDisplayName(ElementType elementType)
        {
            switch (elementType)
            {
                case ElementType.None:
                {
                    return "Neutral";
                }

                case ElementType.Fire:
                {
                    return "Fire";
                }

                case ElementType.Metal:
                {
                    return "Metal";
                }

                case ElementType.Anomaly:
                {
                    return "Anomaly";
                }

                default:
                {
                    return elementType.ToString();
                }
            }
        }

        private static string TokenTypeDisplayName(TokenType tokenType)
        {
            switch (tokenType)
            {
                case TokenType.Block:
                {
                    return "Block";
                }

                case TokenType.BlockPlus:
                {
                    return "Block Plus";
                }

                case TokenType.Dodge:
                {
                    return "Dodge";
                }

                case TokenType.Blind:
                {
                    return "Blind";
                }

                case TokenType.Taunt:
                {
                    return "Taunt";
                }

                case TokenType.Stealth:
                {
                    return "Stealth";
                }

                case TokenType.Combo:
                {
                    return "Combo";
                }

                case TokenType.Stun:
                {
                    return "Stun";
                }

                default:
                {
                    return tokenType.ToString();
                }
            }
        }

        private static string DotTypeAccentColorHex(DotType dotType)
        {
            switch (dotType)
            {
                case DotType.Bleed:
                {
                    return "FF1F1F";
                }

                case DotType.Blight:
                {
                    return "1F6B22";
                }

                case DotType.Burn:
                {
                    return "FF8C00";
                }

                default:
                {
                    return "FFFFFF";
                }
            }
        }

        private static string ElementTypeAccentColorHex(ElementType elementType)
        {
            switch (elementType)
            {
                case ElementType.Fire:
                {
                    return "E85D4C";
                }

                case ElementType.Metal:
                {
                    return "9CA6C4";
                }

                case ElementType.Anomaly:
                {
                    return "C084FC";
                }

                case ElementType.None:
                {
                    return "DDDDDD";
                }

                default:
                {
                    return "FFFFFF";
                }
            }
        }
    }
}