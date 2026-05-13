using System.Threading.Tasks;

namespace Core.Exploration.Character.Movement
{
    public interface ICharacterMovementStartegy
    {
        Task ExecuteBehavior(ICharacterMovementStartegyContext context);
    }
    public interface IReverseableCharacterMovementStartegy :  ICharacterMovementStartegy
    {
        Task UnexecuteBehavior(ICharacterMovementStartegyContext context);
        void CancelImmediate();
    }

    public interface ICharacterMovementStartegyContext { }
}
