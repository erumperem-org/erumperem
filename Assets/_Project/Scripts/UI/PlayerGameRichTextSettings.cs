using Game.Core.Domain;
using TMPro;
using UnityEngine;

namespace Erumperem.UI
{
    /// <summary>
    /// Opcional: mapeia tipos de dados do jogo para nomes de sprites no <see cref="TMP_SpriteAsset"/>
    /// (os nomes devem existir na folha atribuída ao texto). Coloca uma instância em
    /// Resources com o nome "PlayerGameRichTextSettings" para carregamento automático, ou atribui
    /// via <see cref="PlayerGameRichTextSettingsProvider"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerGameRichTextSettings", menuName = "Erumperem/UI/Player Game Rich Text Settings")]
    public sealed class PlayerGameRichTextSettings : ScriptableObject
    {
        [Tooltip("Sprite asset usado pelos componentes TMP (deve ser o mesmo ou compatível com o atribuído ao TextMeshPro).")]
        [SerializeField] private TMP_SpriteAsset _spriteAtlas;

        [Tooltip("Prefixo opcional para nomes de sprite gerados (ex.: \"icon_\" + bleed => icon_bleed).")]
        [SerializeField] private string _spriteNamePrefix = "";

        [Tooltip("Se verdadeiro, emite <sprite name=\"...\">; caso contrário só cor + símbolo unicode.")]
        [SerializeField] private bool _emitSpriteTags = true;

        public TMP_SpriteAsset SpriteAtlas => _spriteAtlas;

        public bool EmitSpriteTags => _emitSpriteTags;

        public string BuildSpriteNameForDot(DotType dotType) =>
            _spriteNamePrefix + "dot_" + dotType.ToString().ToLowerInvariant();

        public string BuildSpriteNameForElement(ElementType element) =>
            _spriteNamePrefix + "elem_" + element.ToString().ToLowerInvariant();

        public string BuildSpriteNameForToken(TokenType token) =>
            _spriteNamePrefix + "token_" + token.ToString().ToLowerInvariant();
    }
}
