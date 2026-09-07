using System.Collections.Generic;
using System.Globalization;
using Game.Core.Domain;
using Game.Core.Models;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Single source of truth for the player-facing tooltip text shown when hovering a token / DOT icon.
    /// Returns raw authored markup (<c>[c …]</c>, <c>[dot …]</c>, <c>[token …]</c>) — pass through
    /// <c>PlayerFacingText.PresentForUi</c> before assigning to TextMeshPro.
    /// Keep entries short (1 line, 60–100 chars) so the floating panel stays compact.
    /// </summary>
    public static class TokenAndDotDescriptionLibrary
    {
        public static string GetTokenAuthoredDescription(TokenType tokenType) => tokenType switch
        {
            TokenType.Block =>
                "[token block]: reduces the next physical damage taken.",
            TokenType.BlockPlus =>
                "[token blockplus]: [c buff]strongly[/c] reduces the next physical damage taken.",
            TokenType.Dodge =>
                "[token dodge]: evades the next enemy attack.",
            TokenType.Blind =>
                "[token blind]: the bearer's next attack has a high chance to miss.",
            TokenType.Taunt =>
                "[token taunt]: enemies prioritize targeting this unit; loses 1 stack when hit.",
            TokenType.Stealth =>
                "[token stealth]: cannot be targeted by direct attacks.",
            TokenType.Combo =>
                "[token combo]: accumulates and empowers specific skills; consumed upon use.",
            TokenType.Stun =>
                "[token stun]: the bearer loses their next turn.",
            TokenType.ControlledInstability =>
                "[token controlledinstability]: attackers take 2 damage per stack.",
            TokenType.Destabilization =>
                "[token destabilization]: on death (or force-trigger), all others take 3 damage per stack.",
            TokenType.Strength =>
                "[token strength]: deals 25% more damage per stack; loses 1 at end of turn.",
            TokenType.Defense =>
                "[token defense]: takes 25% less damage per stack; loses 1 at end of turn.",
            TokenType.Weaken =>
                "[token weaken]: deals 50% less damage per stack; loses 1 at end of turn.",
            TokenType.Vulnerability =>
                "[token vulnerability]: takes 50% more damage per stack; loses 1 at end of turn.",
            TokenType.Confusion =>
                "[token confusion]: 33% chance to retarget enemy skills randomly this turn.",
            TokenType.Bleeding =>
                "[token bleeding]: 5% Max HP damage per stack at end of turn; loses 1 stack.",
            TokenType.LuckyShot =>
                "[token luckyshot]: +4% crit chance per stack; loses 1 at end of turn.",
            TokenType.Dexterity =>
                "[token dexterity]: +10% accuracy per stack; loses 1 at end of turn.",
            TokenType.Exposition =>
                "[token exposition]: skills targeting this gain +20% accuracy per stack.",
            TokenType.Corrosion =>
                "[token corrosion]: amplifies other debuffs; takes 5 damage and loses 1 at EOT.",
            TokenType.Mark =>
                "[token mark]: easier to crit; crits deal +50% more damage per stack.",
            TokenType.Regeneration =>
                "[token regeneration]: heals 1 HP per stack at end of turn; loses 1 stack.",
            TokenType.Clumsy =>
                "[token clumsy]: -20% accuracy per stack; loses 1 at end of turn.",
            TokenType.BonusAction =>
                "[token bonusaction]: may act again before the turn fully ends.",
            _ => tokenType.ToString(),
        };

        public static string GetDotAuthoredDescription(
            DotType dotType,
            IReadOnlyList<DotInstance> activeDots = null)
        {
            var potencyRangePhrase = BuildPotencyRangePhrase(activeDots, dotType);

            return dotType switch
            {
                DotType.Bleed => FormatPerTurnDotLine("bleed damage", "[dot bleed]", potencyRangePhrase),
                DotType.Blight => FormatPerTurnDotLine("blight damage", "[dot blight]", potencyRangePhrase),
                DotType.Burn => FormatPerTurnDotLine("burn damage", "[dot burn]", potencyRangePhrase),
                _ => dotType.ToString(),
            };
        }

        private static string FormatPerTurnDotLine(
            string damageKindLabel,
            string dotMarkupTag,
            string potencyRangePhrase)
        {
            if (string.IsNullOrEmpty(potencyRangePhrase))
            {
                return $"Deals {dotMarkupTag} {damageKindLabel} per turn.";
            }

            return $"Deals {potencyRangePhrase} {dotMarkupTag} {damageKindLabel} per turn.";
        }

        private static string BuildPotencyRangePhrase(IReadOnlyList<DotInstance> activeDots, DotType dotType)
        {
            if (activeDots == null || activeDots.Count == 0)
            {
                return string.Empty;
            }

            int? minimumPotency = null;
            int? maximumPotency = null;

            foreach (var dotInstance in activeDots)
            {
                if (dotInstance.Type != dotType)
                {
                    continue;
                }

                minimumPotency = minimumPotency.HasValue
                    ? System.Math.Min(minimumPotency.Value, dotInstance.Potency)
                    : dotInstance.Potency;
                maximumPotency = maximumPotency.HasValue
                    ? System.Math.Max(maximumPotency.Value, dotInstance.Potency)
                    : dotInstance.Potency;
            }

            if (!minimumPotency.HasValue || !maximumPotency.HasValue)
            {
                return string.Empty;
            }

            if (minimumPotency.Value == maximumPotency.Value)
            {
                return minimumPotency.Value.ToString(CultureInfo.InvariantCulture);
            }

            return $"{minimumPotency.Value}-{maximumPotency.Value}";
        }
    }
}