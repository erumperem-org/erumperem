using Erumperem.Characters;

namespace Core.CharacterStats
{
    /// <summary>
    /// Extra layer for items that are NOT bound to a specific character and
    /// therefore need to be told WHICH character they act on (status items).
    ///
    /// IIITem stays untouched: the parameterless ExecuteItemEffect() is still
    /// part of the contract, but for these items it cannot do anything
    /// meaningful without a target. Callers that know about this layer (the
    /// item-use flow) must use the overload below instead.
    ///
    /// Items already bound to a character (e.g. per-character skill tree
    /// reset) or that need none (general reset) do not implement this.
    /// </summary>
    public interface ICharacterTargetedItem
    {
        /// <summary>Pure check — must not mutate any state.</summary>
        ItemApplicability CanApplyTo(AllyCharacterStatDefinition target);

        /// <summary>
        /// Applies the effect to <paramref name="target"/>. Implementations must
        /// re-check <see cref="CanApplyTo"/> and refuse when it is denied.
        /// </summary>
        void ExecuteItemEffect(AllyCharacterStatDefinition target);
    }
}
