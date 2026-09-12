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
                "Status — [token block]: reduces the next physical damage taken.",
            TokenType.BlockPlus =>
                "Status — [token blockplus]: [c buff]strongly[/c] reduces the next physical damage taken.",
            TokenType.Dodge =>
                "Status — [token dodge]: evades the next enemy attack.",
            TokenType.Blind =>
                "Status — [token blind]: the bearer's next attack has a high chance to miss.",
            TokenType.Taunt =>
                "Status — [token taunt]: enemies can only select this. Lose 1 stack when hit by an enemy.",
            TokenType.Stealth =>
                "Status — [token stealth]: skills targeting this character have -40% accuracy. Lose 1 stack at the end of turn.",
            TokenType.Combo =>
                "Status — [token combo]: accumulates and empowers specific skills; consumed upon use.",
            TokenType.Stun =>
                "Status — [token stun]: the bearer loses their next turn.",
            TokenType.ControlledInstability =>
                "Status — [token controlledinstability]: enemies who hit you receive 2 damage per stack.",
            TokenType.Destabilization =>
                "Status — [token destabilization]: on death, nearby characters take 3 damage per stack.",
            TokenType.Strength =>
                "Status — [token strength]: deals 25% more damage per stack. Lose 1 stack at the end of turn.",
            TokenType.Defense =>
                "Status — [token defense]: takes 25% less damage per stack. Lose 1 stack at the end of turn.",
            TokenType.Weaken =>
                "Status — [token weaken]: deals 50% less damage per stack. Lose 1 stack at the end of turn.",
            TokenType.Vulnerability =>
                "Status — [token vulnerability]: takes 50% more damage per stack. Lose 1 stack at the end of turn.",
            TokenType.Confusion =>
                "Status — [token confusion]: each skill has 33% chance to swap Ally↔Enemy and Self↔None this turn. Lose 1 stack at the end of turn.",
            TokenType.Bleeding =>
                "Status — [token bleeding]: takes 5% maximum health per stack at the end of turn. Lose 1 stack.",
            TokenType.LuckyShot =>
                "Status — [token luckyshot]: +4% critical chance per stack. Lose 1 stack at the end of turn.",
            TokenType.Dexterity =>
                "Status — [token dexterity]: +10% accuracy per stack. Lose 1 stack at the end of turn.",
            TokenType.Exposition =>
                "Status — [token exposition]: skills targeting this character have +20% accuracy per stack. Lose 1 stack at the end of turn.",
            TokenType.Corrosion =>
                "Status — [token corrosion]: every other debuff type is +10% more effective per stack. Take 5 damage and lose 1 stack at the end of turn.",
            TokenType.Mark =>
                "Status — [token mark]: +10% chance to receive a critical hit. Critical hits deal +50% more damage per stack. Lose 1 stack at the end of turn.",
            TokenType.Regeneration =>
                "Status — [token regeneration]: receive 1 health per stack at the end of turn. Lose 1 stack.",
            TokenType.Clumsy =>
                "Status — [token clumsy]: -20% accuracy per stack. Lose 1 stack at the end of turn.",
            TokenType.BonusAction =>
                "Status — [token bonusaction]: may act again before the turn fully ends.",
            TokenType.Hypnosis =>
                "Status — [token hypnosis]: can only use the skill last used before this Status. Lose 1 stack at the end of turn.",
            TokenType.Dizzy =>
                "Status — [token dizzy]: 50% chance to change which targets the skill will hit. Lose 1 stack at the end of turn.",
            TokenType.Burn =>
                "Status — [token burn]: takes damage equal to stacks at the end of turn. Lose 1 stack.",
            TokenType.PermaStrength =>
                "Status — [token permastrength]: +6.25% Damage Caused per stack; does not decay.",
            TokenType.PermaDefense =>
                "Status — [token permadefense]: +6.25% damage reduction per stack; does not decay.",
            TokenType.PermaWeaken =>
                "Status — [token permaweaken]: -12.5% Damage Caused per stack; does not decay.",
            TokenType.PermaVulnerability =>
                "Status — [token permavulnerability]: +12.5% damage taken per stack; does not decay.",
            TokenType.PermaDexterity =>
                "Status — [token permadexterity]: +2.5% accuracy per stack; does not decay.",
            TokenType.PermaClumsy =>
                "Status — [token permaclumsy]: -5% accuracy per stack; does not decay.",
            TokenType.PermaExposition =>
                "Status — [token permaexposition]: skills targeting this gain +5% accuracy per stack; does not decay.",
            TokenType.PermaStealth =>
                "Status — [token permastealth]: skills targeting this have -10% accuracy per stack; does not decay.",
            _ => "Status — " + tokenType.ToString(),
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