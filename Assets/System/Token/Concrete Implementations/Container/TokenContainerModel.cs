using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Tokens;
using Unity.VisualScripting;

namespace Core.Tokens
{
    /// <summary>
    /// Pure data container for tokens.
    /// Responsible only for holding the current state of tokens within a container,
    /// following the MVC pattern (Model layer).
    /// Does not perform validation, stacking, or synergy logic.
    /// </summary>
    [Serializable]
    public sealed class TokenContainerModel
    {
        [SerializeField]
        public List<TokenController> tokens = new List<TokenController>();
    }
}