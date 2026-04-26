using System;
using Unity.VisualScripting;

// STACKING MODEL
// Defines how multiple instances of the same token behave.

namespace Core.Tokens
{
    // Marker only — each allocation creates a new independent instance.
    [Serializable]
    public sealed class IndependentStackData : TokenStackData
    {
    }
}
