using TMPro;
using UnityEngine;

namespace Erumperem.UI
{
    /// <summary>
    /// Anim vértices/carateres para links criados por <see cref="PlayerGameRichText"/>:
    /// <see cref="PlayerGameRichText.LinkIdRainbow"/>, <see cref="PlayerGameRichText.LinkIdShake"/>,
    /// <see cref="PlayerGameRichText.LinkIdWobble"/>. Coloca no mesmo <see cref="GameObject"/> que o
    /// <see cref="TMP_Text"/> (ex.: TextMeshProUGUI do log ou do painel de detalhe).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TmpAuthoredTextEffectDriver : MonoBehaviour
    {
        [SerializeField] private float _rainbowCycleSeconds = 2f;
        [SerializeField] private float _shakeAmplitudePixels = 2f;
        [SerializeField] private float _wobbleAmplitudePixels = 3f;
        [SerializeField] private float _wobbleFrequency = 4f;

        private TMP_Text _text;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void LateUpdate()
        {
            if (_text == null || !_text.isActiveAndEnabled)
            {
                return;
            }

            _text.ForceMeshUpdate(ignoreActiveState: true);
            var textInfo = _text.textInfo;
            if (textInfo == null || textInfo.linkCount == 0)
            {
                return;
            }

            var now = Time.time;
            for (var linkIndex = 0; linkIndex < textInfo.linkCount; linkIndex++)
            {
                var link = textInfo.linkInfo[linkIndex];
                var linkId = link.GetLinkID();
                if (linkId == PlayerGameRichText.LinkIdRainbow)
                {
                    ApplyRainbowToLink(textInfo, link, now);
                }
                else if (linkId == PlayerGameRichText.LinkIdShake)
                {
                    ApplyShakeToLink(textInfo, link, now);
                }
                else if (linkId == PlayerGameRichText.LinkIdWobble)
                {
                    ApplyWobbleToLink(textInfo, link, now);
                }
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private void ApplyRainbowToLink(TMP_TextInfo textInfo, TMP_LinkInfo link, float timeSeconds)
        {
            var firstCharacterIndex = link.linkTextfirstCharacterIndex;
            var characterCountInLink = link.linkTextLength;
            for (var characterIndex = firstCharacterIndex;
                 characterIndex < firstCharacterIndex + characterCountInLink && characterIndex < textInfo.characterCount;
                 characterIndex++)
            {
                var characterInfo = textInfo.characterInfo[characterIndex];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                var materialReferenceIndex = characterInfo.materialReferenceIndex;
                var vertexIndex = characterInfo.vertexIndex;
                var colors = textInfo.meshInfo[materialReferenceIndex].colors32;
                var hue = (timeSeconds / Mathf.Max(0.05f, _rainbowCycleSeconds) + characterIndex * 0.07f) % 1f;
                var color = Color.HSVToRGB(hue, 0.55f, 1f);
                var color32 = (Color32)color;
                for (var vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    colors[vertexIndex + vertexOffset] = color32;
                }
            }
        }

        private void ApplyShakeToLink(TMP_TextInfo textInfo, TMP_LinkInfo link, float timeSeconds)
        {
            var firstCharacterIndex = link.linkTextfirstCharacterIndex;
            var characterCountInLink = link.linkTextLength;
            for (var characterIndex = firstCharacterIndex;
                 characterIndex < firstCharacterIndex + characterCountInLink && characterIndex < textInfo.characterCount;
                 characterIndex++)
            {
                var characterInfo = textInfo.characterInfo[characterIndex];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                var materialReferenceIndex = characterInfo.materialReferenceIndex;
                var vertexIndex = characterInfo.vertexIndex;
                var vertices = textInfo.meshInfo[materialReferenceIndex].vertices;
                var deltaX = (Mathf.PerlinNoise(characterIndex * 0.31f, timeSeconds * 24f) - 0.5f) * 2f *
                             _shakeAmplitudePixels;
                var deltaY = (Mathf.PerlinNoise(characterIndex * 0.17f, timeSeconds * 30f + 19f) - 0.5f) * 2f *
                             _shakeAmplitudePixels;
                var delta = new Vector3(deltaX, deltaY, 0f);
                for (var vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    vertices[vertexIndex + vertexOffset] += delta;
                }
            }
        }

        private void ApplyWobbleToLink(TMP_TextInfo textInfo, TMP_LinkInfo link, float timeSeconds)
        {
            var firstCharacterIndex = link.linkTextfirstCharacterIndex;
            var characterCountInLink = link.linkTextLength;
            for (var characterIndex = firstCharacterIndex;
                 characterIndex < firstCharacterIndex + characterCountInLink && characterIndex < textInfo.characterCount;
                 characterIndex++)
            {
                var characterInfo = textInfo.characterInfo[characterIndex];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                var materialReferenceIndex = characterInfo.materialReferenceIndex;
                var vertexIndex = characterInfo.vertexIndex;
                var vertices = textInfo.meshInfo[materialReferenceIndex].vertices;
                var phase = characterIndex * 0.45f + timeSeconds * _wobbleFrequency;
                var deltaX = Mathf.Sin(phase) * _wobbleAmplitudePixels * 0.5f;
                var deltaY = Mathf.Cos(phase * 1.1f) * _wobbleAmplitudePixels;
                var delta = new Vector3(deltaX, deltaY, 0f);
                for (var vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    vertices[vertexIndex + vertexOffset] += delta;
                }
            }
        }
    }
}
