using System;
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

        [Tooltip(
            "Se verdadeiro, emite <sprite name=\"...\"> apenas quando o sprite existe no atlas. " +
            "Se o sprite faltar, TMP mostraria um quadrado amarelo com ? — nesses casos só fica a cor do texto.")]
        [SerializeField] private bool _emitSpriteTags = false;

        public TMP_SpriteAsset SpriteAtlas => _spriteAtlas;

        public bool EmitSpriteTags => _emitSpriteTags;

        public string BuildSpriteNameForDot(DotType dotType) =>
            _spriteNamePrefix + "dot_" + dotType.ToString().ToLowerInvariant();

        public string BuildSpriteNameForElement(ElementType element) =>
            _spriteNamePrefix + "elem_" + element.ToString().ToLowerInvariant();

        public string BuildSpriteNameForToken(TokenType token) =>
            _spriteNamePrefix + "token_" + token.ToString().ToLowerInvariant();

        /// <summary>
        /// True only when <see cref="_emitSpriteTags"/> is on and the named glyph exists in the atlas.
        /// Missing sprites must never be emitted (TMP fallback is the yellow ? square).
        /// </summary>
        public bool TryResolveExistingSpriteName(string spriteName, out string resolvedSpriteName)
        {
            resolvedSpriteName = null;
            if (!_emitSpriteTags)
            {
                return false;
            }

            return TryFindSpriteNameInAtlas(spriteName, out resolvedSpriteName);
        }

        /// <summary>
        /// True when the atlas contains a glyph with this name (ignore emit toggle).
        /// </summary>
        public bool TryFindSpriteNameInAtlas(string spriteName, out string resolvedSpriteName)
        {
            resolvedSpriteName = null;
            if (_spriteAtlas == null || string.IsNullOrWhiteSpace(spriteName))
            {
                return false;
            }

            var spriteCharacterTable = _spriteAtlas.spriteCharacterTable;
            if (spriteCharacterTable == null || spriteCharacterTable.Count == 0)
            {
                return false;
            }

            for (var spriteIndex = 0; spriteIndex < spriteCharacterTable.Count; spriteIndex++)
            {
                var spriteCharacter = spriteCharacterTable[spriteIndex];
                if (spriteCharacter == null)
                {
                    continue;
                }

                if (string.Equals(spriteCharacter.name, spriteName, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedSpriteName = spriteCharacter.name;
                    return true;
                }
            }

            return false;
        }
    }
}
