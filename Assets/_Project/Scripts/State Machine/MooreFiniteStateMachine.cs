using Services.DebugUtilities;

namespace Core.StateMachine.FiniteStateMachine
{
    public class MooreFiniteStateMachine<TState> where TState : UiState
    {
        private TState _currentState;

        public TState CurrentState => _currentState;

        public void TransitionTo(TState nextState)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, $"Moore Finite State Machine transitioning from [{CurrentState}] to [{nextState.StateName}]", LogCategory.StateMachine);
            _currentState?.OnExit();
            _currentState = nextState;
            _currentState.OnEnter();
        }
    }
}

