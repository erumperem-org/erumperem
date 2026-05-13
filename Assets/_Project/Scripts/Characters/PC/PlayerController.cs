// =============================================================================
// PlayerController.cs
// Orquestrador central da Layered State Machine do personagem.
//
// RESPONSABILIDADE:
//   - Montar o PlayerContext com referências aos componentes Unity
//   - Inicializar as 3 camadas (Locomotion, Interaction, UseItem)
//   - Chamar Update() em cada camada a cada frame (na ordem correta)
//   - Conectar o InputReader ao CharacterSwitcher
//   - Não contém lógica de gameplay — apenas coordenação
//
// ORDEM DE UPDATE DAS CAMADAS:
//   1. Locomotion  → move o personagem; atualiza IsGrounded, Velocity
//   2. Interaction → detecta interatáveis; lê IsGrounded para contexto
//   3. UseItem     → lê IsInteracting para bloquear uso simultâneo
//
//   Essa ordem garante que cada camada sempre lê dados atualizados das
//   camadas que a precedem no mesmo frame.
//
// HIERARQUIA ESPERADA NA CENA:
//   Player (root)
//   ├── PlayerController   ← este componente
//   ├── CharacterController
//   ├── InputReader
//   └── MeshRoot           ← filho onde o mesh/animator vive
//       ├── Animator       ← no mesh filho
//       └── AnimationBridge
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using CharacterSystem.Core;
using CharacterSystem.Animation;
using CharacterSystem.Character;
using CharacterSystem.Input;
using CharacterSystem.Layers.Locomotion;
using CharacterSystem.Layers.Interaction;
using CharacterSystem.Layers.UseItem;
using UnityEngine.AI;
using Services.Navigation;

namespace CharacterSystem
{
    /// <summary>
    /// Ponto de entrada da lógica do personagem.
    /// Configura e coordena todas as camadas e sistemas.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Câmera")]
        [Tooltip("Transform da câmera principal. Usado para calcular direção de movimento.")]
        [SerializeField] private Transform _cameraTransform;

        [Header("Mesh Root")]
        [Tooltip("Transform filho onde o mesh e o Animator vivem. " +
                 "Deve ter um AnimationBridge e um Animator como componentes.")]
        [SerializeField] private Transform _meshRoot;

        [Header("Personagens")]
        [Tooltip("Lista dos 3 CharacterData, na ordem das teclas 1, 2, 3.")]
        [SerializeField] private List<PlayableCharacterData> _characters;

        // ── Contexto ─────────────────────────────────────────────────────────

        private PlayerContext _context; 
        // ── Camadas ──────────────────────────────────────────────────────────

        private LocomotionLayer _locomotionLayer;
        private InteractionLayer _interactionLayer;
        private UseItemLayer _useItemLayer;

        // ── Sistemas ─────────────────────────────────────────────────────────

        private CharacterSwitcher _switcher;
        private InputReader _inputReader;

        // ── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            // Valida dependências antes de montar o sistema
            ValidateDependencies();

            // Monta o contexto com referências aos componentes Unity
            _context = BuildContext();

            // Instancia as camadas
            _locomotionLayer = new LocomotionLayer();
            _interactionLayer = new InteractionLayer();
            _useItemLayer = new UseItemLayer();

            // Instancia o switcher
            _switcher = new CharacterSwitcher(_characters, _meshRoot);

            // Obtém e configura o InputReader
            _inputReader = GetComponent<InputReader>();
            _inputReader.Context = _context;
            _inputReader.OnCharacterSwitchRequested += OnSwitchRequested;
        }

        private void Start()
        {
            // Inicializa o switcher primeiro (popula ctx.ActiveCharacterData
            // e ctx.AnimationBridge antes das camadas inicializarem)
            _switcher.Initialize(_context, _meshRoot.gameObject);

            // Inicializa as camadas na ordem
            _locomotionLayer.Initialize(_context);
            _interactionLayer.Initialize(_context);
            _useItemLayer.Initialize(_context);
        }

        private void Update()
        {
            // Atualiza as camadas na ordem definida.
            // InputReader.Update() roda antes (Script Execution Order).
            _locomotionLayer.Update(_context);
            _interactionLayer.Update(_context);
            _useItemLayer.Update(_context);
        }

        private void OnDestroy()
        {
            // Desinscreve do evento para evitar referências pendentes
            if (_inputReader != null)
                _inputReader.OnCharacterSwitchRequested -= OnSwitchRequested;
        }

        // ── Callbacks ────────────────────────────────────────────────────────

        /// <summary>
        /// Chamado pelo InputReader quando o jogador pressiona 1, 2 ou 3.
        /// </summary>
        private void OnSwitchRequested(int index)
        {
            //_switcher.SwitchTo(index, _context, _meshRoot.gameObject);
        }

        // ── Helpers de Setup ─────────────────────────────────────────────────

        /// <summary>
        /// Monta o PlayerContext com referências aos componentes da cena.
        /// </summary>
        private PlayerContext BuildContext()
        {
            return new PlayerContext
            {
                Agent = GetComponent<NavMeshAgent>(),
                NavMeshService = GetComponent<NavMeshService>(),
                CameraTransform = _cameraTransform,
                AnimationBridge = _meshRoot.GetComponentInChildren<AnimationBridge>()
            };
        }

        /// <summary>
        /// Valida dependências críticas no Inspector antes de rodar.
        /// </summary>
        private void ValidateDependencies()
        {
            if (_cameraTransform == null)
                Debug.LogError("[PlayerController] _cameraTransform não atribuído.", this);

            if (_meshRoot == null)
                Debug.LogError("[PlayerController] _meshRoot não atribuído.", this);

            if (_characters == null || _characters.Count == 0)
                Debug.LogError("[PlayerController] Nenhum CharacterData configurado.", this);

            if (_characters != null && _characters.Count < 3)
                Debug.LogWarning($"[PlayerController] Esperados 3 personagens, " +
                                 $"encontrados {_characters.Count}.", this);

            if (!TryGetComponent<InputReader>(out _))
                Debug.LogError("[PlayerController] InputReader não encontrado.", this);

            if (!TryGetComponent<NavMeshAgent>(out _))
                Debug.LogError("[PlayerController] NavMeshAgent não encontrado.", this);
                
            if (!TryGetComponent<NavMeshService>(out _))
                Debug.LogError("[PlayerController] NavMeshService não encontrado.", this);
        }

#if UNITY_EDITOR
        // ── Debug Visual ─────────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (_characters == null || _characters.Count == 0) return;

            // Desenha o raio de interação do personagem ativo
            var data = _characters[0];
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            Gizmos.DrawSphere(transform.position, data.InteractionRadius);
            Gizmos.color = new Color(0f, 1f, 0.5f, 1f);
            Gizmos.DrawWireSphere(transform.position, data.InteractionRadius);
        }
#endif
    }
}
