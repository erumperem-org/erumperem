using System;
using UnityEngine;
using Services.DebugUtilities.Console;
using Services.DebugUtilities;

namespace Core.Exploration.Enemies
{
    public class ExplorationEnemyOnPool : IExplorationEnemyStrategy
    {
        public ExplorationEnemyStates state => ExplorationEnemyStates.OnPool;

        public void EnterBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy OnPool strategy called");
        }

        public void ExitBehavior()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Lifecycle, "Enemy Pool strategy exit");
        }
    }

    [Serializable]
    public struct ExplorationEnemyOnPoolData{}
}