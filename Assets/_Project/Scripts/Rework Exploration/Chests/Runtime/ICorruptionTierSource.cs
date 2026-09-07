using System;

namespace Core.Chests
{
    /// <summary>
    /// Abstraction over the real source of the corruption value/tier.
    /// A concrete implementation should plug into this once the actual
    /// corruption system is shared/integrated.
    /// </summary>
    public interface ICorruptionTierSource
    {
        int CurrentTier { get; }
        event Action<int> OnTierChanged;
    }
}
