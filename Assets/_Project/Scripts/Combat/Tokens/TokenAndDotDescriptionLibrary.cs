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
                "[token block]: reduz o próximo dano físico recebido.",
            TokenType.BlockPlus =>
                "[token blockplus]: reduz [c buff]fortemente[/c] o próximo dano físico recebido.",
            TokenType.Dodge =>
                "[token dodge]: evade o próximo ataque inimigo.",
            TokenType.Blind =>
                "[token blind]: o próximo ataque do portador tem grande chance de errar.",
            TokenType.Taunt =>
                "[token taunt]: inimigos priorizam atacar este alvo.",
            TokenType.Stealth =>
                "[token stealth]: não pode ser alvejado por ataques diretos.",
            TokenType.Combo =>
                "[token combo]: acumula e potencializa habilidades específicas; é consumido ao usar.",
            TokenType.Stun =>
                "[token stun]: o portador perde o próximo turno.",
            _ => tokenType.ToString(),
        };

        public static string GetDotAuthoredDescription(
            DotType dotType,
            IReadOnlyList<DotInstance> activeDots = null)
        {
            var potencyRangePhrase = BuildPotencyRangePhrase(activeDots, dotType);

            return dotType switch
            {
                DotType.Bleed => FormatPerTurnDotLine("dano de sangramento", "[dot bleed]", potencyRangePhrase),
                DotType.Blight => FormatPerTurnDotLine("dano de praga", "[dot blight]", potencyRangePhrase),
                DotType.Burn => FormatPerTurnDotLine("dano de fogo", "[dot burn]", potencyRangePhrase),
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
                return $"Causa {damageKindLabel} de {dotMarkupTag} por turno.";
            }

            return $"Causa {potencyRangePhrase} de {dotMarkupTag} por turno.";
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
