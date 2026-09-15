using System.Collections.Generic;

namespace Core.SceneAllocation
{
    /// <summary>
    /// Structural validation of a PlaceableObjectRegistry: missing data
    /// references, empty ids, duplicate ids. No UnityEditor dependency —
    /// reusable from automated tests or CI, mirroring ItemRegistryValidator.
    /// </summary>
    public static class PlaceableObjectRegistryValidator
    {
        public readonly struct ValidationError
        {
            public readonly string Message;

            public ValidationError(string message) => Message = message;
        }

        public static IReadOnlyList<ValidationError> Validate(PlaceableObjectRegistry registry)
        {
            var errors = new List<ValidationError>();
            var seenIds = new HashSet<string>();

            for (int i = 0; i < registry.Entries.Count; i++)
            {
                var entry = registry.Entries[i];

                if (entry.Data == null)
                {
                    errors.Add(new ValidationError($"Entry {i} has no PlaceableObjectData assigned."));
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Id))
                {
                    errors.Add(new ValidationError($"Entry {i} ('{entry.Data.name}') has an empty id."));
                    continue;
                }

                if (!seenIds.Add(entry.Id))
                {
                    errors.Add(new ValidationError($"Duplicate id '{entry.Id}' (entry {i}, '{entry.Data.name}')."));
                }
            }

            return errors;
        }
    }
}
