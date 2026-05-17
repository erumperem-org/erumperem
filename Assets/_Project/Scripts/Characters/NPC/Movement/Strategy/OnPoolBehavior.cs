using UnityEngine;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;


namespace Core.Exploration.Character.Movement
{
    public class OnPoolBehavior : IReverseableCharacterMovementStartegy
    {
        public Task ExecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is OnPoolBehaviorContext onPoolBehaviorContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Character [{onPoolBehaviorContext.characterData.name}], is entering [OnPoolBehavior]");
                onPoolBehaviorContext.self.transform.position = onPoolBehaviorContext.newPosition;
                if (onPoolBehaviorContext.parent != null)
                {
                    onPoolBehaviorContext.self.transform.parent = onPoolBehaviorContext.parent;
                }
            }

            return Task.CompletedTask;
        }

        public Task UnexecuteBehavior(ICharacterMovementStartegyContext context)
        {
            if (context is OnPoolBehaviorContext onPoolBehaviorContext)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.AI, $"Character [{onPoolBehaviorContext.characterData.name}], is exiting [OnPoolBehavior]");
                onPoolBehaviorContext.self.transform.position = onPoolBehaviorContext.newPosition;
                if (onPoolBehaviorContext.parent != null)
                {
                    onPoolBehaviorContext.self.transform.parent = onPoolBehaviorContext.parent;
                }
            }

            return Task.CompletedTask;
        }

        public void CancelImmediate() { }
    }

    public class OnPoolBehaviorContext : ICharacterMovementStartegyContext
    {
        public CharacterData characterData;
        public Vector3 newPosition;
        public Transform parent;
        public Transform self;
        public OnPoolBehaviorContext(CharacterData character, Vector3 newPosition, Transform parent, Transform self)
        {
            this.characterData = character;
            this.newPosition = newPosition;
            this.parent = parent;
            this.self = self;
        }
    }
}
