using System;
using Core.Tokens;
using UnityEngine;
using System.Collections.Generic;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using System.Text.RegularExpressions;

namespace Core.Tokens
{
    [Serializable]
    public class TokenController : ITokenExecutionTrigger
    {
        public TokenModel data;

        public TokenController(string displayName, TokenStackData stackingdata, ITokenAllocationStyle allocationStyle)
        {
            data = new TokenModel(displayName, stackingdata, allocationStyle, $"Assets/_Project/Materials & Textures/Materials/{displayName}.mat");
        }

        public virtual void ExecuteTokenEffect()
        {
            ExecuteTokenEffectMessage();
        }

        private void ExecuteTokenEffectMessage() =>
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Applying Token Effect [{Regex.Replace(data.tokenDisplayName, @"(?<!^)(?=[A-Z][a-z])", " ").ToUpper()}]");
    }
}
