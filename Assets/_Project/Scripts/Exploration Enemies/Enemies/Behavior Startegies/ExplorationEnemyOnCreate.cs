using System;
using UnityEngine;
using Services.DebugUtilities.Console;
using Services.DebugUtilities;

namespace Core.Exploration.Enemies
{
    public class ExplorationEnemyOnCreate : IExplorationEnemyStrategy
    {
        public ExplorationEnemyStates state => ExplorationEnemyStates.OnCreate;

        public void EnterBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy OnCreate strategy called");
        }
        public void ExitBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy OnCreate strategy exit");
        }
    }

    [Serializable]
    public struct ExplorationEnemyOnCreateData{}
}
