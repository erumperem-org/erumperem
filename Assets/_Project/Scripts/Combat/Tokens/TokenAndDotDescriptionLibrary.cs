using Game.Core.Domain;

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
                "[token combo]: acumula e potencia habilidades específicas; é consumido ao usar.",
            TokenType.Stun =>
                "[token stun]: o portador perde o próximo turno.",
            _ => tokenType.ToString(),
        };

        public static string GetDotAuthoredDescription(DotType dotType) => dotType switch
        {
            DotType.Bleed =>
                "[dot bleed]: causa [c damage]dano físico por turno[/c] enquanto activo.",
            DotType.Blight =>
                "[dot blight]: causa [c anomaly]dano de praga por turno[/c] (anomalia).",
            DotType.Burn =>
                "[dot burn]: causa [c fire]dano de fogo por turno[/c].",
            _ => dotType.ToString(),
        };
    }
}
