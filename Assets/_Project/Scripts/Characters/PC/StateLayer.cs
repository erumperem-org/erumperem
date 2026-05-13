// =============================================================================
// StateLayer.cs
// Implementação genérica e reutilizável de IStateLayer.
// Cada camada (Locomotion, Interaction, UseItem) instancia um StateLayer
// configurado com seus estados específicos.
//
// FLUXO DE TRANSIÇÃO:
//   1. Caller chama TryTransition(nextState)
//   2. StateLayer pergunta ao CurrentState: CanTransitionTo(next)?
//   3. Se sim: CurrentState.OnExit → next.OnEnter → CurrentState = next
//   4. Se não: transição silenciosamente ignorada (não é um erro)
// =============================================================================

using System;
using UnityEngine;
using CharacterSystem.Core;

namespace CharacterSystem.StateMachine
{
    /// <summary>
    /// Camada genérica da Layered State Machine.
    /// Gerencia o ciclo de vida de estados (enter/update/exit/transition).
    /// </summary>
    public class StateLayer : IStateLayer
    {
        // ── Campos privados ──────────────────────────────────────────────────

        private ICharacterState _currentState;
        private readonly ICharacterState _defaultState;
        private readonly string _layerName;

        // Evento opcional para observar mudanças de estado (útil para debug/UI)
        public event Action<ICharacterState, ICharacterState> OnStateChanged;

        // ── Construtor ───────────────────────────────────────────────────────

        /// <summary>
        /// Cria uma camada com seu estado padrão (inicial).
        /// </summary>
        /// <param name="layerName">Nome legível para logs.</param>
        /// <param name="defaultState">Estado ativo ao inicializar.</param>
        public StateLayer(string layerName, ICharacterState defaultState)
        {
            _layerName    = layerName     ?? throw new ArgumentNullException(nameof(layerName));
            _defaultState = defaultState  ?? throw new ArgumentNullException(nameof(defaultState));
        }

        // ── IStateLayer ──────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string LayerName => _layerName;

        /// <inheritdoc/>
        public ICharacterState CurrentState => _currentState;

        /// <inheritdoc/>
        public void Initialize(PlayerContext context)
        {
            _currentState = _defaultState;
            _currentState.OnEnter(context);
            Debug.Log($"[{_layerName}] Inicializado em: {_currentState.StateName}");
        }

        /// <inheritdoc/>
        public void Update(PlayerContext context)
        {
            _currentState?.OnUpdate(context);
        }

        /// <inheritdoc/>
        public bool TryTransition(ICharacterState nextState, PlayerContext context)
        {
            if (nextState == null)
            {
                Debug.LogWarning($"[{_layerName}] TryTransition recebeu estado nulo.");
                return false;
            }

            // Não faz nada se já está no estado pedido
            if (_currentState == nextState) return false;

            // Pergunta ao estado atual se a transição é permitida
            if (!_currentState.CanTransitionTo(nextState, context))
            {
                // Silencioso por design — transições bloqueadas são normais
                return false;
            }

            // Executa a transição
            var previous = _currentState;
            _currentState.OnExit(context);
            _currentState = nextState;
            _currentState.OnEnter(context);

            OnStateChanged?.Invoke(previous, _currentState);

            Debug.Log($"[{_layerName}] {previous.StateName} → {_currentState.StateName}");
            return true;
        }
    }
}
