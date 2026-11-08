using System.Collections.Generic;
using Erumperem.Characters;

namespace Core.CharacterStats
{
    /// <summary>
    /// Rule: an item cannot be used if it would push the CURRENT value
    /// (base + saved modification) of ANY stat it touches below 50% of the
    /// ORIGINAL (base) value.
    ///
    /// Notes:
    ///  - The item's modifiers are simulated sequentially on a copy; nothing is persisted.
    ///  - Only items that actually LOWER the stat can be blocked: if the stat
    ///    is already under the floor (e.g. data changed), raising it is still allowed.
    ///  - With base = 0 (e.g. default Defense) the floor is 0, so any decrease is blocked.
    /// </summary>
    public static class StatApplicabilityRule
    {
        public const float MinimumFractionOfBase = 0.5f;

        private const float Epsilon = 1e-4f;

        public static ItemApplicability Evaluate(
            AllyCharacterStatDefinition target,
            CharacterStatsData saved,
            IReadOnlyList<ResolvedStatModifier> modifiers)
        {
            if (target == null)
                return ItemApplicability.Denied("No character selected.");

            if (modifiers == null || modifiers.Count == 0)
                return ItemApplicability.Allowed;

            var working = new CharacterStatsData(saved?.AsReadOnlyDictionary());
            var startDeltas = new Dictionary<StatType, float>();

            foreach (var modifier in modifiers)
            {
                float baseValue = CharacterStatResolver.GetBase(target, modifier.Stat);
                float delta = working.Get(modifier.Stat);

                if (!startDeltas.ContainsKey(modifier.Stat))
                    startDeltas[modifier.Stat] = delta;

                working.Set(modifier.Stat, delta + StatModifierMath.ComputeDeltaChange(baseValue, delta, modifier));
            }

            foreach (var pair in startDeltas)
            {
                float baseValue = CharacterStatResolver.GetBase(target, pair.Key);
                float start = baseValue + pair.Value;
                float final = baseValue + working.Get(pair.Key);
                float floor = baseValue * MinimumFractionOfBase;

                bool dropsBelowFloor = final < floor - Epsilon;
                bool isLowering = final < start - Epsilon;

                if (dropsBelowFloor && isLowering)
                {
                    return ItemApplicability.Denied(
                        $"{target.DisplayName}: {pair.Key} would fall to {final:0.##} " +
                        $"(minimum allowed is {floor:0.##}, {MinimumFractionOfBase:P0} of base {baseValue:0.##}).");
                }
            }

            return ItemApplicability.Allowed;
        }
    }
}
