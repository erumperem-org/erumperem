using UnityEngine;
using UnityEngine.UI;
using Erumperem.Characters;

/// <summary>
/// Ícone do player no mapa da UI. Pega a referência do personagem Em Jogo
/// direto de PlayableCharacterController.OnCharacterEnteredInGame, converte
/// a posição de mundo dele para dentro do círculo do mapa da UI (usando uma
/// CircularZone como referência de mundo e uma CircularUIZone como
/// referência de UI) e pulsa entre uma cor base e uma cor de destaque,
/// ambas ajustáveis no Inspector.
///
/// Convenção de projeção assumida: X do mundo -> X da UI, Z do mundo -> Y da
/// UI (mapa visto de cima, "norte" = +Z do mundo = topo da tela). Ajuste os
/// sinais em UpdatePosition se a orientação da arte do mapa for diferente.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PlayerMapIcon : MonoBehaviour
{
    [Header("Personagem em jogo")]
    [SerializeField] private PlayableCharacterController characterController;

    [Header("Áreas de referência")]
    [Tooltip("Zona circular que representa os limites reais do mundo (ex: WorldMapArea ou MapLimits).")]
    [SerializeField] private CircularZone worldArea;
    [Tooltip("Zona circular correspondente no Canvas (Screen Space - Overlay) do mapa da UI.")]
    [SerializeField] private CircularUIZone uiArea;

    [Header("Pulso")]
    [Tooltip("Graphic (Image, etc.) cuja cor vai pulsar. Se vazio, tenta pegar do próprio GameObject.")]
    [SerializeField] private Graphic iconGraphic;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color pulseColor = Color.red;
    [SerializeField, Min(0f)] private float pulseSpeed = 2f;

    private Transform _playerTransform;
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = (RectTransform)transform;
        if (iconGraphic == null) iconGraphic = GetComponent<Graphic>();
    }

    private void OnEnable()
    {
        if (characterController == null)
        {
            Debug.LogError($"{nameof(PlayerMapIcon)}: characterController não atribuído.", this);
            return;
        }

        characterController.OnCharacterEnteredInGame += HandleCharacterEnteredInGame;

        // Cobre o caso do controller já ter concluído o load (Start async)
        // antes deste componente habilitar - sincroniza sem esperar a
        // próxima troca de personagem.
        if (characterController.InGameCharacter != null)
        {
            _playerTransform = characterController.InGameCharacter.transform;
        }
    }

    private void OnDisable()
    {
        if (characterController == null) return;
        characterController.OnCharacterEnteredInGame -= HandleCharacterEnteredInGame;
    }

    private void HandleCharacterEnteredInGame(PlayableCharacters character) =>
        _playerTransform = character != null ? character.transform : null;

    private void Update()
    {
        UpdatePosition();
        UpdatePulse();
    }

    private void UpdatePosition()
    {
        if (_playerTransform == null || worldArea == null || uiArea == null) return;
        if (worldArea.Radius <= 0f) return;

        Vector3 delta = _playerTransform.position - worldArea.Center;
        delta.y = 0f;

        Vector2 normalized = new Vector2(delta.x, delta.z) / worldArea.Radius;

        // Trava o ícone na borda em vez de deixá-lo "vazar" para fora do
        // círculo da UI, caso o player saia da área mapeada.
        if (normalized.sqrMagnitude > 1f) normalized.Normalize();

        _rectTransform.anchoredPosition = uiArea.Center + normalized * uiArea.Radius;
    }

    private void UpdatePulse()
    {
        if (iconGraphic == null) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        iconGraphic.color = Color.Lerp(baseColor, pulseColor, t);
    }
}