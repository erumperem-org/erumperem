using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// BASE SYNERGY CONTRACT
// Root interface for all reverseable token interactions.
namespace Core.Tokens
{
    public interface IReverseableSynergy { void ReverseSynergy(TokenContainerController tokenContainer); }
}
