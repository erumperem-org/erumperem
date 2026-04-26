using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// BASE SYNERGY CONTRACT
// Root interface for all token interaction rules.
namespace Core.Tokens
{
    public interface ITokenSynergy { bool CanApply(TokenAllocationContext context) => true; }

}
