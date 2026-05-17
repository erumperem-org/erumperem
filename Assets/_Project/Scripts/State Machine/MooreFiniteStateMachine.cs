using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.StateMachine.FiniteStateMachine
{
    public class MooreFiniteStateMachine<TState> where TState : UiState
    {
        private TState _currentState;

        public TState CurrentState => _currentState;

        public void TransitionTo(TState nextState)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.StateMachine, $"Moore Finite State Machine transitioning from [{CurrentState}] to [{nextState.StateName}]");
            _currentState?.OnExit();
            _currentState = nextState;
            _currentState.OnEnter();
        }
    }
}

