// =============================================================================
// CharacterData.cs
// ScriptableObject que define tudo que difere entre os 3 personagens:
//   - Stats (velocidade, altura de pulo)
//   - AnimatorController específico
//   - Prefab visual (mesh/skin)
//
// COMO USAR:
//   Assets > Create > CharacterSystem > CharacterData
//   Crie um asset para cada personagem (Warrior, Mage, Archer, etc.)
//   e arraste-os para o CharacterSwitcher no Inspector.
// =============================================================================

using UnityEngine;

namespace CharacterSystem.Character
{
    /// <summary>
    /// Dados imutáveis de um personagem jogável.
    /// Segue o padrão Data-Driven: adicionar um quarto personagem
    /// não exige nenhuma mudança de código — apenas um novo .asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterData_New",
        menuName = "CharacterSystem/PlayableCharacterData",
        order = 0)]
    public class PlayableCharacterData : ScriptableObject
    {
        // ── Identificação ────────────────────────────────────────────────────

        [Header("Identificação")]
        [Tooltip("Nome legível do personagem (usado em UI e logs).")]
        [SerializeField] private CharacterData data;

        // ── Animação ─────────────────────────────────────────────────────────

        [Header("Animação")]
        [Tooltip("AnimatorController específico deste personagem. " +
                 "Deve ter os mesmos parâmetros definidos em AnimatorParameters.cs.")]
        [SerializeField] private RuntimeAnimatorController _animatorController;

        // ── Locomotion Stats ─────────────────────────────────────────────────

        [Header("Locomotion")]
        [Tooltip("Velocidade máxima de caminhada (m/s).")]
        [SerializeField, Range(1f, 10f)] private float _walkSpeed = 4f;

        [Tooltip("Velocidade máxima de corrida (m/s).")]
        [SerializeField, Range(2f, 20f)] private float _runSpeed = 8f;

        [Tooltip("Força vertical aplicada no pulo.")]
        [SerializeField, Range(3f, 15f)] private float _jumpForce = 6f;

        [Tooltip("Multiplicador de gravidade (1 = gravidade padrão da Unity).")]
        [SerializeField, Range(1f, 5f)] private float _gravityMultiplier = 2f;

        [Tooltip("Velocidade de rotação do personagem em direção ao movimento (graus/s).")]
        [SerializeField, Range(100f, 1440f)] private float _rotationSpeed = 720f;

        // ── Interaction Stats ────────────────────────────────────────────────

        [Header("Interaction")]
        [Tooltip("Raio em metros para detectar objetos interativos.")]
        [SerializeField, Range(0.5f, 5f)] private float _interactionRadius = 2f;

        // ── Visual ───────────────────────────────────────────────────────────

        [Header("Visual")]
        [Tooltip("Prefab do mesh/skin deste personagem. " +
                 "Será instanciado como filho do root do jogador.")]
        [SerializeField] private GameObject _characterMeshPrefab;

        // ── Propriedades públicas (somente leitura) ──────────────────────────

        public string CharacterName       => data.name;
        public RuntimeAnimatorController  AnimatorController => _animatorController;
        public float WalkSpeed            => _walkSpeed;
        public float RunSpeed             => _runSpeed;
        public float JumpForce            => _jumpForce;
        public float GravityMultiplier    => _gravityMultiplier;
        public float RotationSpeed        => _rotationSpeed;
        public float InteractionRadius    => _interactionRadius;
        public GameObject CharacterMeshPrefab => _characterMeshPrefab;
        
    }
}
