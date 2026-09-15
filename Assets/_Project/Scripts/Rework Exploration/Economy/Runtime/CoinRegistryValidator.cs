using System.Collections.Generic;
using UnityEngine;

namespace Core.Economy.Currency
{
    /// <summary>
    /// Structural validation of a CoinRegistry: typing, presence and
    /// uniqueness of StorageableId. Does not depend on UnityEditor — reusable
    /// from automated tests or a CI step, mirroring ItemRegistryValidator.
    /// </summary>
    public static class CoinRegistryValidator
    {
        public readonly struct ValidationError
        {
            public readonly string Message;
            public readonly ScriptableObject Context;

            public ValidationError(string message, ScriptableObject context)
            {
                Message = message;
                Context = context;
            }
        }

        public static IReadOnlyList<ValidationError> Validate(CoinRegistry registry)
        {
            var errors = new List<ValidationError>();
            var seenIds = new Dictionary<string, ScriptableObject>();

            foreach (var obj in registry.Coins)
            {
                if (obj == null)
                {
                    errors.Add(new ValidationError("Null element in the coin list.", registry));
                    continue;
                }

                if (obj is not ICoin coin)
                {
                    errors.Add(new ValidationError($"'{obj.name}' does not implement ICoin.", obj));
                    continue;
                }

                if (string.IsNullOrEmpty(coin.StorageableId))
                {
                    errors.Add(new ValidationError($"'{obj.name}' has an empty StorageableId.", obj));
                    continue;
                }

                if (seenIds.TryGetValue(coin.StorageableId, out var existing))
                {
                    errors.Add(new ValidationError(
                        $"Duplicate StorageableId '{coin.StorageableId}': '{existing.name}' and '{obj.name}'.", obj));
                    continue;
                }

                seenIds[coin.StorageableId] = obj;
            }

            return errors;
        }
    }
}
