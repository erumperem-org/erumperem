using System;
using UnityEngine;
using Services.DebugUtilities.Console;
using Services.DebugUtilities;

namespace Core.Exploration.Enemies
{
    public class ExplorationEnemyOnDestroy : IExplorationEnemyStrategy
    {
        public ExplorationEnemyStates state => ExplorationEnemyStates.OnDestroy;

        public void EnterBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy OnDestroy strategy called");
        }
        public void ExitBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy OnDestroy strategy exit");
        }
    }

    [Serializable]
    public struct ExplorationEnemyOnDestroyData{}
}
